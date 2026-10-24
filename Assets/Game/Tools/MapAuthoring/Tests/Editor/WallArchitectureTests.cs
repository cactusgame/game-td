using System;
using System.Linq;
using Game.Construction.Application.Sessions;
using Game.Fortifications.Domain.Definitions;
using Game.Fortifications.Domain.Rules;
using Game.Fortifications.Unity.Views;
using Game.Infrastructure.Maps.Serialization;
using Game.Maps.Domain;
using Game.Maps.Domain.Mappings;
using Game.Maps.Unity.Assets;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tools.MapAuthoring.Tests
{
    public sealed class WallArchitectureTests
    {
        [Test]
        public void FortificationHealth_LoadsIndependentlyFromItsSpatialDimensions()
        {
            var catalog = FacilityCatalogJson.Read("{\"schemaVersion\":1,\"facilities\":[],\"wall\":{\"width\":0.52,\"height\":0.5,\"junctionRadius\":0.4,\"wallHealth\":750,\"gateHealth\":120}}");
            Assert.That(catalog.Wall.WallHealth, Is.EqualTo(750));
            Assert.That(catalog.Wall.GateHealth, Is.EqualTo(120));
            Assert.That(catalog.Wall.Width, Is.EqualTo(0.52f));
        }

        [TestCase(0, 100)]
        [TestCase(300, 0)]
        [TestCase(-1, 100)]
        public void FortificationHealth_MustBePositive(int wallHealth, int gateHealth)
        {
            Assert.Throws<ArgumentException>(() => new WallDefinition(
                0.52f, 0.5f, 0.4f, gateHealth: gateHealth, wallHealth: wallHealth));
        }

        private static MapLayout Map()
        {
            var map = new MapLayout();
            map.wallNodes.Add(new WallNode { id = "a", x = -5, y = 0 });
            map.wallNodes.Add(new WallNode { id = "b", x = 5, y = 0 });
            map.wallEdges.Add(new WallEdge { id = "ab", from = "a", to = "b",
                gates = new System.Collections.Generic.List<WallGate> { new WallGate { id = "gate-a", startTile = 3, isOpen = true } } });
            return map;
        }

        [Test]
        public void CloseGate_UnitClearanceFailure_LeavesVersionNavigationAndWallStateUnchanged()
        {
            var source = Map();
            var catalog = new MapContentCatalog(Array.Empty<FacilityDefinition>());
            var runtime = new MapConstructionState(new MapDefinition(source, catalog), catalog);
            var navigation = runtime.Navigation;
            string before = MapJson.WriteDocument(runtime.Snapshot);
            Assert.That(runtime.TrySetGateOpen("gate-a", false, _ => false, out _), Is.False);
            Assert.That(runtime.Version, Is.Zero);
            Assert.That(runtime.Navigation, Is.SameAs(navigation));
            Assert.That(MapJson.WriteDocument(runtime.Snapshot), Is.EqualTo(before));

            Assert.That(runtime.TrySetGateOpen("gate-a", false, _ => true, out _), Is.True);
            Assert.That(runtime.Snapshot.wallEdges[0].gates[0].isOpen, Is.False);
            Assert.That(source.wallEdges[0].gates[0].isOpen, Is.True);
            var exported = runtime.Snapshot;
            exported.wallEdges.Clear();
            Assert.That(runtime.Snapshot.wallEdges.Count, Is.EqualTo(1));
        }

        [Test]
        public void PhysicalDefinition_JsonAndDifferentTerrainGrids_KeepWallDimensionsIndependentOfArtwork()
        {
            var catalog = FacilityCatalogJson.Read("{\"schemaVersion\":1,\"facilities\":[],\"wall\":{\"width\":0.8,\"height\":1.2,\"junctionRadius\":0.7}}");
            var visuals = AssetDatabase.LoadAssetAtPath<MapVisualCatalog>(Editor.MapEditorWindow.VisualPath);
            var map = Map();
            var network = MapWallSnapshotMapper.Read(map);
            var geometry = new WallRenderGeometry(catalog.Wall, visuals.WallStyle);
            float radius = WallConnectionRules.JunctionRadiusAt(network, "a", catalog.Wall);
            map.cellWidth = 2;
            map.cellHeight = 3;
            var resizedGrid = MapWallSnapshotMapper.Read(map);
            Assert.That(geometry.Width, Is.EqualTo(0.8f));
            Assert.That(geometry.DeckHeight, Is.EqualTo(1.2f));
            Assert.That(WallConnectionRules.JunctionRadiusAt(resizedGrid, "a", catalog.Wall), Is.EqualTo(radius));
            Assert.That(new MapContentCatalog(Array.Empty<FacilityDefinition>()).Wall.Width, Is.EqualTo(WallDefinition.Default.Width));
        }

        [Test]
        public void WallAssemblies_DoNotReferenceMapContentEditorOrBattle()
        {
            var domain = typeof(WallConnectionRules).Assembly.GetReferencedAssemblies().Select(item => item.Name).ToArray();
            Assert.That(domain.Any(name => name.StartsWith("Unity") || name.StartsWith("Game.Maps") || name == "Game.MapContent"), Is.False);
            var view = typeof(RaisedWallPresentation).Assembly.GetReferencedAssemblies().Select(item => item.Name).ToArray();
            Assert.That(view.Any(name => name.StartsWith("UnityEditor") || name.StartsWith("Game.Maps") || name == "Game.MapContent" || name.StartsWith("Game.Battle")), Is.False);
        }

        [Test]
        public void InvalidDraft_NonfiniteGateNode_ReportsInvalidGateWithoutThrowing()
        {
            var map = Map();
            map.wallNodes[0].x = float.NaN;
            Assert.That(Game.Maps.Domain.WallGateRules.Validate(map, map.wallEdges[0], out var reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
        }

        [Test]
        public void InvalidDraft_MissingNode_StillProducesLocatedDiagnosticsInsteadOfAViewException()
        {
            var map = Map();
            map.wallEdges[0].to = "missing";
            Assert.That(MapWallSnapshotMapper.TryRead(map, out _), Is.False);
            var catalog = new MapContentCatalog(Array.Empty<FacilityDefinition>());
            Assert.That(MapContentValidator.Validate(map, catalog).Any(issue => issue.ObjectId == "ab"), Is.True);
        }
    }
}
