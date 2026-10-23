using System;

namespace Game.Battle.Domain.Definitions
{
    /// <summary>单位与战斗的只读内容定义，不保存对局可变状态。</summary>
    public sealed class SkillDefinition
    {
        public string Id { get; }
        public bool Enabled { get; }
        public float Cooldown { get; }
        public string AttackId { get; }
        public string EffectIds { get; }
        public SkillDefinition(string id, bool enabled, float cooldown, string attackId, string effectIds)
        {
            if (string.IsNullOrWhiteSpace(id) || (!Positive(cooldown)))
                throw new ArgumentException("Invalid SkillDefinition");
            Id = id;
            Enabled = enabled;
            Cooldown = cooldown;
            AttackId = attackId;
            EffectIds = effectIds;
        }
        private static bool Nonnegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
        private static bool Positive(float value) => Nonnegative(value) && value > 0;
    }
}
