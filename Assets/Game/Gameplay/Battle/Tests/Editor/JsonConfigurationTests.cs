using System;
using System.IO;
using Game.Infrastructure.Configuration;
using Game.Presentation.Views;
using NUnit.Framework;

namespace Game.Battle.Tests
{
    public sealed class JsonConfigurationTests
    {
        private const string PresentationPath = "Assets/Content/Combat/Prototype/presentation.json";
        private const string BattlePath = "Assets/Content/Combat/Prototype/battle.json";
        [Test]
        public void DeathAndAnimationValuesAreReloadedFromJson()
        {
            var configured = BattlePresentationSettings.Parse(File.ReadAllText(PresentationPath));
            Assert.That(configured.Death.AnimationSeconds, Is.EqualTo(0.6f));
            Assert.That(configured.Death.CorpseHoldSeconds, Is.EqualTo(1.5f));
            string changed = UnityEngine.JsonUtility.ToJson(configured);
            changed = changed.Replace("\"AnimationSeconds\":0.6", "\"AnimationSeconds\":1.2").Replace("\"AttackFps\":10.0", "\"AttackFps\":20.0");
            var reloaded = BattlePresentationSettings.Parse(changed);
            Assert.That(reloaded.Death.AnimationSeconds, Is.EqualTo(1.2f));
            Assert.That(reloaded.Knight.AttackFps, Is.EqualTo(20));
            Assert.That(configured.Knight.AttackFps, Is.EqualTo(10));
        }

        [TestCase("AnimationSeconds", -1)]
        [TestCase("CorpseHoldSeconds", -1)]
        [TestCase("AttackFps", 0)]
        [TestCase("BodyHeightFraction", 2)]
        public void InvalidPresentationValuesAreRejected(string field, float value)
        {
            string json = File.ReadAllText(PresentationPath);
            json = System.Text.RegularExpressions.Regex.Replace(json, "(\"" + field + "\"\\s*:\\s*)[0-9.]+", match => match.Groups[1].Value + value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Assert.Throws<FormatException>(() => BattlePresentationSettings.Parse(json));
        }

        [Test]
        public void ChaseRadiiAreReadIndependentlyAndInvalidOrderingIsRejected()
        {
            string json = File.ReadAllText(BattlePath);
            var configured = JsonBattleDefinitionSource.Parse(json);
            Assert.That(configured.Navigation.DetectionRadius, Is.EqualTo(5));
            Assert.That(configured.Navigation.LoseTargetRadius, Is.EqualTo(5));
            var changed = JsonBattleDefinitionSource.Parse(json.Replace("\"loseTargetRadius\": 5.0", "\"loseTargetRadius\": 7.0"));
            Assert.That(changed.Navigation.DetectionRadius, Is.EqualTo(5));
            Assert.That(changed.Navigation.LoseTargetRadius, Is.EqualTo(7));
            Assert.Throws<ArgumentException>(() => JsonBattleDefinitionSource.Parse(json.Replace("\"loseTargetRadius\": 5.0", "\"loseTargetRadius\": 4.0")));
            Assert.Throws<ArgumentOutOfRangeException>(() => JsonBattleDefinitionSource.Parse(json.Replace("\"loseTargetRadius\": 5.0,", "")));
        }
    }
}
