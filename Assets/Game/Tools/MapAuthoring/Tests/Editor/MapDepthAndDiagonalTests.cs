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
            var walls = MapRaisedWallPresentation.Build(map, edge, visuals).ToArray();
            Assert.That(walls, Is.Not.Empty);
            foreach (var wall in walls)
            {
                var ground = wall.Wall.GroundOutline;
                Assert.That(Vector2.Distance(ground[0], ground[3]), Is.EqualTo(0.52f).Within(0.00001f));
                Assert.That(wall.Wall.Facades, Is.Not.Empty);
                foreach (var facade in wall.Wall.Facades)
                {
                    Assert.That(facade[3].x, Is.EqualTo(facade[0].x).Within(0.00001f), "Left vertical edge leaned at " + degrees);
                    Assert.That(facade[2].x, Is.EqualTo(facade[1].x).Within(0.00001f), "Right vertical edge leaned at " + degrees);
                    Assert.That(facade[3].y, Is.GreaterThan(facade[0].y));
                    Assert.That(facade[2].y - facade[1].y, Is.EqualTo(facade[3].y - facade[0].y).Within(0.00001f));
                }
            }
            edge.from = "b";
            edge.to = "a";
            var reversed = MapRaisedWallPresentation.Build(map, edge, visuals).ToArray();
            Assert.That(reversed.Select(Key), Is.EqualTo(walls.Select(Key)));
            for (int i = 0; i < walls.Length; i++)
                Assert.That(reversed[i].Wall.Mesh.Positions, Is.EqualTo(walls[i].Wall.Mesh.Positions));
            edge.from = "a";
            edge.to = "b";
            Assert.That(MapJson.WriteDocument(map), Is.EqualTo(original));
        }

        private static string Key(MapSceneSprites.Entry entry)
        {
            return entry.SortKey + ":" + entry.GroundX.ToString("R") + ":" + entry.GroundY.ToString("R");
        }
    }
}
