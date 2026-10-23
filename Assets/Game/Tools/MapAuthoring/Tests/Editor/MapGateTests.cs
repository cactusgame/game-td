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

namespace Game.Tools.MapAuthoring.Tests
{
    public sealed class MapGateTests
    {
        private static MapLayout Map()
        {
            var map = new MapLayout();
            map.wallNodes.Add(new WallNode { id = "a", x = -8.5f, y = 1.5f });
            map.wallNodes.Add(new WallNode { id = "b", x = 9.5f, y = 1.5f });
            map.wallEdges.Add(new WallEdge { id = "wall", from = "a", to = "b", hasGate = true });
            return map;
        }

        [Test]
        public void SaveAndHistoryKeepIndependentGateValuesAndLegacyWallsStayWalls()
        {
            var map = Map();
            var reopened = MapJson.ReadDocument(MapJson.WriteDocument(map));
            Assert.That(reopened.wallEdges[0].hasGate, Is.True);
            Assert.That(reopened.wallEdges[0].gate.openingWidth, Is.EqualTo(2));
            var session = new MapEditSession(map);
            var next = session.Document;
            next.wallEdges[0].gate.isOpen = true;
            session.Commit(next);
            session.Undo();
            Assert.That(session.Document.wallEdges[0].gate.isOpen, Is.False);
            session.Redo();
            Assert.That(session.Document.wallEdges[0].gate.isOpen, Is.True);
            Assert.That(map.wallEdges[0].gate.isOpen, Is.False);
            map.wallEdges[0].hasGate = false;
            Assert.That(MapJson.ReadDocument(MapJson.WriteDocument(map)).wallEdges[0].hasGate, Is.False);
        }

        [Test]
        public void GateErrorsIdentifyTheWallAndInvalidDirectionOrPositionIsRejected()
        {
            var map = Map();
            var catalog = new MapContentCatalog(new FacilityDefinition[0]);
            map.wallEdges[0].gate.position = 0;
            Assert.That(MapContentValidator.Validate(map, catalog).Any(issue => issue.ObjectId == "wall" && issue.IsError), Is.True);
            map.wallEdges[0].gate.position = 0.5f;
            map.wallNodes[1].y += 2;
            Assert.That(WallGateRules.Validate(map, map.wallEdges[0], out _), Is.False);
            map.wallNodes[1].y -= 2;
            Assert.That(WallGateRules.Validate(map, map.wallEdges[0], out _), Is.True);
        }

        [Test]
        public void GateUsesNewSpritesAndWallMeshesDoNotCoverThePortal()
        {
            var map = Map();
            var visuals = AssetDatabase.LoadAssetAtPath<MapVisualCatalog>(Editor.MapEditorWindow.VisualPath);
            var catalog = new MapContentCatalog(new FacilityDefinition[0]);
            var edge = map.wallEdges[0];
            var closed = MapSceneSprites.Build(map, visuals, catalog).Single(entry => entry.SortKey == "gate/wall");
            Assert.That(closed.Sprite, Is.EqualTo(visuals.gateClosed).And.Not.Null);
            edge.gate.isOpen = true;
            var open = MapSceneSprites.Build(map, visuals, catalog).Single(entry => entry.SortKey == "gate/wall");
            Assert.That(open.Sprite, Is.EqualTo(visuals.gateOpen).And.Not.Null);
            Assert.That(open.Center, Is.EqualTo(closed.Center));
            Assert.That(open.Width, Is.EqualTo(closed.Width));
            float centerX = WallGateRules.Center(map, edge).X;
            foreach (var entry in RaisedWallPresentation.Build(MapWallSnapshotMapper.Read(map), MapWallSnapshotMapper.Read(map).FindEdge(edge.id), WallDefinition.Default, visuals.WallStyle))
                Assert.That(entry.Wall.GroundOutline.All(point => point.x <= centerX - edge.gate.openingWidth * 0.5f + 0.001f) ||
                            entry.Wall.GroundOutline.All(point => point.x >= centerX + edge.gate.openingWidth * 0.5f - 0.001f), Is.True);
        }
    }
}
