using Game.Battle.Domain.Definitions;
using Game.Battle.Domain.ValueObjects;
using Game.Battle.Simulation.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Systems
{
    /// <summary>导航前统一选择单位战斗目标。仍合法的旧目标优先，不因另一个目标略近而切换。</summary>
    [DisableAutoCreation]
    [UpdateAfter(typeof(UnitStatusSystem))]
    [UpdateAfter(typeof(MonsterObjectiveSystem))]
    [UpdateBefore(typeof(MonsterNavigationSystem))]
    [UpdateBefore(typeof(SoldierNavigationSystem))]
    public partial class CombatTargetSelectionSystem : SystemBase
    {
        public float SoldierDetectionRadius { get; set; } = 5;
        public NavigationDefinition Navigation { get; set; } = new NavigationDefinition();
        private EntityQuery _monsters;
        private EntityQuery _friendlies;
        private EntityQuery _heroes;

        protected override void OnCreate()
        {
            _monsters = GetEntityQuery(typeof(MonsterTag), typeof(GroundTransform), typeof(Vitals), typeof(UnitMotion));
            _friendlies = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<GroundTransform>(), ComponentType.ReadOnly<Vitals>(), ComponentType.ReadOnly<UnitMotion>() },
                Any = new[] { ComponentType.ReadOnly<HeroTag>(), ComponentType.ReadOnly<SoldierState>() }
            });
            _heroes = GetEntityQuery(typeof(HeroTag), typeof(GroundTransform), typeof(Vitals), typeof(UnitMotion));
        }

        protected override void OnUpdate()
        {
            using var monsters = _monsters.ToEntityArray(Allocator.Temp);
            using var friendlies = _friendlies.ToEntityArray(Allocator.Temp);
            Entity hero = _heroes.CalculateEntityCount() == 1 ? _heroes.GetSingletonEntity() : Entity.Null;
            foreach (var (ground, motion, vital, profile, target, actor) in
                SystemAPI.Query<RefRO<GroundTransform>, RefRO<UnitMotion>, RefRO<Vitals>, RefRO<CombatProfile>, RefRW<CombatTargetState>>().WithEntityAccess())
            {
                if (EntityManager.HasComponent<HeroTag>(actor)) continue; // 英雄不自动追敌，在接触阶段选射程内目标。
                if (vital.ValueRO.Health <= 0)
                {
                    target.ValueRW.Target = Entity.Null;
                    continue;
                }
                bool soldier = EntityManager.HasComponent<SoldierState>(actor);
                if (!soldier && !EntityManager.HasComponent<MonsterTag>(actor)) continue;
                Entity previous = target.ValueRO.Target;
                if (IsEligible(actor, previous, hero, soldier, true)) continue;
                Entity chosen = Entity.Null;
                var candidates = soldier ? monsters : friendlies;
                if (soldier)
                {
                    var state = EntityManager.GetComponentData<SoldierState>(actor);
                    if (IsEligible(actor, state.RetaliationTarget, hero, true, false))
                        chosen = state.RetaliationTarget;
                }
                float nearest = float.PositiveInfinity;
                if (chosen == Entity.Null)
                {
                    foreach (var candidate in candidates)
                    {
                        if (!IsEligible(actor, candidate, hero, soldier, false)) continue;
                        float distance = math.distancesq(ground.ValueRO.Position, EntityManager.GetComponentData<GroundTransform>(candidate).Position);
                        if (distance > nearest || (distance == nearest && chosen != Entity.Null && StableId(candidate) >= StableId(chosen))) continue;
                        nearest = distance;
                        chosen = candidate;
                    }
                }
                if (!soldier && chosen == Entity.Null && EntityManager.HasComponent<MonsterNavigationState>(actor))
                {
                    var navigation = EntityManager.GetComponentData<MonsterNavigationState>(actor);
                    if (IsLivingStructure(navigation.StructureTarget)) chosen = navigation.StructureTarget;
                }
                target.ValueRW.Target = chosen;
                if (previous != chosen && EntityManager.HasComponent<MonsterNavigationState>(actor))
                {
                    var navigation = EntityManager.GetComponentData<MonsterNavigationState>(actor);
                    navigation.NextRepathTime = 0;
                    EntityManager.SetComponentData(actor, navigation);
                }
            }
        }

        private bool IsEligible(Entity actor, Entity candidate, Entity hero, bool soldier, bool retained)
        {
            if (candidate == Entity.Null || !EntityManager.Exists(candidate) || candidate == actor ||
                !EntityManager.HasComponent<Vitals>(candidate) || EntityManager.GetComponentData<Vitals>(candidate).Health <= 0 ||
                !EntityManager.HasComponent<GroundTransform>(candidate) || !EntityManager.HasComponent<UnitMotion>(candidate)) return false;
            var ground = EntityManager.GetComponentData<GroundTransform>(actor);
            var motion = EntityManager.GetComponentData<UnitMotion>(actor);
            var profile = EntityManager.GetComponentData<CombatProfile>(actor);
            float2 point = EntityManager.GetComponentData<GroundTransform>(candidate).Position;
            float reach = motion.Radius + EntityManager.GetComponentData<UnitMotion>(candidate).Radius + profile.AttackRange;
            float distance = math.distancesq(ground.Position, point);
            if (soldier)
            {
                if (!EntityManager.HasComponent<MonsterTag>(candidate)) return false;
                var state = EntityManager.GetComponentData<SoldierState>(actor);
                bool heroAlive = hero != Entity.Null && EntityManager.GetComponentData<Vitals>(hero).Health > 0;
                bool following = state.Command == SoldierCommand.FollowHero;
                if (state.Stance == CombatStance.HoldGround || (following && !heroAlive)) return distance <= reach * reach;
                if (following && heroAlive)
                {
                    float2 center = EntityManager.GetComponentData<GroundTransform>(hero).Position;
                    float radius = math.max(SoldierDetectionRadius, math.length(state.FollowOffset) +
                        motion.Radius + EntityManager.GetComponentData<UnitMotion>(hero).Radius + 0.6f);
                    if (math.distancesq(point, center) > radius * radius || math.distancesq(ground.Position, center) > radius * radius) return false;
                }
                return candidate == state.RetaliationTarget || distance <= math.max(SoldierDetectionRadius * SoldierDetectionRadius, reach * reach);
            }
            if (!EntityManager.HasComponent<HeroTag>(candidate) && !EntityManager.HasComponent<SoldierState>(candidate)) return false;
            var navigation = EntityManager.HasComponent<MonsterNavigationState>(actor)
                ? EntityManager.GetComponentData<MonsterNavigationState>(actor) : default;
            // 主堡仍是长期目标，只让已到攻击距离的单位打断攻城；已有士兵交战目标允许短距离追击。
            if (navigation.HasCastleObjective != 0 && (!retained || candidate == hero)) return distance <= reach * reach;
            float detection = retained ? Navigation.LoseTargetRadius : Navigation.DetectionRadius;
            return distance <= math.max(detection * detection, reach * reach);
        }

        private bool IsLivingStructure(Entity entity) => entity != Entity.Null && EntityManager.Exists(entity) &&
            EntityManager.HasComponent<DefenseStructure>(entity) && EntityManager.GetComponentData<DefenseStructure>(entity).Health > 0;

        private ulong StableId(Entity entity) => EntityManager.HasComponent<UnitIdentity>(entity)
            ? EntityManager.GetComponentData<UnitIdentity>(entity).Id : (ulong)entity.Index;
    }
}
