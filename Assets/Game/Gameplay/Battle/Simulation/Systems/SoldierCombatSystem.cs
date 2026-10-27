using Game.Battle.Domain.Rules;
using Game.Battle.Domain.ValueObjects;
using Game.Battle.Simulation.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Systems
{
    /// <summary>移动后复核共享目标并结算士兵/怪物普通攻击；英雄接战在此前阶段处理，全部消费同一冷却。</summary>
    [DisableAutoCreation]
    [UpdateAfter(typeof(CircleContactSystem))]
    [UpdateBefore(typeof(StructureCombatSystem))]
    public partial class SoldierCombatSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // 移动可能使目标死亡/离开限制范围；有效目标仍保持，不能在攻击阶段另挑更近的目标。
            World.GetOrCreateSystemManaged<CombatTargetSelectionSystem>().Update();
            double now = SystemAPI.Time.ElapsedTime;
            foreach (var (ground, health, motion, profile, soldier, targets, actor) in
                SystemAPI.Query<RefRW<GroundTransform>, RefRW<Vitals>, RefRO<UnitMotion>, RefRO<CombatProfile>,
                    RefRW<SoldierState>, RefRO<CombatTargetState>>().WithEntityAccess())
            {
                Entity target = targets.ValueRO.Target;
                if (soldier.ValueRO.Behavior == UnitBehavior.Regrouping || health.ValueRO.Health <= 0 ||
                    !NormalAttackRules.IsReady(now, health.ValueRO.NextNormalAttackAt) || !IsLivingUnit(target) ||
                    !EntityManager.HasComponent<MonsterTag>(target)) continue;
                // 状态入口保持在实际结算处，不能仅依赖导航前的判断。
                if (!CanAttack(actor, now) || !WithinHeroLeash(soldier.ValueRO.FollowCombatRadius, ground.ValueRO.Position)) continue;
                if (!InRange(ground.ValueRO.Position, motion.ValueRO.Radius, profile.ValueRO.AttackRange, target)) continue;
                soldier.ValueRW.Behavior = UnitBehavior.Attacking;
                Strike(target, health.ValueRO.Attack, now);
                health.ValueRW.LastAttackTime = (float)now;
                health.ValueRW.NextNormalAttackAt = NormalAttackRules.NextAttackAt(now, profile.ValueRO.AttackInterval);
                Face(ref ground.ValueRW, EntityManager.GetComponentData<GroundTransform>(target).Position);
            }
            foreach (var (ground, health, motion, profile, targets, attacker) in
                SystemAPI.Query<RefRW<GroundTransform>, RefRW<Vitals>, RefRO<UnitMotion>, RefRO<CombatProfile>, RefRO<CombatTargetState>>()
                    .WithAll<MonsterTag>().WithEntityAccess())
            {
                Entity target = targets.ValueRO.Target;
                if (health.ValueRO.Health <= 0 || !CanAttack(attacker, now) ||
                    !NormalAttackRules.IsReady(now, health.ValueRO.NextNormalAttackAt) || !IsLivingUnit(target) ||
                    !EntityManager.HasComponent<SoldierState>(target) ||
                    !InRange(ground.ValueRO.Position, motion.ValueRO.Radius, profile.ValueRO.AttackRange, target)) continue;
                Strike(target, health.ValueRO.Attack, now);
                var soldier = EntityManager.GetComponentData<SoldierState>(target);
                if (soldier.Behavior != UnitBehavior.Regrouping) soldier.RetaliationTarget = attacker;
                EntityManager.SetComponentData(target, soldier);
                health.ValueRW.LastAttackTime = (float)now;
                health.ValueRW.NextNormalAttackAt = NormalAttackRules.NextAttackAt(now, profile.ValueRO.AttackInterval);
                Face(ref ground.ValueRW, EntityManager.GetComponentData<GroundTransform>(target).Position);
            }
        }

        private bool WithinHeroLeash(float radius, float2 position)
        {
            if (radius <= 0) return true;
            foreach (var (hero, vital) in SystemAPI.Query<RefRO<GroundTransform>, RefRO<Vitals>>().WithAll<HeroTag>())
                if (vital.ValueRO.Health > 0 && math.distancesq(position, hero.ValueRO.Position) > radius * radius) return false;
            return true;
        }

        private bool CanAttack(Entity entity, double now) => !EntityManager.HasComponent<UnitStatusState>(entity) ||
            EntityManager.GetComponentData<UnitStatusState>(entity).CanAttackAt(now);

        private bool IsLivingUnit(Entity entity) => entity != Entity.Null && EntityManager.Exists(entity) &&
            EntityManager.HasComponent<Vitals>(entity) && EntityManager.GetComponentData<Vitals>(entity).Health > 0;

        private bool InRange(float2 position, float radius, float range, Entity target)
        {
            float reach = radius + EntityManager.GetComponentData<UnitMotion>(target).Radius + range;
            return math.distancesq(position, EntityManager.GetComponentData<GroundTransform>(target).Position) <= reach * reach;
        }

        private void Strike(Entity target, int attack, double now)
        {
            if (EntityManager.HasComponent<UnitStatusState>(target) &&
                EntityManager.GetComponentData<UnitStatusState>(target).IsInvulnerableAt(now)) return;
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
