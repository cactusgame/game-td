using System;

namespace Game.Battle.Contracts.ReadModels
{
    /// <summary>模拟复制的技能冷却读模型。进度从0填充到1，表现不自行计时或决定技能可用性。</summary>
    public readonly struct SkillCooldownSnapshot
    {
        public readonly double RemainingSeconds;
        public readonly float DurationSeconds;
        public bool IsCoolingDown => RemainingSeconds > 0 && DurationSeconds > 0;
        public float Progress => DurationSeconds > 0
            ? (float)Math.Max(0, Math.Min(1, 1 - RemainingSeconds / DurationSeconds))
            : 1;

        public SkillCooldownSnapshot(double remainingSeconds, float durationSeconds)
        {
            RemainingSeconds = Math.Max(0, remainingSeconds);
            DurationSeconds = durationSeconds;
        }
    }
}
