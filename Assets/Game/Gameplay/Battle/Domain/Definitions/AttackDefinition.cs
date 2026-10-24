using System;

namespace Game.Battle.Domain.Definitions
{
    /// <summary>单位与战斗的只读内容定义，不保存对局可变状态。</summary>
    public sealed class AttackDefinition
    {
        public string Id { get; }
        public AttackMode Mode { get; }
        public int Damage { get; }
        public float Interval { get; }
        public float Range { get; }
        public string EffectIds { get; }
        public AttackDefinition(string id, string mode, int damage, float interval, float range, string effectIds)
        {
            if (!Enum.TryParse<AttackMode>(mode, out var attackMode) || !Enum.IsDefined(typeof(AttackMode), attackMode) || attackMode.ToString() != mode)
            {
                throw new ArgumentException("Unknown attack mode: " + mode, nameof(mode));
            }
            if (string.IsNullOrWhiteSpace(id) || damage < 0 || !Positive(interval) || !Nonnegative(range))
            {
                throw new ArgumentException("Invalid AttackDefinition");
            }
            Id = id;
            Mode = attackMode;
            Damage = damage;
            Interval = interval;
            Range = range;
            EffectIds = effectIds;
        }
        private static bool Nonnegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
        private static bool Positive(float value) => Nonnegative(value) && value > 0;
    }
}
