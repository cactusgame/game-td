using Game.Battle.Domain.Rules;
using Game.Battle.Domain.ValueObjects;
using Game.Battle.Simulation.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Systems
{
    /// <summary>固定步内顺序结算士兵攻击和怪物近身反击。生命、攻击/受击时点只写 ECS。</summary>
    [DisableAutoCreation]
    [UpdateAfter(typeof(CircleContactSystem))]
    [UpdateBefore(typeof(StructureCombatSystem))]
    public partial class SoldierCombatSystem : SystemBase
    {
        private EntityQuery _heroes;

        protected override void OnCreate()
        {
            _heroes = GetEntityQuery(typeof(HeroTag), typeof(GroundTransform), typeof(Vitals));
        }

        protected override void OnUpdate()
        {
            double now = SystemAPI.Time.ElapsedTime;
            Entity hero = _heroes.CalculateEntityCount() == 1 ? _heroes.GetSingletonEntity() : Entity.Null;
            bool heroAlive = hero != Entity.Null && EntityManager.GetComponentData<Vitals>(hero).Health > 0;
            float2 heroPosition = heroAlive ? EntityManager.GetComponentData<GroundTransform>(hero).Position : default;
            foreach (var (ground, health, motion, profile, soldier, soldierEntity) in
                SystemAPI.Query<RefRW<GroundTransform>, RefRW<Vitals>, RefRO<UnitMotion>, RefRO<CombatProfile>, RefRW<SoldierState>>().WithEntityAccess())
            {
                // 归队不授予免伤，只暂停士兵主动攻击；怪物仍可正常攻击撤退中的士兵。
                if (soldier.ValueRO.Behavior == UnitBehavior.Regrouping) continue;
                if (!CanAttack(soldierEntity, now)) continue;
                if (health.ValueRO.Health <= 0 || now < health.ValueRO.LastAttackTime + profile.ValueRO.AttackInterval) continue;
                float leashRadius = soldier.ValueRO.FollowCombatRadius;
                if (heroAlive && leashRadius > 0 && math.distancesq(ground.ValueRO.Position, heroPosition) > leashRadius * leashRadius)
                    continue;
                Entity target = Entity.Null;
                float nearest = float.PositiveInfinity;
                foreach (var (other, vital, otherMotion, entity) in SystemAPI.Query<RefRO<GroundTransform>, RefRO<Vitals>, RefRO<UnitMotion>>().WithAll<MonsterTag>().WithEntityAccess())
                {
                    float distance = math.distancesq(ground.ValueRO.Position, other.ValueRO.Position);
                    float reach = motion.ValueRO.Radius + otherMotion.ValueRO.Radius + profile.ValueRO.AttackRange;
                    if (vital.ValueRO.Health <= 0 || distance > reach * reach || distance >= nearest) continue;
                    if (heroAlive && leashRadius > 0 && math.distancesq(other.ValueRO.Position, heroPosition) > leashRadius * leashRadius)
                        continue;
                    nearest = distance;
                    target = entity;
                }
                if (target == Entity.Null) continue;
                soldier.ValueRW.Behavior = UnitBehavior.Attacking;
                Strike(target, health.ValueRO.Attack, now);
                health.ValueRW.LastAttackTime = (float)now;
                Face(ref ground.ValueRW, EntityManager.GetComponentData<GroundTransform>(target).Position);
            }
            foreach (var (ground, health, motion, profile, attacker) in
                SystemAPI.Query<RefRW<GroundTransform>, RefRW<Vitals>, RefRO<UnitMotion>, RefRO<CombatProfile>>().WithAll<MonsterTag>().WithEntityAccess())
            {
                if (!CanAttack(attacker, now)) continue;
                if (health.ValueRO.Health <= 0 || now < health.ValueRO.LastAttackTime + profile.ValueRO.AttackInterval) continue;
                Entity target = Entity.Null;
                float nearest = float.PositiveInfinity;
                foreach (var (other, vital, otherMotion, entity) in SystemAPI.Query<RefRO<GroundTransform>, RefRO<Vitals>, RefRO<UnitMotion>>().WithAll<SoldierState>().WithEntityAccess())
                {
                    float distance = math.distancesq(ground.ValueRO.Position, other.ValueRO.Position);
                    float reach = motion.ValueRO.Radius + otherMotion.ValueRO.Radius + profile.ValueRO.AttackRange;
                    if (vital.ValueRO.Health <= 0 || distance > reach * reach || distance >= nearest) continue;
                    nearest = distance;
                    target = entity;
                }
                if (target == Entity.Null) continue;
                Strike(target, health.ValueRO.Attack, now);
                // 攻击事实触发反击，即使本次伤害被防御抵消。死守及跟随指令仍由导航优先裁决。
                var soldier = EntityManager.GetComponentData<SoldierState>(target);
                if (soldier.Behavior != UnitBehavior.Regrouping) soldier.RetaliationTarget = attacker;
                EntityManager.SetComponentData(target, soldier);
                health.ValueRW.LastAttackTime = (float)now;
                Face(ref ground.ValueRW, EntityManager.GetComponentData<GroundTransform>(target).Position);
            }
        }
        private bool CanAttack(Entity entity, double now)
        {
            return !EntityManager.HasComponent<UnitStatusState>(entity) ||
                EntityManager.GetComponentData<UnitStatusState>(entity).CanAttackAt(now);
        }

        private void Strike(Entity target, int attack, double now)
        {
            if (EntityManager.HasComponent<UnitStatusState>(target) &&
                EntityManager.GetComponentData<UnitStatusState>(target).IsInvulnerableAt(now))
                return;
            var vital = EntityManager.GetComponentData<Vitals>(target);
            int damage = ContactRules.Damage(attack, vital.Defense);
            vital.Health = ContactRules.ApplyDamage(vital.Health, damage);
            if (damage > 0) vital.LastHitTime = (float)now;
            EntityManager.SetComponentData(target, vital);
        }
        private static void Face(ref GroundTransform ground, float2 target)
        {
            float x = target.x - ground.Position.x;
            if (math.abs(x) > 0.0001f) ground.FacingX = math.sign(x);
        }
    }
}
