using System;
using System.Collections.Generic;
using Game.Battle.Simulation.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Systems
{
    /// <summary>战斗结算后，为脱战且无异常状态的存活单位恢复生命；不改受击和攻击时间。</summary>
    [DisableAutoCreation]
    [UpdateAfter(typeof(UnitStatusSystem))]
    [UpdateAfter(typeof(StructureCombatSystem))]
    [UpdateBefore(typeof(HeroRespawnSystem))]
    public partial class HealthRegenerationSystem : SystemBase
    {
        public const double OutOfCombatDelaySeconds = 5;
        private readonly HashSet<Entity> _engagedUnits = new HashSet<Entity>();

        protected override void OnUpdate()
        {
            double now = SystemAPI.Time.ElapsedTime;
            double deltaTime = SystemAPI.Time.DeltaTime;
            CollectEngagedUnits();
            foreach (var (vitals, regeneration, entity) in SystemAPI.Query<RefRW<Vitals>, RefRW<HealthRegenerationState>>().WithEntityAccess())
            {
                regeneration.ValueRW.LastRecoveryAt = now;
                regeneration.ValueRW.HealthRestoredThisStep = 0;
                if (vitals.ValueRO.Health <= 0 || vitals.ValueRO.Health >= vitals.ValueRO.MaxHealth || regeneration.ValueRO.HealthPerSecond <= 0)
                {
                    regeneration.ValueRW.AccumulatedHealing = 0;
                    continue;
                }

                if (_engagedUnits.Contains(entity))
                    regeneration.ValueRW.CombatBlockedUntil = now + OutOfCombatDelaySeconds;
                double combatBlockedUntil = Math.Max(regeneration.ValueRO.CombatBlockedUntil,
                    Math.Max(vitals.ValueRO.LastHitTime, vitals.ValueRO.LastAttackTime) + OutOfCombatDelaySeconds);
                double statusBlockedUntil = regeneration.ValueRO.StatusBlockedUntil;
                if (EntityManager.HasComponent<UnitStatusState>(entity))
                    statusBlockedUntil = Math.Max(statusBlockedUntil, EntityManager.GetComponentData<UnitStatusState>(entity).HealthRegenerationBlockedUntil);
                double blockedUntil = Math.Max(combatBlockedUntil, statusBlockedUntil);
                double stepStartedAt = now - deltaTime;
                if (blockedUntil > stepStartedAt)
                    regeneration.ValueRW.AccumulatedHealing = 0;
                double recoverySeconds = Math.Max(0, now - Math.Max(stepStartedAt, blockedUntil));
                if (recoverySeconds <= 0)
                    continue;

                double accumulated = regeneration.ValueRO.AccumulatedHealing + regeneration.ValueRO.HealthPerSecond * recoverySeconds;
                int missingHealth = vitals.ValueRO.MaxHealth - vitals.ValueRO.Health;
                // 容忍浮点步长误差；先按缺失生命截断，避免大恢复量转整数溢出。
                int restoredHealth = (int)Math.Min(missingHealth, Math.Floor(accumulated + 0.000001));
                vitals.ValueRW.Health += restoredHealth;
                regeneration.ValueRW.HealthRestoredThisStep = restoredHealth;
                regeneration.ValueRW.AccumulatedHealing = restoredHealth == missingHealth ? 0 : Math.Max(0, accumulated - restoredHealth);
            }
        }

        private void CollectEngagedUnits()
        {
            _engagedUnits.Clear();
            foreach (var (vitals, target, actor) in SystemAPI.Query<RefRO<Vitals>, RefRO<CombatTargetState>>().WithEntityAccess())
            {
                if (vitals.ValueRO.Health <= 0 || !IsEngagedTarget(actor, target.ValueRO.Target))
                    continue;
                _engagedUnits.Add(actor);
                _engagedUnits.Add(target.ValueRO.Target);
            }
        }

        private bool IsEngagedTarget(Entity actor, Entity target)
        {
            if (target == Entity.Null || target == actor || !EntityManager.Exists(target))
                return false;
            if (EntityManager.HasComponent<Vitals>(target))
                return EntityManager.GetComponentData<Vitals>(target).Health > 0;
            // 远处的主城只是行军目的地；进入攻击距离才算与建筑交战。
            if (!EntityManager.HasComponent<DefenseStructure>(target) || !EntityManager.HasComponent<GroundTransform>(actor) ||
                !EntityManager.HasComponent<UnitMotion>(actor) || !EntityManager.HasComponent<CombatProfile>(actor))
                return false;
            var structure = EntityManager.GetComponentData<DefenseStructure>(target);
            var position = EntityManager.GetComponentData<GroundTransform>(actor).Position;
            float reach = EntityManager.GetComponentData<UnitMotion>(actor).Radius + EntityManager.GetComponentData<CombatProfile>(actor).AttackRange;
            return structure.Health > 0 && math.distance(position, math.clamp(position, structure.Min, structure.Max)) <= reach + 0.0001f;
        }
    }
}
