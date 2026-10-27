using System;
using Game.Battle.Domain.Rules;
using Game.Battle.Simulation.Components;
using Unity.Entities;

namespace Game.Battle.Simulation.Systems
{
    /// <summary>固定步首先结算中毒，保证毒死单位不能在本步行动。按周期总数批量计算，不逐 tick 循环。</summary>
    [DisableAutoCreation]
    [UpdateBefore(typeof(MonsterObjectiveSystem))]
    [UpdateBefore(typeof(MonsterNavigationSystem))]
    [UpdateBefore(typeof(SoldierNavigationSystem))]
    [UpdateBefore(typeof(MovementSystem))]
    public partial class UnitStatusSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            double now = SystemAPI.Time.ElapsedTime;
            foreach (var (status, vitals, entity) in SystemAPI.Query<RefRW<UnitStatusState>, RefRW<Vitals>>().WithEntityAccess())
            {
                if (EntityManager.HasComponent<HealthRegenerationState>(entity))
                {
                    var regeneration = EntityManager.GetComponentData<HealthRegenerationState>(entity);
                    regeneration.StatusBlockedUntil = status.ValueRO.HealthRegenerationBlockedUntil;
                    EntityManager.SetComponentData(entity, regeneration);
                }
                ResolvePoison(ref status.ValueRW, ref vitals.ValueRW, now);
            }
        }

        /// <summary>桥接在替换中毒前也调用此结算，避免丢掉已到期而尚未推进的周期。</summary>
        internal static void ResolvePoison(ref UnitStatusState status, ref Vitals vitals, double now)
        {
            if (vitals.Health <= 0)
            {
                status = default;
                return;
            }
            if (status.PoisonDamagePerTick <= 0 || status.PoisonIntervalSeconds <= 0)
                return;
            // 终止时刻上的最后一跳仍结算；过期后调用也不能漏掉到期前的周期。
            double lastDueTime = Math.Min(now, status.PoisonedUntil);
            // 周期小数不能精确表示；容忍十亿分之一个周期的舍入，避免 0.3 秒漏掉第三次 0.1 秒跳伤。
            const double tickRoundingTolerance = 1e-9;
            double elapsedPeriods = (lastDueTime - status.NextPoisonTickAt) / status.PoisonIntervalSeconds;
            if (elapsedPeriods >= -tickRoundingTolerance)
            {
                double dueCount = Math.Floor(elapsedPeriods + tickRoundingTolerance) + 1;
                // 按每跳发生时刻判无敌，而不是用当前时间一次性裁决全部补跳。
                double protectedCount = Math.Max(0, Math.Ceiling((status.InvulnerableUntil - status.NextPoisonTickAt) / status.PoisonIntervalSeconds - tickRoundingTolerance));
                double damageCount = Math.Max(0, dueCount - Math.Min(dueCount, protectedCount));
                if (damageCount > 0)
                {
                    double damagingTicks = Math.Min(damageCount, Math.Ceiling((double)vitals.Health / status.PoisonDamagePerTick));
                    int damage = (int)Math.Min(vitals.Health, damageCount * status.PoisonDamagePerTick);
                    vitals.Health = ContactRules.ApplyDamage(vitals.Health, damage);
                    vitals.LastHitTime = (float)(status.NextPoisonTickAt + (Math.Min(dueCount, protectedCount) + damagingTicks - 1) * status.PoisonIntervalSeconds);
                }
                status.NextPoisonTickAt += dueCount * status.PoisonIntervalSeconds;
            }
            if (vitals.Health <= 0)
                status = default;
            else if (now >= status.PoisonedUntil)
            {
                status.PoisonedUntil = 0;
                status.NextPoisonTickAt = 0;
                status.PoisonIntervalSeconds = 0;
                status.PoisonDamagePerTick = 0;
            }
        }
    }
}
