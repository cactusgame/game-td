using System;

namespace Game.Battle.Domain.Definitions
{
    /// <summary>单位与战斗的只读内容定义，不保存对局可变状态。</summary>
    public sealed class EffectDefinition
    {
        public string Id { get; }
        public string Kind { get; }
        public float Duration { get; }
        public float Magnitude { get; }
        public int MaxStacks { get; }
        public EffectDefinition(string id, string kind, float duration, float magnitude, int maxStacks)
        {
            if (string.IsNullOrWhiteSpace(id) || (!Positive(duration) || !Nonnegative(magnitude) || maxStacks <= 0 || (kind != "Slow" && kind != "Damage" && kind != "Heal") || (kind == "Slow" && magnitude > 1)))
                throw new ArgumentException("Invalid EffectDefinition");
            Id = id;
            Kind = kind;
            Duration = duration;
            Magnitude = magnitude;
            MaxStacks = maxStacks;
        }
        private static bool Nonnegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
        private static bool Positive(float value) => Nonnegative(value) && value > 0;
    }
}
