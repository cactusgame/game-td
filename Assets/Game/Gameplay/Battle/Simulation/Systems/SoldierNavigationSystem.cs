using System.Collections.Generic;
using Game.Battle.Simulation.Components;
using Game.MapEngine.Domain;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Systems
{
    /// <summary>单位与战斗领域的士兵意图：优先交战，否则寻路跟随存活英雄；敌人近身时停步反击。</summary>
    [DisableAutoCreation]
    [UpdateAfter(typeof(MonsterNavigationSystem))]
    [UpdateBefore(typeof(MovementSystem))]
    public partial class SoldierNavigationSystem : SystemBase
    {
        private GridMap _map;
        private GridPathfinder _pathfinder;
        private readonly List<GridCell> _cells = new List<GridCell>();
        public void Configure(GridMap map)
        {
            _map = map;
            _pathfinder = new GridPathfinder(map);
        }
        protected override void OnUpdate()
        {
            if (_map == null) return;
            double now = SystemAPI.Time.ElapsedTime;
            var hero = SystemAPI.GetSingletonEntity<HeroTag>();
            var heroGround = SystemAPI.GetComponent<GroundTransform>(hero);
            var heroMotion = SystemAPI.GetComponent<UnitMotion>(hero);
            bool heroAlive = SystemAPI.GetComponent<Vitals>(hero).Health > 0;
            foreach (var (ground, health, motion, profile, navigation, path) in
                SystemAPI.Query<RefRO<GroundTransform>, RefRO<Vitals>, RefRO<UnitMotion>, RefRO<CombatProfile>,
                    RefRW<MonsterNavigationState>, DynamicBuffer<NavigationWaypoint>>().WithAll<SoldierState>())
            {
                if (health.ValueRO.Health <= 0) continue;
                navigation.ValueRW.IsChasing = 1; // 空路线表示等待，不能进入随机游走。
                // 战斗优先于跟随；英雄死亡时原地警戒，不向尸体或旧营地继续移动。
                float followDistance = heroMotion.Radius + motion.ValueRO.Radius + 0.6f;
                if (HasEnemyInRange(ground.ValueRO.Position, motion.ValueRO.Radius, profile.ValueRO.AttackRange) ||
                    !heroAlive || math.distancesq(ground.ValueRO.Position, heroGround.Position) <= followDistance * followDistance)
                {
                    path.Clear();
                    navigation.ValueRW.NextRepathTime = 0;
                    continue;
                }
                if (now < navigation.ValueRO.NextRepathTime) continue;
                path.Clear();
                navigation.ValueRW.WaypointIndex = 0;
                navigation.ValueRW.NextRepathTime = now + 0.25;
                float2 target = heroGround.Position;
                if (_map.TryWorldToCell(ground.ValueRO.Position.x, ground.ValueRO.Position.y, out var start) &&
                    _map.TryWorldToCell(target.x, target.y, out var goal) && _pathfinder.TryFindPath(start, goal, _cells))
                {
                    for (int i = 1; i < _cells.Count; i++)
                    {
                        var center = _map.CellCenter(_cells[i]);
                        path.Add(new NavigationWaypoint { Position = new float2(center.X, center.Y) });
                    }
                    path.Add(new NavigationWaypoint { Position = target });
                }
            }
            foreach (var (ground, health, motion, profile, navigation, path) in
                SystemAPI.Query<RefRO<GroundTransform>, RefRO<Vitals>, RefRO<UnitMotion>, RefRO<CombatProfile>,
                    RefRW<MonsterNavigationState>, DynamicBuffer<NavigationWaypoint>>().WithAll<MonsterTag>())
            {
                if (health.ValueRO.Health <= 0) continue;
                foreach (var (soldier, vital, otherMotion) in SystemAPI.Query<RefRO<GroundTransform>, RefRO<Vitals>, RefRO<UnitMotion>>().WithAll<SoldierState>())
                {
                    float reach = motion.ValueRO.Radius + otherMotion.ValueRO.Radius + profile.ValueRO.AttackRange;
                    if (vital.ValueRO.Health <= 0 || math.distancesq(soldier.ValueRO.Position, ground.ValueRO.Position) > reach * reach) continue;
                    path.Clear();
                    navigation.ValueRW.IsChasing = 1;
                    navigation.ValueRW.NextRepathTime = now + 0.25;
                    break;
                }
            }
        }
        private bool HasEnemyInRange(float2 position, float radius, float range)
        {
            foreach (var (ground, vital, motion) in SystemAPI.Query<RefRO<GroundTransform>, RefRO<Vitals>, RefRO<UnitMotion>>().WithAll<MonsterTag>())
            {
                float reach = radius + motion.ValueRO.Radius + range;
                if (vital.ValueRO.Health > 0 && math.distancesq(position, ground.ValueRO.Position) <= reach * reach) return true;
            }
            return false;
        }
    }
}
