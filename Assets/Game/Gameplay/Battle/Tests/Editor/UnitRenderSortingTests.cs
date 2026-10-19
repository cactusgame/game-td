using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.ValueObjects;
using Game.Presentation.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Battle.Tests
{
    public sealed class UnitRenderSortingTests
    {
        [TestCase("Heroes/Knight/Sprites/knight", UnitKind.Hero)]
        [TestCase("Enemies/Goblin/Sprites/goblin", UnitKind.Goblin)]
        [TestCase("Enemies/Runner/Sprites/runner", UnitKind.Runner)]
        public void UnitsAndHealthStayAboveGroundAcrossTheWholeMap(string path, UnitKind kind)
        {
            var animations = AssetDatabase.LoadAssetAtPath<UnitAnimationSet>("Assets/Content/Units/" + path + "-actions.asset");
            var settings = BattlePresentationSettings.Parse(System.IO.File.ReadAllText("Assets/Content/Combat/Prototype/presentation.json")).Death;
            var root = new GameObject("Sorting regression test");
            var ground = root.AddComponent<MeshRenderer>();
            ground.sortingLayerName = BattleRenderSorting.GroundLayer;
            ground.sortingOrder = 0;
            try
            {
                var sprite = animations.Idle[0];
                var view = new UnitView(root.transform, sprite, sprite, sprite, 1.65f, 0.4f, Color.white, animations, settings, BattlePresentationSettings.Parse(System.IO.File.ReadAllText("Assets/Content/Combat/Prototype/presentation.json")).AnimationFor(kind));
                var unit = root.transform.GetChild(0);
                var body = unit.Find("Sprite").GetComponent<SpriteRenderer>();
                var shadow = unit.Find("GroundShadow").GetComponent<SpriteRenderer>();
                var background = unit.Find("HealthBackground").GetComponent<SpriteRenderer>();
                var health = background.transform.GetChild(0).GetComponent<SpriteRenderer>();
                int previous = int.MinValue;
                // 原缺陷在 Y>22 时出现；覆盖上沿、中部、下沿和比当前地图更远的位置。
                foreach (float y in new[]{1000f, 26.58f, 24f, 0f, -26.58f, -1000f})
                {
                    view.Render(new UnitSnapshot(1, kind, 0, y, 1, 10, 10, -100), 1, 0);
                    AssertDrawnAfter(shadow, ground);
                    AssertDrawnAfter(body, shadow);
                    AssertDrawnAfter(background, body);
                    AssertDrawnAfter(health, background);
                    Assert.That(body.sortingOrder, Is.GreaterThanOrEqualTo(previous));
                    previous = body.sortingOrder;
                }

                if (kind != UnitKind.Hero)
                {
                    view.Render(new UnitSnapshot(1, kind, 0, 26.58f, 1, 0, 10, 2), 2, 0);
                    Assert.That(animations.Death, Does.Contain(body.sprite));
                    AssertDrawnAfter(body, ground);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(100000)]
        [TestCase(-100000)]
        public void DepthOrderAndHealthOffsetsRemainInsideRendererRange(float y)
        {
            int order = BattleRenderSorting.UnitDepth(y);
            Assert.That(order, Is.InRange(short.MinValue, short.MaxValue - 2));
            Assert.That(order + 2, Is.LessThanOrEqualTo(short.MaxValue));
        }

        [Test]
        public void UpperEdgeCameraLeavesRoomForHeroAndHealthBar()
        {
            var settings = BattlePresentationSettings.Parse(System.IO.File.ReadAllText("Assets/Content/Combat/Prototype/presentation.json")).Map;
            float heroFeet = 27 - 0.42f;
            var center = BattleCameraFraming.Center(new Vector2(0, heroFeet), 96, 54, settings.VisibleWorldHeight, 16f / 9, settings.EdgeMargin, settings.TopEdgeMargin);
            float viewportTop = center.y + settings.VisibleWorldHeight * 0.5f;
            Assert.That(viewportTop, Is.GreaterThan(heroFeet + 1.65f + 0.2f));
            Assert.That(center.y, Is.EqualTo(20).Within(0.0001));
        }

        private static void AssertDrawnAfter(Renderer foreground, Renderer background)
        {
            int frontLayer = SortingLayer.GetLayerValueFromID(foreground.sortingLayerID);
            int backLayer = SortingLayer.GetLayerValueFromID(background.sortingLayerID);
            Assert.That(frontLayer > backLayer || (frontLayer == backLayer && foreground.sortingOrder > background.sortingOrder), Is.True, foreground.name + " must be drawn after " + background.name);
        }
    }
}
