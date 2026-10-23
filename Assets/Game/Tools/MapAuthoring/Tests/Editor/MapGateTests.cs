using System;
using System.Collections.Generic;
using System.Linq;
using Game.Fortifications.Domain.Definitions;
using Game.Fortifications.Unity.Views;
using Game.Infrastructure.Maps.Serialization;
using Game.Maps.Domain;
using Game.Maps.Domain.Mappings;
using Game.Maps.Unity.Assets;
using Game.Maps.Unity.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tools.MapAuthoring.Tests
{
    public sealed class MapGateTests
    {
        private static MapLayout Map()
        {
            var map = new MapLayout();
            map.wallNodes.Add(new WallNode { id = "a", x = -8.5f, y = 1.5f });
            map.wallNodes.Add(new WallNode { id = "b", x = 9.5f, y = 1.5f });
            map.wallEdges.Add(new WallEdge { id = "wall", from = "a", to = "b", gates = new List<WallGate>
            {
                new WallGate { id = "gate-a", startTile = 3 },
                new WallGate { id = "gate-b", startTile = 10 }
            } });
            return map;
        }

        [Test]
        public void SaveAndHistory_IndependentDoors_PreserveEachIdAndState()
        {
            var map = Map();
            var reopened = MapJson.ReadDocument(MapJson.WriteDocument(map));
            Assert.That(reopened.wallEdges[0].gates.Select(gate => gate.id), Is.EqualTo(new[] { "gate-a", "gate-b" }));
            var session = new MapEditSession(map);
            var next = session.Document;
            next.wallEdges[0].gates[1].isOpen = true;
            session.Commit(next);
            session.Undo();
            Assert.That(session.Document.wallEdges[0].gates.All(gate => !gate.isOpen), Is.True);
            session.Redo();
            Assert.That(session.Document.wallEdges[0].gates[0].isOpen, Is.False);
            Assert.That(session.Document.wallEdges[0].gates[1].isOpen, Is.True);
            Assert.That(map.wallEdges[0].gates[1].isOpen, Is.False);
        }

        [Test]
        public void GateErrors_OverlapOrEndOverflow_LocateIndividualDoor()
        {
            var map = Map();
            var catalog = new MapContentCatalog(Array.Empty<FacilityDefinition>());
            map.wallEdges[0].gates[1].startTile = 4;
            Assert.That(MapContentValidator.Validate(map, catalog).Any(issue => issue.ObjectId == "gate-b" && issue.IsError), Is.True);
            map.wallEdges[0].gates[1].startTile = 16;
            Assert.That(MapContentValidator.Validate(map, catalog).Any(issue => issue.ObjectId == "gate-b"), Is.True);
        }

        [TestCase(0)]
        [TestCase(25)]
        [TestCase(70)]
        [TestCase(90)]
        [TestCase(135)]
        public void GateGeometry_AllWallDirections_HasValidMeshesAndSharedStone(float degrees)
        {
            var map = Map();
            var edge = map.wallEdges[0];
            edge.gates.RemoveAt(1);
            float angle = degrees * Mathf.Deg2Rad;
            map.wallNodes[0].x = -5 * Mathf.Cos(angle);
            map.wallNodes[0].y = -5 * Mathf.Sin(angle);
            map.wallNodes[1].x = 5 * Mathf.Cos(angle);
            map.wallNodes[1].y = 5 * Mathf.Sin(angle);
            Assert.That(WallGateRules.Validate(map, edge, out var reason), Is.True, reason);
            var visuals = AssetDatabase.LoadAssetAtPath<MapVisualCatalog>(Editor.MapEditorWindow.VisualPath);
            var network = MapWallSnapshotMapper.Read(map);
            var gate = network.Edges[0].Gates[0];
            string before = MapJson.WriteDocument(map);
            var entries = GatePresentation.Build(network, network.Edges[0], gate, WallDefinition.Default, visuals.WallStyle).ToArray();
            Assert.That(entries, Is.Not.Empty);
            Assert.That(entries.All(entry => entry.Id == "gate-a" && entry.Wall != null), Is.True);
            Assert.That(entries.Any(entry => entry.Wall.Mesh.Sprite == visuals.wallMasonry), Is.True);
            foreach (var entry in entries)
            {
                var mesh = entry.Wall.Mesh;
                Assert.That(mesh.Indices.All(index => index >= 0 && index < mesh.Positions.Length), Is.True);
                Assert.That(mesh.Positions.All(point => !float.IsNaN(point.x) && !float.IsNaN(point.y)), Is.True);
            }
            Assert.That(MapJson.WriteDocument(map), Is.EqualTo(before));
            var span = default(Game.Fortifications.Domain.ValueObjects.WallGateSpan);
            Assert.That(WallGateRules.TryGetSpan(map, edge, edge.gates[0], out span), Is.True);
            var center = span.Center;
            Assert.That(MapSceneSprites.PickGate(map, visuals, new Vector2(center.X, center.Y), new MapContentCatalog(Array.Empty<FacilityDefinition>())), Is.EqualTo("gate-a"));
        }

        [Test]
        public void LegacySingleGate_Import_ConvertsToIndependentIntegerTileDoor()
        {
            var map = Map();
            map.wallEdges[0].gates.Clear();
            string json = MapJson.WriteDocument(map).Replace("\"gates\": []", "\"hasGate\": true, \"gate\": {\"position\":0.5,\"width\":4.5,\"openingWidth\":2,\"isOpen\":true}");
            var reopened = MapJson.ReadDocument(json);
            var gate = reopened.wallEdges[0].gates.Single();
            Assert.That(gate.id, Is.EqualTo("wall/gate"));
            Assert.That(gate.widthTiles, Is.EqualTo(6));
            Assert.That(gate.openingTiles, Is.EqualTo(2));
            Assert.That(gate.isOpen, Is.True);
            Assert.That(MapJson.WriteDocument(reopened), Does.Not.Contain("hasGate"));
        }

        [Test]
        public void FractionalTileJson_Import_RejectsInsteadOfSilentlyTruncating()
        {
            var json = MapJson.WriteDocument(Map()).Replace("\"widthTiles\": 4", "\"widthTiles\": 4.5");
            Assert.Throws<FormatException>(() => MapJson.ReadDocument(json));
        }
    }
}
