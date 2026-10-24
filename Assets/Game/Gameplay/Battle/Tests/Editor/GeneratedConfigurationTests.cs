using System;
using System.IO;
using Game.Construction.Domain.Definitions;
using Game.Battle.Domain.Definitions;
using Game.Infrastructure.Configuration;
using NUnit.Framework;

namespace Game.Battle.Tests
{
    public sealed class GeneratedConfigurationTests
    {
        private const string Root = "Assets/Content/Configuration/Resources/GameConfig/";
        [TestCase("Contact", AttackMode.Contact)]
        [TestCase("Melee", AttackMode.Melee)]
        [TestCase("Direct", AttackMode.Direct)]
        public void AttackModesLoadAsDomainEnum(string name, AttackMode expected)
        {
            Assert.That(new AttackDefinition("test", name, 1, 1, 0, "").Mode, Is.EqualTo(expected));
        }

        [TestCase("contact")]
        [TestCase("Ranged")]
        [TestCase("0")]
        [TestCase("999")]
        public void UnknownOrNumericAttackModesAreRejected(string mode)
        {
            Assert.Throws<ArgumentException>(() => new AttackDefinition("test", mode, 1, 1, 0, ""));
        }
        [Test]
        public void GeneratedCombatAndConstructionContentLoadsIntoItsOwningDomains()
        {
            var combat = JsonCombatContentSource.Parse(File.ReadAllText(Root + "combat-content.json"));
            // 策划可调整塔伤害；集成测试检查能力身份及语义，不锁死平衡数值。
            Assert.That(combat.Attacks["attack.tower"].Mode, Is.EqualTo(AttackMode.Direct));
            Assert.That(combat.Skills["skill.alien_burst"].Enabled, Is.True);
            var battle = JsonBattleDefinitionSource.Parse(File.ReadAllText(Root + "battle.json"));
            Assert.That(battle.Hero.Skills.Count, Is.EqualTo(2));
            var skill = combat.Skills[battle.Hero.Skills[0].Id];
            Assert.That(battle.Hero.Skills[0].Radius, Is.EqualTo(combat.Attacks[skill.AttackId].Range));
            Assert.That(battle.Hero.Skills[0].Damage, Is.EqualTo(combat.Attacks[skill.AttackId].Damage));
            Assert.That(battle.Hero.Skills[0].Cooldown, Is.EqualTo(skill.Cooldown));
            Assert.That(combat.Effects["effect.slow"].Magnitude, Is.EqualTo(0.2f));
            var construction = JsonConstructionContentSource.Parse(File.ReadAllText(Root + "construction-content.json"));
            Assert.That(construction.Buildings["hall"].CanDemolish, Is.False);
            Assert.That(construction.Upgrades["upgrade.tower.rapid"].BuildingId, Is.EqualTo("tower"));
        }
        [Test]
        public void UnitRuntimeProjectionContainsStableIdentityAndBehaviorParameters()
        {
            var battle = JsonBattleDefinitionSource.Parse(File.ReadAllText(Root + "battle.json"));
            Assert.That(battle.Goblin.Id, Is.EqualTo("enemy.skeleton"));
            Assert.That(battle.Runner.Id, Is.EqualTo("enemy.fallen"));
            Assert.That(battle.Goblin.TargetMainCastle, Is.True);
            Assert.That(battle.Goblin.CanAttackGates, Is.True);
            Assert.That(battle.Goblin.AttackInterval, Is.EqualTo(1));
        }
        [Test]
        public void ConstructionDomainRejectsCyclicUpgradeDefinitions()
        {
            var building = new BuildingDevelopmentDefinition("tower", 50, 0, true, 0.5f);
            var a = new UpgradeBranchDefinition("a", "tower", "b", "", 1, 0, 0, 0, 0);
            var b = new UpgradeBranchDefinition("b", "tower", "a", "", 1, 0, 0, 0, 0);
            Assert.Throws<ArgumentException>(() => new ConstructionContentDefinition(new[] { building }, new[] { a, b }));
        }
    }
}
