using System;
using Game.Battle.Domain.ValueObjects;

namespace Game.Battle.Contracts.Commands
{
    /// <summary>对稳定单位 ID 施加限时效果；中毒伤害为直接扣血，绕过防御但受无敌保护。</summary>
    public readonly struct ApplyUnitStatusCommand
    {
        public readonly ulong UnitId;
        public readonly UnitStatusKind Kind;
        public readonly double DurationSeconds;
        public readonly int PoisonDamagePerTick;
        public readonly double PoisonIntervalSeconds;

        public ApplyUnitStatusCommand(ulong unitId, UnitStatusKind kind, double durationSeconds,
            int poisonDamagePerTick = 0, double poisonIntervalSeconds = 1)
        {
            if (unitId == 0 || kind > UnitStatusKind.Poison ||
                double.IsNaN(durationSeconds) || double.IsInfinity(durationSeconds) || durationSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Status requires a unit, valid kind and finite positive duration.");
            if (kind == UnitStatusKind.Poison && (poisonDamagePerTick <= 0 ||
                double.IsNaN(poisonIntervalSeconds) || double.IsInfinity(poisonIntervalSeconds) || poisonIntervalSeconds <= 0))
                throw new ArgumentOutOfRangeException(nameof(poisonIntervalSeconds), "Poison requires positive damage and a finite positive interval.");
            UnitId = unitId;
            Kind = kind;
            DurationSeconds = durationSeconds;
            PoisonDamagePerTick = poisonDamagePerTick;
            PoisonIntervalSeconds = poisonIntervalSeconds;
        }
    }
}
