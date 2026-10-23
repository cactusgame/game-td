using System;
using System.Linq;
using Game.Battle.Domain.Definitions;
using UnityEngine;

namespace Game.Infrastructure.Configuration
{
    public static class JsonCombatContentSource
    {
        public static CombatContentDefinition Parse(string json)
        {
            var data = JsonUtility.FromJson<Document>(json);
            if (data == null || data.schemaVersion != 1 || data.attacks == null || data.skills == null || data.effects == null || data.behaviors == null)
                throw new FormatException("Invalid combat content JSON.");
            return new CombatContentDefinition(
                data.attacks.Select(a => new AttackDefinition(a.id, a.mode, a.damage, a.interval, a.range, a.effectIds)),
                data.skills.Select(s => new SkillDefinition(s.id, s.enabled, s.cooldown, s.attackId, s.effectIds)),
                data.effects.Select(e => new EffectDefinition(e.id, e.kind, e.duration, e.magnitude, e.maxStacks)),
                data.behaviors.Select(b => new EnemyBehaviorDefinition(b.id, b.target, b.canAttackGates)));
        }
        [Serializable] private sealed class Document
        {
            public int schemaVersion;
            public Attack[] attacks;
            public Skill[] skills;
            public Effect[] effects;
            public Behavior[] behaviors;
        }
        [Serializable] private sealed class Attack
        {
            public string id, mode, effectIds;
            public int damage;
            public float interval, range;
        }
        [Serializable] private sealed class Skill
        {
            public string id, attackId, effectIds;
            public bool enabled;
            public float cooldown;
        }
        [Serializable] private sealed class Effect
        {
            public string id, kind;
            public float duration, magnitude;
            public int maxStacks;
        }
        [Serializable] private sealed class Behavior
        {
            public string id, target;
            public bool canAttackGates;
        }
    }
}
