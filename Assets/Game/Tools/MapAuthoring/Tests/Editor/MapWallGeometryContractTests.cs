using Game.Maps.Domain.Mappings;
using Game.Fortifications.Domain.Definitions;
using Game.Fortifications.Unity.Views;
using System.Linq;
using Game.Infrastructure.Maps.Serialization;
using Game.Maps.Domain;
using Game.Maps.Unity.Assets;
using Game.Maps.Unity.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tools.MapAuthoring.Tests
{
    /// <summary>只检查共享接口和渲染数据契约，不代替用户进行逐场景视觉或通行验收。</summary>
    public sealed class MapWallGeometryContractTests
    {
        [Test]
        public void NodeAndWallUseTheSameOpeningWidthAndCenter()
        {
            var visuals = AssetDatabase.LoadAssetAtPath<MapVisualCatalog>(Editor.MapEditorWindow.VisualPath);
            var map = new MapLayout();
            var a = new WallNode { id = "a", x = 0, y = 0 };
            var b = new WallNode { id = "b", x = 4, y = 0 };
            var c = new WallNode { id = "c", x = 4, y = 4 };
            map.wallNodes.AddRange(new[] { a, b, c });
            map.wallEdges.Add(new WallEdge { id = "ab", from = "a", to = "b" });
            map.wallEdges.Add(new WallEdge { id = "bc", from = "b", to = "c" });
            var geometry = new WallRenderGeometry(WallDefinition.Default, visuals.WallStyle);
            float radius = geometry.RadiusAt(MapWallSnapshotMapper.Read(map), MapWallSnapshotMapper.Read(map).FindNode(b.id));
            var center = new Vector2(b.x, b.y);
            var directions = geometry.DirectionsAt(MapWallSnapshotMapper.Read(map), MapWallSnapshotMapper.Read(map).FindNode(b.id));
            var outline = geometry.NodeOutline(center, radius, directions);
            foreach (var direction in directions)
            {
                Vector2 normal = new Vector2(-direction.y, direction.x);
                Vector2 port = center + direction * geometry.PortDistance(radius);
                Assert.That(outline.Any(point => Vector2.Distance(point, port + normal * geometry.Width * 0.5f) < 0.0001f), Is.True);
                Assert.That(outline.Any(point => Vector2.Distance(point, port - normal * geometry.Width * 0.5f) < 0.0001f), Is.True);
            }
            Assert.That(outline.All(point => Vector2.Distance(point, center) <= radius + 0.0001f), Is.True);
        }

        [Test]
        public void SeparateDeckMaterialProducesValidMeshesWithoutWritingMapData()
        {
            var originalVisuals = AssetDatabase.LoadAssetAtPath<MapVisualCatalog>(Editor.MapEditorWindow.VisualPath);
            var visuals = Object.Instantiate(originalVisuals);
            try
            {
                visuals.wallWalkway = visuals.Terrain(Game.MapEngine.Domain.TerrainKind.Dirt).ground;
                var map = new MapLayout();
                map.wallNodes.Add(new WallNode { id = "a", x = -3, y = 1 });
                map.wallNodes.Add(new WallNode { id = "b", x = 3, y = 1 });
                var edge = new WallEdge { id = "ab", from = "a", to = "b" };
                map.wallEdges.Add(edge);
                string before = MapJson.WriteDocument(map);
                var fragments = RaisedWallPresentation.Build(MapWallSnapshotMapper.Read(map), MapWallSnapshotMapper.Read(map).FindEdge(edge.id), WallDefinition.Default, visuals.WallStyle)
                    .Concat(WallJunctionPresentation.Build(MapWallSnapshotMapper.Read(map), MapWallSnapshotMapper.Read(map).Nodes[0], WallDefinition.Default, visuals.WallStyle)).ToArray();
                Assert.That(fragments.Any(item => item.Wall.Mesh.Sprite == visuals.wallWalkway), Is.True);
                Assert.That(fragments.Any(item => item.Wall.Mesh.Sprite == visuals.wallMasonry), Is.True);
                foreach (var fragment in fragments)
                {
                    var mesh = fragment.Wall.Mesh;
                    Assert.That(mesh.Indices.All(index => index >= 0 && index < mesh.Positions.Length), Is.True);
                    Assert.That(mesh.Positions.All(point => !float.IsNaN(point.x) && !float.IsInfinity(point.x) && !float.IsNaN(point.y) && !float.IsInfinity(point.y)), Is.True);
                    Assert.That(mesh.Uvs.All(uv => uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1), Is.True);
                }
                Assert.That(MapJson.WriteDocument(map), Is.EqualTo(before));
            }
            finally
            {
                Object.DestroyImmediate(visuals);
            }
        }
    }
}
