using System;
using Game.MapEngine.Domain;
using Game.Battle.Domain.ValueObjects;

namespace Game.Battle.Domain.Definitions
{
    /// <summary>某类单位的只读属性定义，不持有场上实例状态。</summary>
    public sealed class UnitDefinition
    {
        public UnitKind Kind { get; }

        public int MaxHealth { get; }

        public int Attack { get; }

        public int Defense { get; }

        public float Speed { get; }

        // 逻辑碰撞半径与画面高度分开；放大 Sprite 不会改变接触检测结果。
        public float Radius { get; }

        public float VisualHeight { get; }

        public UnitDefinition(UnitKind kind, int maxHealth, int attack, int defense, float speed, float radius, float visualHeight)
        {
            if (kind != UnitKind.Hero && kind != UnitKind.Goblin && kind != UnitKind.Runner)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), "Unknown unit kind.");
            }

            if (maxHealth <= 0 || attack < 0 || defense < 0)
                throw new ArgumentException("Health must be positive; attack and defense must be nonnegative.");
            RequirePositive(speed, nameof(speed));
            RequirePositive(radius, nameof(radius));
            RequirePositive(visualHeight, nameof(visualHeight));
            Kind = kind;
            MaxHealth = maxHealth;
            Attack = attack;
            Defense = defense;
            Speed = speed;
            Radius = radius;
            VisualHeight = visualHeight;
        }

        internal static void RequirePositive(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
                throw new ArgumentOutOfRangeException(name, "Must be finite and positive.");
        }
    }
}
