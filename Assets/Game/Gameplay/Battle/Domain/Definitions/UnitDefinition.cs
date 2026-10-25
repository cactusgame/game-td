using System;
using Game.MapEngine.Domain;
using Game.Battle.Domain.ValueObjects;

namespace Game.Battle.Domain.Definitions
{
    /// <summary>某类单位的只读属性定义，不持有场上实例状态。</summary>
    public sealed class UnitDefinition
    {
        public UnitKind Kind { get; }
        public string Id { get; }
        public int KillGold { get; }
        public float AttackInterval { get; }
        public float AttackRange { get; }
        public bool TargetMainCastle { get; }
        public bool CanAttackGates { get; }
        public System.Collections.Generic.IReadOnlyList<HeroAreaSkillDefinition> Skills { get; }

        public int MaxHealth { get; }

        public int Attack { get; }

        public int Defense { get; }

        public float Speed { get; }

        // 逻辑碰撞半径与画面高度分开；放大 Sprite 不会改变接触检测结果。
        public float Radius { get; }

        public float VisualHeight { get; }

        public UnitDefinition(UnitKind kind, int maxHealth, int attack, int defense, float speed, float radius, float visualHeight, string id = null, float attackInterval = 1, float attackRange = 0.15f, bool targetMainCastle = true, bool canAttackGates = true, System.Collections.Generic.IEnumerable<HeroAreaSkillDefinition> skills = null, int killGold = 0)
        {
            if (!Enum.IsDefined(typeof(UnitKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), "Unknown unit kind.");
            }

            if (maxHealth <= 0 || attack < 0 || defense < 0)
                throw new ArgumentException("Health must be positive; attack and defense must be nonnegative.");
            RequirePositive(speed, nameof(speed));
            RequirePositive(radius, nameof(radius));
            RequirePositive(visualHeight, nameof(visualHeight));
            RequirePositive(attackInterval, nameof(attackInterval));
            if (float.IsNaN(attackRange) || float.IsInfinity(attackRange) || attackRange < 0)
                throw new ArgumentOutOfRangeException(nameof(attackRange));
            if (killGold < 0) throw new ArgumentOutOfRangeException(nameof(killGold));
            KillGold = killGold;
            Id = id ?? kind.ToString();
            Skills = new System.Collections.Generic.List<HeroAreaSkillDefinition>(skills ?? Array.Empty<HeroAreaSkillDefinition>()).AsReadOnly();
            AttackInterval = attackInterval;
            AttackRange = attackRange;
            TargetMainCastle = targetMainCastle;
            CanAttackGates = canAttackGates;
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
