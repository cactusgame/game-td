using System;
using System.IO;
using Game.Infrastructure.Configuration;
using Game.Presentation.Views;
using NUnit.Framework;

namespace Game.Battle.Tests
{
    public sealed class JsonConfigurationTests
    {
        private const string PresentationPath = "Assets/Content/Configuration/Resources/GameConfig/presentation.json";
        private const string BattlePath = "Assets/Content/Configuration/Resources/GameConfig/battle.json";
        [Test]
        public void ProductCanReloadResourceSelectionWithoutAnyAnimationAuthoringParameters()
        {
            var configured = BattlePresentationSettings.Parse(File.ReadAllText(PresentationPath));
            Assert.That(configured.Death.CorpseHoldSeconds, Is.EqualTo(1.5f));
            string changed = UnityEngine.JsonUtility.ToJson(configured)
                .Replace("alien-actions", "knight-actions").Replace("1.5", "2.5");
            var reloaded = BattlePresentationSettings.Parse(changed);
            Assert.That(reloaded.Death.CorpseHoldSeconds, Is.EqualTo(2.5f));
            Assert.That(reloaded.AnimationFor(Game.Battle.Domain.ValueObjects.UnitKind.Hero).ResourceId, Is.EqualTo("knight-actions"));
            Assert.That(configured.AnimationFor(Game.Battle.Domain.ValueObjects.UnitKind.Hero).ResourceId, Is.EqualTo("alien-actions"));
            string binding = UnityEngine.JsonUtility.ToJson(configured.Units[0]);
            Assert.That(binding, Is.EqualTo("{\"UnitKind\":\"Hero\",\"ResourceId\":\"alien-actions\"}"));
        }

        [TestCase("CorpseHoldSeconds", -1)]
        [TestCase("VisibleWorldHeight", 0)]
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
