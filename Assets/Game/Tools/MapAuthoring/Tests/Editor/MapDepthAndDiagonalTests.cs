using Game.Maps.Domain.Mappings;
using Game.Fortifications.Domain.Definitions;
using Game.Fortifications.Unity.Views;
using Game.MapEngine.Unity.Views;
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
    public sealed class MapDepthAndDiagonalTests
    {
        private static MapVisualCatalog Visuals => AssetDatabase.LoadAssetAtPath<MapVisualCatalog>(Editor.MapEditorWindow.VisualPath);

        [Test]
        public void InsertingBuildingOrReorderingSourceDoesNotReorderExistingObjects()
        {
            var visuals = Visuals;
            var map = Editor.MapArtShowcase.Create(FacilityCatalogJson.Read(visuals.facilityRules.text));
            var original = MapSceneSprites.Build(map, visuals, FacilityCatalogJson.Read(visuals.facilityRules.text)).ToArray();
            map.facilities.Add(new InitialFacility { id = "extra", definitionId = "tower", x = 12, y = 24 });
            map.facilities.Reverse();
            map.wallEdges.Reverse();
            map.wallNodes.Reverse();
            var after = MapSceneSprites.Build(map, visuals, FacilityCatalogJson.Read(visuals.facilityRules.text)).Where(e => e.Id != "extra").ToArray();
            Assert.That(after.Select(Key), Is.EqualTo(original.Select(Key)));
        }

        [TestCase(0)]
        [TestCase(15)]
        [TestCase(30)]
        [TestCase(45)]
        [TestCase(60)]
        [TestCase(75)]
        [TestCase(85)]
        [TestCase(89)]
        [TestCase(90)]
        [TestCase(91)]
        [TestCase(95)]
        [TestCase(105)]
        [TestCase(120)]
        [TestCase(135)]
        [TestCase(150)]
        [TestCase(165)]
        public void EveryWallAngleKeepsThicknessAndUprightEdges(float degrees)
        {
            var visuals = Visuals;
            var map = new MapLayout();
            Vector2 delta = new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad)) * 5;
            map.wallNodes.Add(new WallNode { id = "a", x = -2.5f, y = -2.5f });
            map.wallNodes.Add(new WallNode { id = "b", x = -2.5f + delta.x, y = -2.5f + delta.y });
            var edge = new WallEdge { id = "ab", from = "a", to = "b" };
            map.wallEdges.Add(edge);
            string original = MapJson.WriteDocument(map);
            var walls = RaisedWallPresentation.Build(MapWallSnapshotMapper.Read(map), MapWallSnapshotMapper.Read(map).FindEdge(edge.id), WallDefinition.Default, visuals.WallStyle).ToArray();
            Assert.That(walls, Is.Not.Empty);
            foreach (var wall in walls)
            {
                var ground = wall.Wall.GroundOutline;
                Assert.That(Vector2.Distance(ground[0], ground[3]), Is.EqualTo(0.52f).Within(0.00001f));
                // 表面现在独立裁切，单个片段可能只有顶面；不能要求每个片段同时含完整立面。
                Assert.That(wall.Wall.Mesh.Positions.Length, Is.GreaterThanOrEqualTo(3));
                Assert.That(wall.Wall.Mesh.Uvs.Length, Is.EqualTo(wall.Wall.Mesh.Positions.Length));
            }
            Assert.That(RaisedWallPresentation.Project(Vector2.one, 0.5f), Is.EqualTo(new Vector2(1, 1.5f)));
            edge.from = "b";
            edge.to = "a";
            var reversed = RaisedWallPresentation.Build(MapWallSnapshotMapper.Read(map), MapWallSnapshotMapper.Read(map).FindEdge(edge.id), WallDefinition.Default, visuals.WallStyle).ToArray();
            Assert.That(reversed.Select(Key), Is.EqualTo(walls.Select(Key)));
            for (int i = 0; i < walls.Length; i++)
                Assert.That(reversed[i].Wall.Mesh.Positions, Is.EqualTo(walls[i].Wall.Mesh.Positions));
            edge.from = "a";
            edge.to = "b";
            Assert.That(MapJson.WriteDocument(map), Is.EqualTo(original));
        }

        private static string Key(SceneRenderEntry entry)
        {
            return entry.SortKey + ":" + entry.GroundX.ToString("R") + ":" + entry.GroundY.ToString("R");
        }
    }
}
