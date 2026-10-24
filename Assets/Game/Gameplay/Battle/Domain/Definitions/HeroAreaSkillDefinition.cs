using System;

namespace Game.Battle.Domain.Definitions
{
    /// <summary>由技能和攻击表投影的瞬时范围扣血能力。Cooldown 使用配置既有的秒单位。</summary>
    public sealed class HeroAreaSkillDefinition
    {
        public string Id { get; }
        public bool Enabled { get; }
        public float Radius { get; }
        public int Damage { get; }
        public float Cooldown { get; }
        public HeroAreaSkillDefinition(string id, bool enabled, float radius, int damage, float cooldown)
        {
            if (string.IsNullOrWhiteSpace(id) || float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0 ||
                damage < 0 || float.IsNaN(cooldown) || float.IsInfinity(cooldown) || cooldown <= 0)
                throw new ArgumentException("Invalid hero area skill.");
            Id = id;
            Enabled = enabled;
            Radius = radius;
            Damage = damage;
            Cooldown = cooldown;
        }
    }
}
