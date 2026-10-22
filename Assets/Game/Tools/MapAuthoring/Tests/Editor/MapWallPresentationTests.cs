using Game.Infrastructure.Maps.Serialization;
using Game.Maps.Unity.Views;
using Game.Maps.Unity.Assets;
using System.Linq;
using Game.Maps.Domain;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tools.MapAuthoring.Tests
{
    public sealed class MapWallPresentationTests
    {
        [Test]
        public void WallCenterlineUsesExactNodesWithoutChangingTheSource()
        {
            var map = new MapLayout();
            map.wallNodes.Add(new WallNode { id = "a", x = -11.5f, y = 1.5f });
            map.wallNodes.Add(new WallNode { id = "b", x = 11.5f, y = 1.5f });
            var edge = new WallEdge { id = "ab", from = "a", to = "b" };
            map.wallEdges.Add(edge);
            string before = MapJson.WriteDocument(map);
            Assert.That(MapWallPresentation.TryGetSpan(map, edge, out var start, out var end), Is.True);
            Assert.That(start.x, Is.EqualTo(-11.5f));
            Assert.That(end.x, Is.EqualTo(11.5f));
            Assert.That(MapJson.WriteDocument(map), Is.EqualTo(before));
        }

        [Test]
        public void ConnectedNodesAndFreeEndsKeepTheirGroundCoordinates()
        {
            var map = new MapLayout { cellWidth = 2, cellHeight = 1, originX = 10, originY = 20 };
            map.wallNodes.Add(new WallNode { id = "a", x = 11, y = 20.5f });
            map.wallNodes.Add(new WallNode { id = "b", x = 11, y = 23.5f });
            map.wallNodes.Add(new WallNode { id = "c", x = 15, y = 23.5f });
            var first = new WallEdge { id = "ab", from = "a", to = "b" };
            var second = new WallEdge { id = "bc", from = "b", to = "c" };
            map.wallEdges.Add(first);
            map.wallEdges.Add(second);
            MapWallPresentation.TryGetSpan(map, first, out var a, out var sharedA);
            MapWallPresentation.TryGetSpan(map, second, out var sharedB, out var c);
            Assert.That(a.y, Is.EqualTo(20.5f));
            Assert.That(sharedA, Is.EqualTo(new Vector2(11, 23.5f)));
            Assert.That(sharedB, Is.EqualTo(sharedA));
            Assert.That(c.x, Is.EqualTo(15));
        }

        [Test]
        public void DiagonalCenterlineKeepsTheAuthoredGroundEndpoints()
        {
            var map = new MapLayout();
            map.wallNodes.Add(new WallNode { id = "a", x = -2.5f, y = 0.5f });
            map.wallNodes.Add(new WallNode { id = "b", x = 0.5f, y = 3.5f });
            var edge = new WallEdge { id = "ab", from = "a", to = "b" };
            map.wallEdges.Add(edge);
            MapWallPresentation.TryGetSpan(map, edge, out var start, out var end);
            Assert.That(Vector2.Distance(start, new Vector2(-2.5f, 0.5f)), Is.LessThan(0.0001f));
            Assert.That(Vector2.Distance(end, new Vector2(0.5f, 3.5f)), Is.LessThan(0.0001f));
            edge.from = "b";
            edge.to = "a";
            MapWallPresentation.TryGetSpan(map, edge, out var reversedStart, out var reversedEnd);
            Assert.That(reversedStart, Is.EqualTo(end));
            Assert.That(reversedEnd, Is.EqualTo(start));
        }

        [Test]
        public void GameViewBuildsWallAndNodeMeshesWithinTheirBaseRadii()
        {
            var map = new MapLayout();
            map.wallNodes.Add(new WallNode { id = "a", x = -11.5f, y = 1.5f });
            map.wallNodes.Add(new WallNode { id = "b", x = 11.5f, y = 1.5f });
            map.wallEdges.Add(new WallEdge { id = "ab", from = "a", to = "b" });
            var visuals = AssetDatabase.LoadAssetAtPath<MapVisualCatalog>(Editor.MapEditorWindow.VisualPath);
            var root = new GameObject("Wall view verification");
            try
            {
                root.AddComponent<MapWorldView>().Rebuild(map, visuals, FacilityCatalogJson.Read(visuals.facilityRules.text));
                var pieces = root.GetComponentsInChildren<MeshRenderer>().Where(renderer => renderer.name == "ab").ToList();
                Assert.That(pieces, Is.Not.Empty);
                var wallAndPillars = root.GetComponentsInChildren<Renderer>().Where(renderer => renderer.name == "ab" || renderer.name == "a" || renderer.name == "b").ToArray();
                Assert.That(wallAndPillars.Min(piece => piece.bounds.min.x), Is.GreaterThanOrEqualTo(-12));
                Assert.That(wallAndPillars.Max(piece => piece.bounds.max.x), Is.LessThanOrEqualTo(12));
                Assert.That(root.GetComponentsInChildren<MapWallMeshView>().Any(piece => piece.name == "a"), Is.True);
                Assert.That(pieces.Any(piece => piece.sharedMaterial.mainTexture == visuals.wallMasonry.texture), Is.True);
                Assert.That(pieces.Any(piece => piece.sharedMaterial.mainTexture == visuals.wallWalkway.texture), Is.True);
                Assert.That(pieces.All(piece => piece.sharedMaterial.mainTexture == visuals.wallMasonry.texture ||
                                              piece.sharedMaterial.mainTexture == visuals.wallWalkway.texture), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
