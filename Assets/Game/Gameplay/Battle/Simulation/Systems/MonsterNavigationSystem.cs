using System;
using System.Collections.Generic;
using Game.Battle.Domain;
using Game.MapEngine.Domain;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation
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
            _pathfinder = new GridPathfinder(map);
            _pathCells.Clear();
        }

        protected override void OnUpdate()
        {
            if (_map == null) return;
            Entity hero = SystemAPI.GetSingletonEntity<HeroTag>();
            var heroGround = SystemAPI.GetComponent<GroundTransform>(hero);
            bool heroAlive = SystemAPI.GetComponent<Vitals>(hero).Health > 0;
            double now = SystemAPI.Time.ElapsedTime;
            float rangeSquared = _definition.DetectionRadius * _definition.DetectionRadius;
            bool heroInMap = _map.TryWorldToCell(heroGround.Position.x, heroGround.Position.y, out var goal);

            // Query 会完成此前移动 Job 对位置、导航进度和路径 Buffer 的冲突依赖。
            // 主线程依次重用 A* 工作区，不会由不同实体同时写入同一搜索数组。
            foreach (var (ground, vitals, navigation, wander, path) in SystemAPI.Query<
                RefRO<GroundTransform>, RefRO<Vitals>, RefRW<MonsterNavigationState>,
                RefRW<WanderState>, DynamicBuffer<NavigationWaypoint>>().WithAll<MonsterTag>())
            {
                float distanceSquared = math.lengthsq(heroGround.Position - ground.ValueRO.Position);
                bool shouldChase = heroAlive && heroInMap && vitals.ValueRO.Health > 0 && distanceSquared <= rangeSquared;
                if (!shouldChase)
                {
                    // 一旦脱离感知范围就切换，不等待下一次重算；恢复游走时选新的随机方向。
                    if (navigation.ValueRO.IsChasing != 0) wander.ValueRW.Remaining = 0;
                    navigation.ValueRW = default;
                    path.Clear();
                    continue;
                }

                bool enteringChase = navigation.ValueRO.IsChasing == 0;
                navigation.ValueRW.IsChasing = 1;
                if (enteringChase || now >= navigation.ValueRO.NextRepathTime)
                {
                    path.Clear();
                    navigation.ValueRW.WaypointIndex = 0;
                    navigation.ValueRW.GoalCell = new int2(goal.X, goal.Y);
                    navigation.ValueRW.NextRepathTime = now + _definition.RepathSeconds;
                    bool startInMap = _map.TryWorldToCell(ground.ValueRO.Position.x, ground.ValueRO.Position.y, out var start);
                    if (startInMap && _pathfinder.TryFindPath(start, goal, _pathCells))
                    {
                        // 不先绕到起点格中心。终点用英雄的连续坐标，同格时也能完成靠近。
                        for (int i = 1; i < _pathCells.Count; i++)
                        {
                            var center = _map.CellCenter(_pathCells[i]);
                            path.Add(new NavigationWaypoint { Position = new float2(center.X, center.Y) });
                        }
                        if (path.Length == 0)
                            path.Add(new NavigationWaypoint { Position = heroGround.Position });
                    }
                    // 无路时保持追踪但不移动，按配置重试；不能沿旧路线穿过无效地块。
                }

                if (path.Length > 0)
                {
                    // 格子内的移动也要跟随，不能只追到英雄所在格子的中心就停止。
                    var writablePath = path;
                    writablePath[path.Length - 1] = new NavigationWaypoint { Position = heroGround.Position };
                }
            }
        }
    }
}
