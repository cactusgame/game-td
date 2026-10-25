using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.ValueObjects;
using Game.Presentation.Views;
using NUnit.Framework;
using UnityEngine;

namespace Game.Battle.Tests
{
    public sealed class HeroSkillCooldownViewTests
    {
        [Test]
        public void HeroBars_ReflectProgressHideIndependentlyAndHideForGhost()
        {
            var root = new GameObject("Hero cooldown presentation test");
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
            try
            {
                var view = new UnitView(root.transform, sprite, sprite, sprite, 1, 0.3f, Color.white);
                UnitSnapshot State(double first, double second, bool ghost = false) =>
                    new UnitSnapshot(1, UnitKind.Hero, 2, 3, 1, ghost ? 0 : 100, 100, -100,
                        isGhost: ghost, skillOneCooldown: new SkillCooldownSnapshot(first, 4), skillTwoCooldown: new SkillCooldownSnapshot(second, 8));
                view.Render(State(3, 4), 1, 0);
                var unit = root.transform.GetChild(0);
                var first = unit.Find("Skill one cooldown");
                var second = unit.Find("Skill two cooldown");
                var health = unit.Find("HealthBackground");
                Assert.That(first.gameObject.activeSelf, Is.True);
                Assert.That(second.gameObject.activeSelf, Is.True);
                Assert.That(first.Find("Fill").localScale.x, Is.EqualTo(0.92f * 0.25f).Within(0.001));
                Assert.That(second.Find("Fill").localScale.x, Is.EqualTo(0.92f * 0.5f).Within(0.001));
                Assert.That(first.localPosition.y - first.localScale.y * 0.5f,
                    Is.EqualTo(health.localPosition.y + health.localScale.y * 0.5f).Within(0.0001));
                Assert.That(second.localPosition.y - second.localScale.y * 0.5f,
                    Is.EqualTo(first.localPosition.y + first.localScale.y * 0.5f).Within(0.0001));
                float firstSlotY = first.localPosition.y;
                view.Render(State(0, 1), 4, 0);
                Assert.That(first.gameObject.activeSelf, Is.False);
                Assert.That(second.gameObject.activeSelf, Is.True);
                Assert.That(second.localPosition.y, Is.EqualTo(firstSlotY).Within(0.0001), "隐藏技能1后不保留空槽");
                view.ShowHealthBar = false;
                view.Render(State(3, 4), 4, 0);
                Assert.That(health.gameObject.activeSelf, Is.False);
                Assert.That(first.localPosition.y, Is.EqualTo(health.localPosition.y).Within(0.0001));
                Assert.That(second.localPosition.y - first.localPosition.y, Is.EqualTo(first.localScale.y).Within(0.0001));
                view.Render(State(0, 1, true), 4, 0);
                Assert.That(second.gameObject.activeSelf, Is.False);
                Assert.That(health.gameObject.activeSelf, Is.False, "复活不应覆盖血条显隐设置");
                view.ShowHealthBar = true;
                view.Render(State(3, 4), 9, 0);
                Assert.That(health.gameObject.activeSelf, Is.True);
                Assert.That(first.localPosition.y, Is.EqualTo(firstSlotY).Within(0.0001));
                view.Render(State(0, 0), 8, 0);
                Assert.That(first.gameObject.activeSelf, Is.False);
                Assert.That(second.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(sprite);
            }
        }
    }
}
