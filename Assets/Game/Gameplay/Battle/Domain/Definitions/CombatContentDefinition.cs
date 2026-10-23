using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Game.Battle.Domain.Definitions
{
    public sealed class CombatContentDefinition
    {
        public IReadOnlyDictionary<string, AttackDefinition> Attacks { get; }
        public IReadOnlyDictionary<string, SkillDefinition> Skills { get; }
        public IReadOnlyDictionary<string, EffectDefinition> Effects { get; }
        public IReadOnlyDictionary<string, EnemyBehaviorDefinition> Behaviors { get; }
        public CombatContentDefinition(IEnumerable<AttackDefinition> attacks, IEnumerable<SkillDefinition> skills,
            IEnumerable<EffectDefinition> effects, IEnumerable<EnemyBehaviorDefinition> behaviors)
        {
            Attacks = Index(attacks, a => a.Id);
            Skills = Index(skills, s => s.Id);
            Effects = Index(effects, e => e.Id);
            Behaviors = Index(behaviors, b => b.Id);
            foreach (var attack in Attacks.Values) ValidateEffects(attack.EffectIds);
            foreach (var skill in Skills.Values)
            {
                if (!Attacks.ContainsKey(skill.AttackId)) throw new ArgumentException("Unknown skill attack: " + skill.Id);
                ValidateEffects(skill.EffectIds);
            }
        }
        private void ValidateEffects(string effectIds)
        {
            foreach (var id in (effectIds ?? "").Split(';'))
                if (!string.IsNullOrWhiteSpace(id) && !Effects.ContainsKey(id.Trim()))
                    throw new ArgumentException("Unknown effect: " + id);
        }
        private static IReadOnlyDictionary<string, T> Index<T>(IEnumerable<T> values, Func<T, string> key)
        {
            var result = new Dictionary<string, T>();
            foreach (var value in values) result.Add(key(value), value);
            return new ReadOnlyDictionary<string, T>(result);
        }
    }
}
