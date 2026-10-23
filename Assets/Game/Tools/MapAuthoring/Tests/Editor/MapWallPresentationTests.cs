using Game.Maps.Domain.Mappings;
using Game.Fortifications.Domain.Definitions;
using Game.Fortifications.Unity.Views;
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
        /// <summary>开门、关门和破门只切换预生成门表现，静态地表、对象和 Mesh 身份不变。</summary>
        /// <summary>模拟试玩切换释放临时素材：旧快照必须失效，重建后保留所有建筑与城门。</summary>
        [Test]
        public void CanvasRecoversAfterCatalogReleasesTemporarySprites()
        {
            var map = new MapLayout();
            map.wallNodes.Add(new WallNode { id = "a", x = -8.5f, y = 2.5f });
            map.wallNodes.Add(new WallNode { id = "b", x = 9.5f, y = 2.5f });
            map.wallEdges.Add(new WallEdge { id = "wall", from = "a", to = "b",
                gates = new System.Collections.Generic.List<WallGate> { new WallGate { id = "door" } } });
            map.facilities.Add(new InitialFacility { id = "building", definitionId = "tower", x = 2, y = 2 });
            var source = AssetDatabase.LoadAssetAtPath<MapVisualCatalog>(Editor.MapEditorWindow.VisualPath);
            var visuals = Object.Instantiate(source);
            // 强制使用由目录拥有生命周期的临时金属 Sprite。
            visuals.gateMetal = null;
            var catalog = FacilityCatalogJson.Read(visuals.facilityRules.text);
            var canvas = new Editor.MapCanvas { Layout = map, Visuals = visuals, Catalog = catalog };
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var cache = typeof(Editor.MapCanvas).GetField("_sprites", flags);
            try
            {
                var entries = MapSceneSprites.Build(map, visuals, catalog);
                cache.SetValue(canvas, entries);
                Assert.That(entries.Any(entry => entry.Id == "door" && entry.Wall != null), Is.True);
                string before = MapJson.WriteDocument(canvas.Layout);
                typeof(MapVisualCatalog).GetMethod("OnDisable", flags).Invoke(visuals, null);
                Assert.That(entries.Any(entry => entry.Wall != null && entry.Wall.Mesh.Sprite == null), Is.True);
                typeof(Editor.MapCanvas).GetMethod("InvalidateReleasedResources", flags).Invoke(canvas, null);
                Assert.That(cache.GetValue(canvas), Is.Null);
                var refreshed = MapSceneSprites.Build(canvas.Layout, visuals, catalog);
                Assert.That(refreshed.All(entry => entry.Wall == null || entry.Wall.Mesh.Sprite != null), Is.True);
                Assert.That(refreshed.Any(entry => entry.Id == "building"), Is.True);
                Assert.That(refreshed.Any(entry => entry.Id == "door" && entry.Wall != null), Is.True);
                Assert.That(MapJson.WriteDocument(canvas.Layout), Is.EqualTo(before));
            }
            finally
            {
                Object.DestroyImmediate(visuals);
            }
        }

        [Test]
        public void GateStateChangesReuseAllMapObjectsAndMeshes()
        {
            var map = new MapLayout();
            map.wallNodes.Add(new WallNode { id = "a", x = -8.5f, y = 2.5f });
            map.wallNodes.Add(new WallNode { id = "b", x = 9.5f, y = 2.5f });
            var gate = new WallGate { id = "door" };
            map.wallEdges.Add(new WallEdge { id = "wall", from = "a", to = "b",
                gates = new System.Collections.Generic.List<WallGate> { gate } });
            var visuals = AssetDatabase.LoadAssetAtPath<MapVisualCatalog>(Editor.MapEditorWindow.VisualPath);
            var root = new GameObject("Gate reuse verification");
            try
            {
                var view = root.AddComponent<MapWorldView>();
                view.Rebuild(map, visuals, FacilityCatalogJson.Read(visuals.facilityRules.text));
                for (int state = 0; state < 3; state++)
                {
                    var group = root.transform.Find($"Map state view/Gate door state {state}");
                    var renderers = group.GetComponentsInChildren<MeshRenderer>(true);
                    Assert.That(renderers, Is.Not.Empty, $"城门状态 {state} 不能是空组");
                    Assert.That(group.GetComponentsInChildren<MeshFilter>(true).All(filter => filter.sharedMesh.vertexCount > 0), Is.True);
                }
                var objects = root.GetComponentsInChildren<Transform>(true);
                var meshes = root.GetComponentsInChildren<MeshFilter>(true).Select(item => item.sharedMesh).ToArray();
                Assert.That(root.transform.Find("Map state view/Gate door state 0").gameObject.activeSelf, Is.True);
                for (int i = 0; i < 12; i++)
                {
                    gate.isOpen = i % 2 == 0;
                    view.SynchronizeGates(map);
                    Assert.That(root.transform.Find("Map state view/Gate door state 1").gameObject.activeSelf, Is.EqualTo(gate.isOpen));
                }
                gate.isDestroyed = true;
                view.SynchronizeGates(map);
                Assert.That(root.transform.Find("Map state view/Gate door state 2").gameObject.activeSelf, Is.True);
                CollectionAssert.AreEqual(objects, root.GetComponentsInChildren<Transform>(true));
                CollectionAssert.AreEqual(meshes, root.GetComponentsInChildren<MeshFilter>(true).Select(item => item.sharedMesh).ToArray());
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WallCenterlineUsesExactNodesWithoutChangingTheSource()
        {
            var map = new MapLayout();
            map.wallNodes.Add(new WallNode { id = "a", x = -11.5f, y = 1.5f });
            map.wallNodes.Add(new WallNode { id = "b", x = 11.5f, y = 1.5f });
            var edge = new WallEdge { id = "ab", from = "a", to = "b" };
            map.wallEdges.Add(edge);
            string before = MapJson.WriteDocument(map);
            Assert.That(WallPresentation.TryGetSpan(MapWallSnapshotMapper.Read(map), MapWallSnapshotMapper.Read(map).FindEdge(edge.id), out var start, out var end), Is.True);
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
            WallPresentation.TryGetSpan(MapWallSnapshotMapper.Read(map), MapWallSnapshotMapper.Read(map).FindEdge(first.id), out var a, out var sharedA);
            WallPresentation.TryGetSpan(MapWallSnapshotMapper.Read(map), MapWallSnapshotMapper.Read(map).FindEdge(second.id), out var sharedB, out var c);
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
            WallPresentation.TryGetSpan(MapWallSnapshotMapper.Read(map), MapWallSnapshotMapper.Read(map).FindEdge(edge.id), out var start, out var end);
            Assert.That(Vector2.Distance(start, new Vector2(-2.5f, 0.5f)), Is.LessThan(0.0001f));
            Assert.That(Vector2.Distance(end, new Vector2(0.5f, 3.5f)), Is.LessThan(0.0001f));
            edge.from = "b";
            edge.to = "a";
            WallPresentation.TryGetSpan(MapWallSnapshotMapper.Read(map), MapWallSnapshotMapper.Read(map).FindEdge(edge.id), out var reversedStart, out var reversedEnd);
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
                Assert.That(root.GetComponentsInChildren<WallMeshView>().Any(piece => piece.name == "a"), Is.True);
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
