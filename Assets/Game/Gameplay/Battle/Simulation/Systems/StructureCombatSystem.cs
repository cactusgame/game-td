using Game.Battle.Domain.Rules;
using Game.Battle.Domain.ValueObjects;
using Game.Battle.Simulation.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Systems
{
    /// <summary>移动和接触完成后，顺序结算塔射击与怪物攻城，避免同一生命被并行写入。</summary>
    [DisableAutoCreation]
    [UpdateAfter(typeof(CircleContactSystem))]
    public partial class StructureCombatSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            double now = SystemAPI.Time.ElapsedTime;
            foreach (var structure in SystemAPI.Query<RefRW<DefenseStructure>>())
            {
                if (structure.ValueRO.Health <= 0)
                    continue;
                var bounds = structure.ValueRO;
                Entity target = Entity.Null;
                float nearest = bounds.Range * bounds.Range;
                foreach (var (ground, vitals, entity) in SystemAPI.Query<RefRO<GroundTransform>, RefRO<Vitals>>()
                    .WithAll<MonsterTag>().WithEntityAccess())
                {
                    if (vitals.ValueRO.Health <= 0)
                        continue;
                    float distance = math.distancesq(ground.ValueRO.Position, math.clamp(ground.ValueRO.Position, bounds.Min, bounds.Max));
                    if (distance <= nearest)
                    {
                        nearest = distance;
                        target = entity;
                    }
                }
                // 被塔锁定时仍在交战，包括射击冷却和无敌挡伤期间；不能夹在两次射击间恢复。
                if (bounds.Attack > 0 && target != Entity.Null && EntityManager.HasComponent<HealthRegenerationState>(target))
                {
                    var regeneration = EntityManager.GetComponentData<HealthRegenerationState>(target);
                    regeneration.CombatBlockedUntil = now + HealthRegenerationSystem.OutOfCombatDelaySeconds;
                    regeneration.AccumulatedHealing = 0;
                    EntityManager.SetComponentData(target, regeneration);
                }
                if (bounds.Attack > 0 && now >= bounds.NextAttack && target != Entity.Null)
                {
                    var health = EntityManager.GetComponentData<Vitals>(target);
                    bool invulnerable = EntityManager.HasComponent<UnitStatusState>(target) &&
                        EntityManager.GetComponentData<UnitStatusState>(target).IsInvulnerableAt(now);
                    if (!invulnerable)
                    {
                        health.Health = ContactRules.ApplyDamage(health.Health, ContactRules.Damage(bounds.Attack, health.Defense));
                        health.LastHitTime = (float)now;
                    }
                    EntityManager.SetComponentData(target, health);
                    structure.ValueRW.NextAttack = now + bounds.AttackInterval;
                }
            }
            foreach (var (ground, motion, vitals, navigation, profile, monster) in SystemAPI.Query<RefRO<GroundTransform>, RefRO<UnitMotion>, RefRW<Vitals>, RefRW<MonsterNavigationState>, RefRO<CombatProfile>>()
                .WithAll<MonsterTag>().WithEntityAccess())
            {
                if (EntityManager.HasComponent<UnitStatusState>(monster) &&
                    !EntityManager.GetComponentData<UnitStatusState>(monster).CanAttackAt(now))
                    continue;
                Entity target = navigation.ValueRO.StructureTarget;
                if (EntityManager.HasComponent<CombatTargetState>(monster))
                {
                    Entity selected = EntityManager.GetComponentData<CombatTargetState>(monster).Target;
                    if (selected != Entity.Null && selected != target) continue;
                }
                if (vitals.ValueRO.Health <= 0 || target == Entity.Null || !EntityManager.Exists(target))
                    continue;
                var structure = EntityManager.GetComponentData<DefenseStructure>(target);
                float reach = motion.ValueRO.Radius + profile.ValueRO.AttackRange;
                float distance = math.distance(ground.ValueRO.Position, math.clamp(ground.ValueRO.Position, structure.Min, structure.Max));
                if (structure.Health <= 0 || distance > reach + 0.0001f)
                    continue;
                navigation.ValueRW.Behavior = UnitBehavior.Attacking;
                navigation.ValueRW.StopReason = MovementStopReason.InAttackRange;
                if (!NormalAttackRules.IsReady(now, vitals.ValueRO.NextNormalAttackAt))
                    continue;
                structure.Health = ContactRules.ApplyDamage(structure.Health, vitals.ValueRO.Attack);
                vitals.ValueRW.LastAttackTime = (float)now;
                vitals.ValueRW.NextNormalAttackAt = NormalAttackRules.NextAttackAt(now, profile.ValueRO.AttackInterval);
                EntityManager.SetComponentData(target, structure);
            }
        }
    }
}
