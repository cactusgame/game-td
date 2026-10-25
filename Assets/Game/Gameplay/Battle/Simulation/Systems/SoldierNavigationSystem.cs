using System.Collections.Generic;
using Game.Battle.Simulation.Components;
using Game.MapEngine.Domain;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Systems
{
    /// <summary>死守禁止移动；警戒在范围内追敌、受击反击，无目标时返回待机点。选择时遵循英雄跟随命令。</summary>
    [DisableAutoCreation]
    [UpdateAfter(typeof(MonsterNavigationSystem))]
    [UpdateBefore(typeof(MovementSystem))]
    public partial class SoldierNavigationSystem : SystemBase
    {
        private GridMap _map;
        private GridPathfinder _pathfinder;
        private readonly List<GridCell> _cells = new List<GridCell>();
        // 会话组装选项，默认警戒；不由动画或 UI 修改导航状态。
        public bool FollowHero { get; set; }
        public float DetectionRadius { get; set; } = 5;
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
            foreach (var (ground, health, motion, profile, soldier, navigation, path) in
                SystemAPI.Query<RefRO<GroundTransform>, RefRO<Vitals>, RefRO<UnitMotion>, RefRO<CombatProfile>,
                    RefRW<SoldierState>, RefRW<MonsterNavigationState>, DynamicBuffer<NavigationWaypoint>>())
            {
                if (health.ValueRO.Health <= 0) continue;
                navigation.ValueRW.IsChasing = 1; // 空路线表示等待，不能进入随机游走。
                if (soldier.ValueRO.IsStationed)
                {
                    soldier.ValueRW.IsRegrouping = false;
                    soldier.ValueRW.FollowCombatRadius = 0;
                    soldier.ValueRW.RetaliationTarget = Entity.Null;
                    path.Clear();
                    navigation.ValueRW.NextRepathTime = 0;
                    continue;
                }
                // 明确呼叫优先于交战；已归队士兵只在英雄附近作战，英雄离远后再次强制归队。
                float followDistance = heroMotion.Radius + motion.ValueRO.Radius + 0.6f;
                bool selected = soldier.ValueRO.IsSelected;
                bool followsHero = selected || (FollowHero && !soldier.ValueRO.HasPlayerOrder);
                float2 target = selected ? heroGround.Position + soldier.ValueRO.FollowOffset :
                    followsHero ? heroGround.Position : soldier.ValueRO.Rally;
                float stopDistance = selected ? 0.15f : followsHero ? followDistance : 0.08f;
                // 跟随点随英雄更新；贴墙时偏移点可能进入障碍，改为接近英雄本身。
                if (selected && !_map.CanOccupy(target.x, target.y, motion.ValueRO.Radius))
                {
                    target = heroGround.Position;
                    stopDistance = followDistance;
                }
                float combatRadius = math.max(DetectionRadius, math.length(soldier.ValueRO.FollowOffset) + followDistance);
                soldier.ValueRW.FollowCombatRadius = followsHero && heroAlive ? combatRadius : 0;
                if (followsHero && !heroAlive)
                {
                    // 不跟随死亡英雄的幽灵。原地防御，复活后再依据新位置判断是否归队。
                    soldier.ValueRW.IsRegrouping = false;
                    soldier.ValueRW.RetaliationTarget = Entity.Null;
                    path.Clear();
                    navigation.ValueRW.NextRepathTime = 0;
                    continue;
                }
                if (followsHero && !soldier.ValueRO.IsRegrouping &&
                    math.distancesq(ground.ValueRO.Position, heroGround.Position) > combatRadius * combatRadius)
                {
                    soldier.ValueRW.IsRegrouping = true;
                    path.Clear();
                    navigation.ValueRW.NextRepathTime = 0;
                }
                if (soldier.ValueRO.IsRegrouping)
                {
                    soldier.ValueRW.RetaliationTarget = Entity.Null;
                    if (math.distancesq(ground.ValueRO.Position, target) <= stopDistance * stopDistance)
                        soldier.ValueRW.IsRegrouping = false;
                }
                float2 combatCenter = followsHero ? heroGround.Position : ground.ValueRO.Position;
                float allowedRadius = followsHero ? combatRadius : float.PositiveInfinity;
                if (!soldier.ValueRO.IsRegrouping)
                {
                    Entity enemy = soldier.ValueRO.RetaliationTarget;
                    if (!IsLivingMonster(enemy) ||
                        math.distancesq(EntityManager.GetComponentData<GroundTransform>(enemy).Position, combatCenter) > allowedRadius * allowedRadius)
                    {
                        soldier.ValueRW.RetaliationTarget = Entity.Null;
                        enemy = FindNearbyEnemy(ground.ValueRO.Position, combatCenter, allowedRadius);
                    }
                    if (enemy != Entity.Null)
                    {
                        target = EntityManager.GetComponentData<GroundTransform>(enemy).Position;
                        // 停在攻击距离内，避免弓兵追到敌人中心才停下。
                        stopDistance = motion.ValueRO.Radius + EntityManager.GetComponentData<UnitMotion>(enemy).Radius + profile.ValueRO.AttackRange;
                    }
                }
                if ((!soldier.ValueRO.IsRegrouping && HasEnemyInRange(ground.ValueRO.Position, motion.ValueRO.Radius, profile.ValueRO.AttackRange, combatCenter, allowedRadius)) ||
                    math.distancesq(ground.ValueRO.Position, target) <= stopDistance * stopDistance)
                {
                    path.Clear();
                    navigation.ValueRW.NextRepathTime = 0;
                    continue;
                }
                if (now < navigation.ValueRO.NextRepathTime) continue;
                path.Clear();
                navigation.ValueRW.WaypointIndex = 0;
                navigation.ValueRW.NextRepathTime = now + 0.25;
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
        private bool IsLivingMonster(Entity entity)
        {
            return entity != Entity.Null && EntityManager.Exists(entity) &&
                EntityManager.HasComponent<MonsterTag>(entity) && EntityManager.GetComponentData<Vitals>(entity).Health > 0;
        }

        private Entity FindNearbyEnemy(float2 position, float2 allowedCenter, float allowedRadius)
        {
            Entity target = Entity.Null;
            float nearest = DetectionRadius * DetectionRadius;
            foreach (var (ground, vital, entity) in SystemAPI.Query<RefRO<GroundTransform>, RefRO<Vitals>>().WithAll<MonsterTag>().WithEntityAccess())
            {
                float distance = math.distancesq(position, ground.ValueRO.Position);
                if (vital.ValueRO.Health <= 0 || distance > nearest)
                    continue;
                if (math.distancesq(ground.ValueRO.Position, allowedCenter) > allowedRadius * allowedRadius)
                    continue;
                nearest = distance;
                target = entity;
            }
            return target;
        }
        private bool HasEnemyInRange(float2 position, float radius, float range, float2 allowedCenter, float allowedRadius)
        {
            foreach (var (ground, vital, motion) in SystemAPI.Query<RefRO<GroundTransform>, RefRO<Vitals>, RefRO<UnitMotion>>().WithAll<MonsterTag>())
            {
                float reach = radius + motion.ValueRO.Radius + range;
                if (vital.ValueRO.Health > 0 && math.distancesq(position, ground.ValueRO.Position) <= reach * reach &&
                    math.distancesq(ground.ValueRO.Position, allowedCenter) <= allowedRadius * allowedRadius) return true;
            }
            return false;
        }
    }
}
