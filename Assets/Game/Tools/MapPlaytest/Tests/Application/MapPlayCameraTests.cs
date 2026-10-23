using Game.Tools.MapPlaytest.Unity.Views;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tools.MapPlaytest.Tests
{
    public sealed class MapPlayCameraTests
    {
        [Test]
        public void LargeMapFollowsHeroAndSmallMapKeepsTheSameZoom()
        {
            var go = new GameObject("Camera test");
            var json = new TextAsset("{\"schemaVersion\":1,\"visibleHeight\":10,\"edgeMargin\":0.5,\"topEdgeMargin\":2}");
            try
            {
                var camera = go.AddComponent<Camera>();
                var view = new MapPlayCamera(camera, json);
                view.SetMap(new Rect(100, 200, 100, 80));
                view.Follow(new Vector2(130, 230), 0.2f);
                Assert.That(camera.transform.position.x, Is.EqualTo(130).Within(0.001));
                Assert.That(camera.transform.position.y, Is.EqualTo(230).Within(0.001));
                Assert.That(camera.orthographicSize, Is.EqualTo(5));
                Assert.That(camera.rect.height, Is.EqualTo(0.8f).Within(0.001));
                view.SetMap(new Rect(100, 200, 2, 2));
                view.Follow(new Vector2(102, 202), 0.2f);
                Assert.That(camera.transform.position.x, Is.EqualTo(101).Within(0.001));
                Assert.That(camera.transform.position.y, Is.EqualTo(201).Within(0.001));
                Assert.That(camera.orthographicSize, Is.EqualTo(5));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(json);
            }
        }

        [TestCase(0.5625f)]
        [TestCase(1.777778f)]
        [TestCase(3.555556f)]
        public void MapEdgesClampViewportWithNonZeroOrigin(float aspect)
        {
            var bounds = new Rect(100, 200, 100, 80);
            var center = Game.Presentation.Views.BattleCameraFraming.CenterOnMap(new Vector2(999, 999), bounds, 10, aspect, 0.5f, 2);
            Assert.That(center.x + 5 * aspect, Is.EqualTo(200.5f).Within(0.001));
            Assert.That(center.y + 5, Is.EqualTo(282).Within(0.001));
            center = Game.Presentation.Views.BattleCameraFraming.CenterOnMap(Vector2.zero, bounds, 10, aspect, 0.5f, 2);
            Assert.That(center.x - 5 * aspect, Is.EqualTo(99.5f).Within(0.001));
            Assert.That(center.y - 5, Is.EqualTo(199.5f).Within(0.001));
        }
    }
}
