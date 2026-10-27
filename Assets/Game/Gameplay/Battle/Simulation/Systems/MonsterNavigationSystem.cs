using Game.Battle.Simulation.Components;
using System;
using System.Collections.Generic;
using Game.Battle.Domain.Definitions;
using Game.Battle.Domain.ValueObjects;
using Game.MapEngine.Domain;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Systems
{
    /// <summary>
    /// 在移动之前批量处理感知和路径请求。只有低频 A* 在主线程运行，
    /// 共用搜索工作区但将结果复制到每只怪物的 ECS Buffer；没有每怪物托管对象。
    /// </summary>
    [DisableAutoCreation]
    [UpdateBefore(typeof(MovementSystem))]
    public partial class MonsterNavigationSystem : SystemBase
    {
        private GridMap _map;
        private NavigationDefinition _definition;
        private GridPathfinder _pathfinder;
        private readonly List<GridCell> _pathCells = new List<GridCell>();
        public void Configure(GridMap map, NavigationDefinition definition)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            World.GetOrCreateSystemManaged<CombatTargetSelectionSystem>().Navigation = definition;
            _pathfinder = new GridPathfinder(map);
            _pathCells.Clear();
        }

        protected override void OnUpdate()
        {
            if (_map == null)
                return;
            double now = SystemAPI.Time.ElapsedTime;
            foreach (var (ground, vitals, navigation, wander, path, motion, targets, actor) in
                SystemAPI.Query<RefRO<GroundTransform>, RefRO<Vitals>, RefRW<MonsterNavigationState>, RefRW<WanderState>,
                    DynamicBuffer<NavigationWaypoint>, RefRO<UnitMotion>, RefRO<CombatTargetState>>().WithAll<MonsterTag>().WithEntityAccess())
            {
                if (vitals.ValueRO.Health <= 0)
                {
                    navigation.ValueRW = default;
                    navigation.ValueRW.Behavior = UnitBehavior.Dead;
                    navigation.ValueRW.StopReason = MovementStopReason.Dead;
                    path.Clear();
                    continue;
                }
                Entity target = targets.ValueRO.Target;
                bool hasTarget = target != Entity.Null && EntityManager.Exists(target);
                bool structure = hasTarget && EntityManager.HasComponent<DefenseStructure>(target);
                float2 targetPosition = structure ? navigation.ValueRO.StructureGoal :
                    hasTarget ? EntityManager.GetComponentData<GroundTransform>(target).Position : ground.ValueRO.Position;
                if (!hasTarget || !_map.TryWorldToCell(targetPosition.x, targetPosition.y, out var goal))
                {
                    if (navigation.ValueRO.HasCastleObjective != 0)
                    {
                        navigation.ValueRW.IsChasing = 1;
                        navigation.ValueRW.Behavior = UnitBehavior.Idle;
                        navigation.ValueRW.StopReason = MovementStopReason.NoTarget;
                        navigation.ValueRW.NextRepathTime = now + _definition.RepathSeconds;
                    }
                    else
                    {
                        if (navigation.ValueRO.IsChasing != 0) wander.ValueRW.Remaining = 0;
                        navigation.ValueRW = default;
                        navigation.ValueRW.Behavior = UnitBehavior.Moving;
                    }
                    path.Clear();
                    continue;
                }
                navigation.ValueRW.Behavior = UnitBehavior.Chasing;
                navigation.ValueRW.StopReason = MovementStopReason.None;
                if (!structure)
                {
                    float range = EntityManager.HasComponent<CombatProfile>(actor)
                        ? EntityManager.GetComponentData<CombatProfile>(actor).AttackRange : 0;
                    float reach = motion.ValueRO.Radius + EntityManager.GetComponentData<UnitMotion>(target).Radius + range;
                    if (math.distancesq(ground.ValueRO.Position, targetPosition) <= reach * reach)
                    {
                        navigation.ValueRW.IsChasing = 1;
                        navigation.ValueRW.Behavior = UnitBehavior.Attacking;
                        navigation.ValueRW.StopReason = MovementStopReason.InAttackRange;
                        navigation.ValueRW.NextRepathTime = now + _definition.RepathSeconds;
                        path.Clear();
                        continue;
                    }
                }
                bool enteringChase = navigation.ValueRO.IsChasing == 0;
                navigation.ValueRW.IsChasing = 1;
                if (enteringChase || now >= navigation.ValueRO.NextRepathTime)
                {
                    path.Clear();
                    navigation.ValueRW.WaypointIndex = 0;
                    navigation.ValueRW.GoalCell = new int2(goal.X, goal.Y);
                    navigation.ValueRW.NextRepathTime = now + _definition.RepathSeconds;
                    navigation.ValueRW.StopReason = MovementStopReason.NoPath;
                    if (_map.TryWorldToCell(ground.ValueRO.Position.x, ground.ValueRO.Position.y, out var start) &&
                        _pathfinder.TryFindPath(start, goal, _pathCells))
                    {
                        for (int i = 1; i < _pathCells.Count; i++)
                        {
                            var center = _map.CellCenter(_pathCells[i]);
                            path.Add(new NavigationWaypoint { Position = new float2(center.X, center.Y) });
                        }
                        if (path.Length == 0) path.Add(new NavigationWaypoint { Position = targetPosition });
                        navigation.ValueRW.StopReason = MovementStopReason.None;
                    }
                }
                if (path.Length == 0 && navigation.ValueRO.StopReason == MovementStopReason.None)
                    navigation.ValueRW.StopReason = MovementStopReason.NoPath;
                if (path.Length > 0)
                {
                    var writablePath = path;
                    writablePath[path.Length - 1] = new NavigationWaypoint { Position = targetPosition };
                }
            }
        }
    }
}
