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
            foreach (var (ground, motion, vitals, navigation, profile) in SystemAPI.Query<RefRO<GroundTransform>, RefRO<UnitMotion>, RefRW<Vitals>, RefRW<MonsterNavigationState>, RefRO<CombatProfile>>()
                .WithAll<MonsterTag>())
            {
                Entity target = navigation.ValueRO.StructureTarget;
                if (vitals.ValueRO.Health <= 0 || target == Entity.Null || !EntityManager.Exists(target))
                    continue;
                var structure = EntityManager.GetComponentData<DefenseStructure>(target);
                float reach = motion.ValueRO.Radius + profile.ValueRO.AttackRange;
                float distance = math.distance(ground.ValueRO.Position, math.clamp(ground.ValueRO.Position, structure.Min, structure.Max));
                if (structure.Health <= 0 || distance > reach + 0.0001f)
                    continue;
                navigation.ValueRW.Behavior = UnitBehavior.Attacking;
                if (now < vitals.ValueRO.LastAttackTime + (profile.ValueRO.AttackInterval > 0 ? profile.ValueRO.AttackInterval : 1))
                    continue;
                structure.Health = ContactRules.ApplyDamage(structure.Health, vitals.ValueRO.Attack);
                vitals.ValueRW.LastAttackTime = (float)now;
                EntityManager.SetComponentData(target, structure);
            }
        }
    }
}
