using System;
using Game.Battle.Contracts.Commands;
using Game.Battle.Domain.ValueObjects;
using Game.Battle.Simulation.Components;
using Game.Battle.Simulation.Systems;

namespace Game.Battle.Simulation.Bridges
{
    public sealed partial class EcsBattleSimulation
    {
        /// <summary>统一效果提交入口，不依赖单位阵营。控制效果取较晚期限；中毒替换参数并重置周期。</summary>
        public bool TryApplyStatus(ApplyUnitStatusCommand command)
        {
            var em = _world.EntityManager;
            em.CompleteAllTrackedJobs();
            double until = ElapsedTime + command.DurationSeconds;
            // 同时拒绝 default 命令以及当前时间精度下无法推进的期限。
            if (!_unitEntities.TryGetValue(command.UnitId, out var entity) || !em.Exists(entity) ||
                command.Kind > UnitStatusKind.Poison || double.IsNaN(until) || double.IsInfinity(until) || until <= ElapsedTime)
                return false;
            if (command.Kind == UnitStatusKind.Poison && (command.PoisonDamagePerTick <= 0 ||
                double.IsNaN(command.PoisonIntervalSeconds) || double.IsInfinity(command.PoisonIntervalSeconds) ||
                ElapsedTime + command.PoisonIntervalSeconds <= ElapsedTime ||
                double.IsInfinity(ElapsedTime + command.PoisonIntervalSeconds)))
                return false;
            var vitals = em.GetComponentData<Vitals>(entity);
            var status = em.GetComponentData<UnitStatusState>(entity);
            UnitStatusSystem.ResolvePoison(ref status, ref vitals, ElapsedTime);
            if (vitals.Health <= 0)
            {
                em.SetComponentData(entity, vitals);
                em.SetComponentData(entity, status);
                return false;
            }
            switch (command.Kind)
            {
                case UnitStatusKind.Stun:
                    status.StunnedUntil = Math.Max(status.StunnedUntil, until);
                    break;
                case UnitStatusKind.Root:
                    status.RootedUntil = Math.Max(status.RootedUntil, until);
                    break;
                case UnitStatusKind.Silence:
                    status.SilencedUntil = Math.Max(status.SilencedUntil, until);
                    break;
                case UnitStatusKind.Poison:
                    status.PoisonedUntil = until;
                    status.PoisonDamagePerTick = command.PoisonDamagePerTick;
                    status.PoisonIntervalSeconds = command.PoisonIntervalSeconds;
                    status.NextPoisonTickAt = ElapsedTime + command.PoisonIntervalSeconds;
                    break;
            }
            em.SetComponentData(entity, vitals);
            em.SetComponentData(entity, status);
            return true;
        }
    }
}
