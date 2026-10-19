using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.ValueObjects;
using Game.Presentation.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Battle.Tests
{
    public sealed class UnitAnimationTests
    {
        private static UnitAnimationSettings AnimationSettings(string path)
        {
            var settings = BattlePresentationSettings.Parse(System.IO.File.ReadAllText("Assets/Content/Combat/Prototype/presentation.json"));
            if (path.Contains("Knight"))
                return settings.Knight;
            if (path.Contains("Goblin"))
                return settings.Goblin;
            return settings.Runner;
        }

        [TestCase("Enemies/Goblin/Sprites/goblin", UnitKind.Goblin)]
        [TestCase("Enemies/Runner/Sprites/runner", UnitKind.Runner)]
        public void MonsterPlaysDeathHoldsLastFrameAndOnlyThenBecomesRemovable(string path, UnitKind kind)
        {
            var set = AssetDatabase.LoadAssetAtPath<UnitAnimationSet>("Assets/Content/Units/" + path + "-actions.asset");
            Assert.That(set.Death.Length, Is.EqualTo(4));
            foreach (var frame in set.Death)
                Assert.That(frame, Is.Not.Null);
            var settings = new UnitDeathSettings();
            // 使用不同于默认值的时长，验证播放和移除确实读取统一配置。
            settings.AnimationSeconds = 1;
            settings.CorpseHoldSeconds = 2;
            var root = new GameObject("Death animation test");
            try
            {
                var sprite = set.Idle[0];
                var view = new UnitView(root.transform, sprite, sprite, sprite, 1.3f, 0.4f, Color.red, set, settings, AnimationSettings(path));
                var unit = root.transform.GetChild(0);
                var body = unit.Find("Sprite").GetComponent<SpriteRenderer>();
                var state = new UnitSnapshot(2, kind, 3, 4, -1, 0, 10, 10, 10);
                view.Render(state, 10, 0);
                Assert.That(unit.gameObject.activeSelf, Is.True);
                Assert.That(body.sprite, Is.EqualTo(set.Death[0]));
                Assert.That(body.flipX, Is.True);
                Assert.That(unit.Find("HealthBackground").gameObject.activeSelf, Is.False);
                Assert.That(unit.position, Is.EqualTo(new Vector3(3, 4, 0)));
                view.Render(state, 10.3, 0.3f);
                Assert.That(body.sprite, Is.EqualTo(set.Death[1]));
                view.Render(state, 10.3, 1); // 模拟暂停，即使渲染 dt 很大也不会提前播放/移除。
                Assert.That(body.sprite, Is.EqualTo(set.Death[1]));
                Assert.That(view.IsReadyForRemoval, Is.False);
                view.Render(state, 11.5, 1);
                Assert.That(body.sprite, Is.EqualTo(set.Death[3]));
                Assert.That(view.IsReadyForRemoval, Is.False);
                view.Render(state, 12.99, 1);
                Assert.That(view.IsReadyForRemoval, Is.False);
                view.Render(state, 13, 0.01f);
                Assert.That(view.IsReadyForRemoval, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DeathJsonHasValidDurations()
        {
            var settings = BattlePresentationSettings.Parse(System.IO.File.ReadAllText("Assets/Content/Combat/Prototype/presentation.json")).Death;
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.Duration, Is.GreaterThan(0));
            Assert.That(settings.Hold, Is.GreaterThanOrEqualTo(0));
        }

        [TestCase("Heroes/Knight/Sprites/knight")]
        [TestCase("Enemies/Goblin/Sprites/goblin")]
        [TestCase("Enemies/Runner/Sprites/runner")]
        public void ImportedFramesPlayAttackThenHurtAndReturnToLocomotion(string path)
        {
            var set = AssetDatabase.LoadAssetAtPath<UnitAnimationSet>("Assets/Content/Units/" + path + "-actions.asset");
            Assert.That(set, Is.Not.Null);
            foreach (var frames in new[]{set.Idle, set.Move, set.Attack, set.Hurt})
            {
                Assert.That(frames.Length, Is.EqualTo(4));
                foreach (var frame in frames)
                    Assert.That(frame, Is.Not.Null);
            }

            var root = new GameObject("Animation test");
            try
            {
                var sprite = set.Idle[0];
                var view = new UnitView(root.transform, sprite, sprite, sprite, 1.5f, 0.4f, Color.white, set, null, AnimationSettings(path));
                var body = root.transform.GetChild(0).Find("Sprite").GetComponent<SpriteRenderer>();
                UnitSnapshot State(float x = 0, float facing = 1, int hp = 10, float hit = -100, float attack = -100) => new UnitSnapshot(1, UnitKind.Hero, x, 0, facing, hp, 10, hit, attack);
                view.Render(State(), 0, 0);
                Assert.That(body.sprite, Is.EqualTo(set.Idle[0]));
                view.Render(State(x: 1, facing: -1), 0.1, 0.1f);
                Assert.That(body.sprite, Is.EqualTo(set.Move[0]));
                Assert.That(body.flipX, Is.True);
                view.Render(State(x: 1, hit: 1, attack: 1), 1.11, 0.1f);
                Assert.That(body.sprite, Is.EqualTo(set.Attack[1]));
                view.Render(State(x: 1, hit: 1, attack: 1), 1 + set.Attack.Length / (double)AnimationSettings(path).AttackFps + 0.08, 0.1f);
                Assert.That(body.sprite, Is.EqualTo(set.Hurt[1]));
                view.Render(State(x: 1, hit: 1, attack: 1), 2, 0.1f);
                Assert.That(set.Idle, Does.Contain(body.sprite));
                view.Render(State(x: 1, hp: 0), 3, 0.1f);
                Assert.That(root.transform.GetChild(0).gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
