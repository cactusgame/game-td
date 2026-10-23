using Game.Construction.Application.Sessions;
using Game.Infrastructure.Maps.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Battle.Contracts.Commands;
using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.Definitions;
using Game.Battle.Simulation.Bridges;
using Game.Infrastructure.Configuration;
using Game.MapEngine.Domain;
using Game.Maps.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tools.MapAuthoring.Tests
{
    public sealed class MapAuthoringTests
    {
        private static MapContentCatalog Catalog() => new MapContentCatalog(new[]
        {
            new FacilityDefinition { id = "tower", name = "Tower", width = 1, height = 1 },
            new FacilityDefinition { id = "hall", name = "Hall", width = 2, height = 2 }
        });

        private static MapLayout Map()
        {
            var map = new MapLayout();
            map.markers.Add(new MapMarker { id = "hero", name = "Hero", kind = MarkerKind.HeroSpawn, x = -5.5f, y = 0.5f });
            map.markers.Add(new MapMarker { id = "entry", name = "Entry", kind = MarkerKind.EnemyEntry, x = 10.5f, y = 0.5f });
            return map;
        }

        [Test]
        public void EditorReplacementIsAtomicUndoableAndRuntimeStillRejectsOccupancy()
        {
            var map = Map();
            map.facilities.Add(new InitialFacility { id = "old", definitionId = "tower", x = 2, y = 2 });
            var session = new MapEditSession(map);
            var replacement = new InitialFacility { id = "new", definitionId = "hall", x = 2, y = 2 };
            Assert.That(MapEditSession.TryReplaceFacility(map, Catalog(), replacement, out var next, out _), Is.True);
            Assert.That(map.facilities.Single().id, Is.EqualTo("old"));
            session.Commit(next);
            Assert.That(session.Document.facilities.Single().id, Is.EqualTo("new"));
            session.Undo();
            Assert.That(session.Document.facilities.Single().id, Is.EqualTo("old"));
            session.Redo();
            var runtime = new MapConstructionState(new MapDefinition(session.Document, Catalog()), Catalog());
            Assert.That(runtime.TryBuild("tower", 3, 3, _ => true, out _), Is.False);
            Assert.That(runtime.Version, Is.Zero);
            Assert.That(runtime.Snapshot.facilities.Single().id, Is.EqualTo("new"));
            map.terrain[3 * map.columns + 3] = TerrainKind.Water;
            Assert.That(MapEditSession.TryReplaceFacility(map, Catalog(), replacement, out _, out _), Is.False);
            Assert.That(map.facilities.Single().id, Is.EqualTo("old"));
        }

        [Test]
        public void HistoryTracksWholeOperationSavedRevisionAndBranchingAcrossReload()
        {
            var session = new MapEditSession(Map(), true);
            var working = session.Document;
            working.terrain[0] = TerrainKind.Water;
            working.terrain[1] = TerrainKind.Water;
            session.Commit(working);
            Assert.That(session.IsDirty, Is.True);
            session.Undo();
            Assert.That(session.IsDirty, Is.False);
            Assert.That(session.Document.terrain[0], Is.EqualTo(TerrainKind.Grass));
            session.Redo();
            session.MarkSaved();
            var state = JsonUtility.ToJson(session.CaptureHistory());
            session = MapEditSession.Restore(JsonUtility.FromJson<MapEditSession.History>(state));
            Assert.That(session.Document.terrain[1], Is.EqualTo(TerrainKind.Water));
            session.Undo();
            Assert.That(session.IsDirty, Is.True);
            session.Commit(Map());
            Assert.That(session.CanRedo, Is.False);
            Assert.That(session.IsDirty, Is.True);
        }

        [Test]
        public void ExportRoundTripAndRuntimeEditsNeverMutateTheSource()
        {
            var source = Map();
            source.facilities.Add(new InitialFacility { id = "initial", definitionId = "tower", x = 2, y = 2 });
            var json = MapJson.WriteDocument(source);
            var reopened = MapJson.ReadDocument(json);
            var definition = new MapDefinition(reopened, Catalog());
            reopened.terrain[0] = TerrainKind.Water;
            var runtime = new MapConstructionState(definition, Catalog());
            Assert.That(runtime.TryBuild("tower", 3, 3, _ => true, out _), Is.True);
            Assert.That(runtime.TryRemove(-9.5f, -5.5f), Is.True);
            Assert.That(runtime.Snapshot.facilities.Single().id, Is.Not.EqualTo("initial"));
            Assert.That(MapJson.WriteDocument(source), Is.EqualTo(json));
            Assert.That(definition.CreateWorkingCopy().facilities.Single().id, Is.EqualTo("initial"));
            Assert.That(definition.CreateWorkingCopy().terrain[0], Is.EqualTo(TerrainKind.Grass));
        }

        [Test]
        public void BuildRejectsBlockedTerrainOccupancyMarkersAndUnitsWithoutPartialState()
        {
            var map = Map();
            map.terrain[0] = TerrainKind.Water;
            map.terrain[1] = TerrainKind.Rock;
            map.terrain[2] = TerrainKind.Tree;
            var runtime = new MapConstructionState(new MapDefinition(map, Catalog()), Catalog());
            Assert.That(runtime.TryBuild("tower", 0, 0, _ => true, out _), Is.False);
            Assert.That(runtime.TryBuild("tower", 1, 0, _ => true, out _), Is.False);
            Assert.That(runtime.TryBuild("tower", 2, 0, _ => true, out _), Is.False);
            Assert.That(runtime.TryBuild("hall", 23, 15, _ => true, out _), Is.False);
            Assert.That(runtime.TryBuild("tower", 6, 8, _ => true, out _), Is.False);
            Assert.That(runtime.TryBuild("tower", 4, 4, _ => false, out _), Is.False);
            Assert.That(runtime.Version, Is.Zero);
            Assert.That(runtime.Snapshot.facilities, Is.Empty);
            Assert.That(runtime.TryBuild("tower", 4, 4, _ => true, out _), Is.True);
            Assert.That(runtime.TryBuild("tower", 4, 4, _ => true, out _), Is.False);
            Assert.That(runtime.Navigation.IsWalkable(new GridCell(4, 4)), Is.False);
        }

        [Test]
        public void ValidationLocatesBadFacilityAndInvalidWallReferences()
        {
            var map = Map();
            map.terrain[0] = TerrainKind.Water;
            map.facilities.Add(new InitialFacility { id = "bad-tower", definitionId = "tower", x = 0, y = 0 });
            map.wallEdges.Add(new WallEdge { id = "bad-wall", from = "missing-a", to = "missing-b" });
            var issues = MapContentValidator.Validate(map, Catalog());
            Assert.That(issues.Any(issue => issue.ObjectId == "bad-tower" && issue.X == 0 && issue.Y == 0), Is.True);
            Assert.That(issues.Any(issue => issue.ObjectId == "bad-wall"), Is.True);
            Assert.Throws<ArgumentException>(() => new MapDefinition(map, Catalog()));
            map.facilities.Add(null);
            Assert.DoesNotThrow(() => MapContentValidator.Validate(map, Catalog()));
        }

        [Test]
        public void WallIdentityAndTopologySurviveExportAndRuntimeRemoval()
        {
            var map = Map();
            map.wallNodes.Add(new WallNode { id = "a", x = -0.5f, y = 2.5f });
            map.wallNodes.Add(new WallNode { id = "b", x = 3.5f, y = 3.5f });
            map.wallEdges.Add(new WallEdge { id = "ab", from = "a", to = "b" });
            var definition = MapJson.LoadDefinition(MapJson.WriteDocument(map), Catalog());
            var runtime = new MapConstructionState(definition, Catalog());
            Assert.That(runtime.Navigation.IsWalkable(new GridCell(11, 10)), Is.False);
            Assert.That(runtime.TryRemove(1.5f, 3), Is.True);
            Assert.That(runtime.Snapshot.wallEdges, Is.Empty);
            Assert.That(definition.CreateWorkingCopy().wallEdges.Single().id, Is.EqualTo("ab"));
        }

        [Test]
        public void ActualMovementRespectsImportedTerrainRadiusAndArbitraryOrigin()
        {
            var rules = JsonBattleDefinitionSource.Parse(File.ReadAllText("Assets/Content/Configuration/Resources/GameConfig/battle.json"));
            var walkable = Enumerable.Repeat(true, 12 * 12).ToArray();
            for (int y = 0; y < 12; y++)
                walkable[y * 12 + 5] = false;
            var grid = new GridMap(12, 12, 1, 1, 10, 20, walkable);
            var definition = new BattleDefinition(rules.Hero, rules.Goblin, rules.Runner, 0, 0, 12, 12, 0.02f, 1,
                navigationMap: grid, heroSpawn: new MapPoint(11.5f, 24.5f));
            using var simulation = new EcsBattleSimulation();
            simulation.Reset(definition);
            for (int i = 0; i < 200; i++)
                simulation.Step(0.02f, new MoveIntent(1, 0));
            var units = new List<UnitSnapshot>();
            simulation.CopyUnits(units);
            Assert.That(units[0].X, Is.GreaterThan(11.5f));
            Assert.That(units[0].X + rules.Hero.Radius, Is.LessThanOrEqualTo(15.0001f));
            Assert.That(grid.CanOccupy(units[0].X, units[0].Y, rules.Hero.Radius), Is.True);
            simulation.UpdateNavigationMap(new GridMap(12, 12, 1, 1, 10, 20), definition.Navigation);
            for (int i = 0; i < 100; i++)
                simulation.Step(0.02f, new MoveIntent(1, 0));
            simulation.CopyUnits(units);
            Assert.That(units[0].X, Is.GreaterThan(15));
        }

        [Test]
        public void CircleClearanceUsesRectangularCellsAndCannotCutCorners()
        {
            var map = new GridMap(3, 3, 2, 1, 0, 0, new[] { true, false, true, true, true, true, true, true, true });
            Assert.That(map.CanOccupy(1.8f, 0.5f, 0.3f), Is.False);
            Assert.That(map.CanOccupy(1.5f, 0.5f, 0.3f), Is.True);
            Assert.That(map.CanOccupy(float.PositiveInfinity, 1, 0.3f), Is.False);
        }

        [Test]
        public void CrossingWallsRequireAnExplicitSharedNode()
        {
            var map = Map();
            map.wallNodes.Add(new WallNode { id = "a", x = -1, y = 2 });
            map.wallNodes.Add(new WallNode { id = "b", x = 1, y = 4 });
            map.wallNodes.Add(new WallNode { id = "c", x = -1, y = 4 });
            map.wallNodes.Add(new WallNode { id = "d", x = 1, y = 2 });
            map.wallEdges.Add(new WallEdge { id = "ab", from = "a", to = "b" });
            map.wallEdges.Add(new WallEdge { id = "cd", from = "c", to = "d" });
            Assert.That(MapContentValidator.Validate(map, Catalog()).Any(issue => issue.ObjectId == "ab" && issue.Message.Contains("共享节点")), Is.True);
        }

        [Test]
        public void ExplicitSpawnsDoNotUseTheOldCenteredRandomSpawnAssumption()
        {
            var rules = JsonBattleDefinitionSource.Parse(File.ReadAllText("Assets/Content/Configuration/Resources/GameConfig/battle.json"));
            var map = new GridMap(3, 3, 1, 1, 10, 20);
            var definition = new BattleDefinition(rules.Hero, rules.Goblin, rules.Runner, 1, 0, 3, 3, 0.02f, 1,
                navigationMap: map, heroSpawn: new MapPoint(10.5f, 20.5f), enemySpawns: new[] { new MapPoint(12.5f, 22.5f) });
            using var simulation = new EcsBattleSimulation();
            simulation.Reset(definition);
            var units = new List<UnitSnapshot>();
            simulation.CopyUnits(units);
            Assert.That(units[0].X, Is.EqualTo(10.5f));
            Assert.That(units[1].X, Is.EqualTo(12.5f));
        }
    }
}
