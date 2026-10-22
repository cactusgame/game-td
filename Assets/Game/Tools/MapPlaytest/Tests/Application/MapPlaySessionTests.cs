using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Battle.Contracts.Commands;
using Game.Battle.Contracts.Ports;
using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.Definitions;
using Game.Battle.Domain.ValueObjects;
using Game.Battle.Simulation.Bridges;
using Game.Infrastructure.Configuration;
using Game.MapEngine.Domain;
using Game.Maps.Domain;
using Game.Tools.MapPlaytest.Application.Sessions;
using NUnit.Framework;
using Unity.Entities;

namespace Game.Tools.MapPlaytest.Tests
{
    public sealed class MapPlaySessionTests
    {
        private static MapLayout Source()
        {
            var map = new MapLayout();
            map.markers.Add(new MapMarker { id = "hero", name = "Hero", kind = MarkerKind.HeroSpawn, x = -5.5f, y = 0.5f });
            map.markers.Add(new MapMarker { id = "entry", name = "Entry", kind = MarkerKind.EnemyEntry, x = 10.5f, y = 0.5f });
            return map;
        }

        private static MapContentCatalog Catalog() => new MapContentCatalog(new[]
        {
            new FacilityDefinition { id = "tower", width = 1, height = 1 }
        });

        private static BattleDefinition Rules() => JsonBattleDefinitionSource.Parse(File.ReadAllText("Assets/Content/Combat/Prototype/battle.json"));

        private static MapPlaySession Session(Func<ISpatialBattleSimulation> create, MapLayout source = null, IHeroInput input = null)
        {
            var catalog = Catalog();
            return new MapPlaySession(new MapDefinition(source ?? Source(), catalog), catalog, Rules(), input ?? new Input(), create);
        }

        [Test]
        public void BuildAndRemovePublishMapAndSimulationSpaceTogetherWithoutChangingTheSource()
        {
            var source = Source();
            var simulation = new Simulation();
            using var session = Session(() => simulation, source);
            session.Restart();
            Assert.That(session.TryBuild("tower", 4, 4, out _), Is.True);
            Assert.That(session.MapVersion, Is.EqualTo(1));
            Assert.That(simulation.Navigation.IsWalkable(new GridCell(4, 4)), Is.False);
            Assert.That(source.facilities, Is.Empty);
            Assert.That(session.TryRemove(-7.5f, -3.5f, out _), Is.True);
            Assert.That(session.MapVersion, Is.EqualTo(2));
            Assert.That(simulation.Navigation.IsWalkable(new GridCell(4, 4)), Is.True);
            Assert.That(session.Snapshot.facilities, Is.Empty);
        }

        private static MapLayout GateMap(bool open = false)
        {
            var map = Source();
            map.wallNodes.Add(new WallNode { id = "a", x = -8.5f, y = 2.5f });
            map.wallNodes.Add(new WallNode { id = "b", x = 9.5f, y = 2.5f });
            map.wallEdges.Add(new WallEdge { id = "gate-wall", from = "a", to = "b", hasGate = true, gate = new WallGate { isOpen = open } });
            return map;
        }

        [Test]
        public void GateChangesPublishNavigationAndRestartRestoresTheSourceState()
        {
            var source = GateMap();
            var simulation = new Simulation();
            using var session = Session(() => simulation, source);
            session.Restart();
            session.Navigation.TryWorldToCell(0.5f, 2.5f, out var opening);
            session.Navigation.TryWorldToCell(2.5f, 2.5f, out var frame);
            Assert.That(session.Navigation.IsWalkable(opening), Is.False);
            Assert.That(session.TrySetGateOpen("gate-wall", true, out _), Is.True);
            Assert.That(session.Navigation.IsWalkable(opening), Is.True);
            Assert.That(session.Navigation.IsWalkable(frame), Is.False);
            Assert.That(simulation.Navigation, Is.SameAs(session.Navigation));
            Assert.That(source.wallEdges[0].gate.isOpen, Is.False);
            var snapshot = session.Snapshot;
            snapshot.wallEdges[0].gate.isOpen = false;
            Assert.That(session.Snapshot.wallEdges[0].gate.isOpen, Is.True);
            int version = session.MapVersion;
            Assert.That(session.TrySetGateOpen("gate-wall", true, out _), Is.True);
            Assert.That(session.MapVersion, Is.EqualTo(version));
            session.Restart();
            Assert.That(session.Snapshot.wallEdges[0].gate.isOpen, Is.False);
            Assert.That(session.Navigation.IsWalkable(opening), Is.False);
        }

        [Test]
        public void GateCannotCloseOverAUnitAndFailedUpdatesDoNotChangeItsState()
        {
            var simulation = new Simulation();
            using var session = Session(() => simulation, GateMap(true));
            session.Restart();
            simulation.HeroPosition = new MapPoint(0.5f, 2.5f);
            session.Advance(0);
            Assert.That(session.TrySetGateOpen("gate-wall", false, out var reason), Is.False);
            Assert.That(reason, Does.Contain("单位"));
            Assert.That(session.MapVersion, Is.Zero);
            Assert.That(simulation.UpdateCount, Is.Zero);
            Assert.That(session.Snapshot.wallEdges[0].gate.isOpen, Is.True);
            simulation.HeroPosition = new MapPoint(-5.5f, 0.5f);
            session.Advance(0);
            simulation.FailNextUpdate = true;
            Assert.That(session.TrySetGateOpen("gate-wall", false, out reason), Is.False);
            Assert.That(reason, Does.Contain("空间同步失败"));
            Assert.That(session.MapVersion, Is.Zero);
            Assert.That(session.Snapshot.wallEdges[0].gate.isOpen, Is.True);
            Assert.That(simulation.Navigation, Is.SameAs(session.Navigation));
            Assert.That(session.TrySetGateOpen("gate-wall", false, out _), Is.True);
            Assert.That(session.Snapshot.wallEdges[0].gate.isOpen, Is.False);
        }

        [Test]
        public void RealEcsHeroStopsAtClosedGateAndCrossesAfterItOpens()
        {
            var source = GateMap();
            source.markers.Find(marker => marker.kind == MarkerKind.HeroSpawn).x = 0.5f;
            var input = new Input { Y = 1 };
            int worlds = World.All.Count;
            using (var session = Session(() => new EcsBattleSimulation(), source, input))
            {
                session.Restart();
                for (int i = 0; i < 60; i++)
                    session.Advance(0.02f);
                Assert.That(session.Units.First(unit => unit.Kind == UnitKind.Hero).Y, Is.LessThan(2));
                Assert.That(session.TrySetGateOpen("gate-wall", true, out _), Is.True);
                for (int i = 0; i < 15; i++)
                    session.Advance(0.02f);
                Assert.That(session.TrySetGateOpen("gate-wall", false, out _), Is.False);
                for (int i = 0; i < 50; i++)
                    session.Advance(0.02f);
                Assert.That(session.Units.First(unit => unit.Kind == UnitKind.Hero).Y, Is.GreaterThan(3.5f));
                Assert.That(session.TrySetGateOpen("gate-wall", false, out _), Is.True);
            }
            Assert.That(World.All.Count, Is.EqualTo(worlds));
        }

        [Test]
        public void FailedSpatialUpdateRestoresSimulationAndDoesNotPublishCandidate()
        {
            var simulation = new Simulation();
            using var session = Session(() => simulation);
            session.Restart();
            simulation.FailNextUpdate = true;
            Assert.That(session.TryBuild("tower", 4, 4, out var reason), Is.False);
            Assert.That(reason, Does.Contain("空间同步失败"));
            Assert.That(session.MapVersion, Is.Zero);
            Assert.That(session.Snapshot.facilities, Is.Empty);
            Assert.That(simulation.Navigation.IsWalkable(new GridCell(4, 4)), Is.True);
            session.Advance(0.02f);
            Assert.That(session.TryBuild("tower", 4, 4, out _), Is.True);
        }

        [Test]
        public void FailedSpatialRollbackPreventsFurtherSimulationUntilRestart()
        {
            var first = new Simulation();
            var second = new Simulation();
            var queue = new Queue<Simulation>(new[] { first, second });
            using var session = Session(() => queue.Dequeue());
            session.Restart();
            first.FailEveryUpdate = true;
            Assert.Throws<AggregateException>(() => session.TryBuild("tower", 4, 4, out _));
            Assert.Throws<InvalidOperationException>(() => session.Advance(0.02f));
            session.Restart();
            Assert.That(first.DisposeCount, Is.EqualTo(1));
            Assert.DoesNotThrow(() => session.Advance(0.02f));
        }

        [Test]
        public void BuildingOverAMovingUnitIsRejectedBeforeSpatialPublication()
        {
            var simulation = new Simulation();
            using var session = Session(() => simulation);
            session.Restart();
            simulation.HeroPosition = new MapPoint(3.5f, -2.5f);
            session.Advance(0);
            Assert.That(session.TryBuild("tower", 15, 5, out _), Is.False);
            Assert.That(session.MapVersion, Is.Zero);
            Assert.That(simulation.UpdateCount, Is.Zero);
        }

        [Test]
        public void FailedRestartDisposesCandidateAndPreservesTheRunningSession()
        {
            var running = new Simulation();
            var failing = new Simulation { FailReset = true };
            var queue = new Queue<Simulation>(new[] { running, failing });
            using var session = Session(() => queue.Dequeue());
            session.Restart();
            session.TryBuild("tower", 4, 4, out _);
            Assert.Throws<InvalidOperationException>(() => session.Restart());
            Assert.That(running.DisposeCount, Is.Zero);
            Assert.That(failing.DisposeCount, Is.EqualTo(1));
            Assert.That(session.Generation, Is.EqualTo(1));
            Assert.That(session.MapVersion, Is.EqualTo(1));
            Assert.DoesNotThrow(() => session.Advance(0.02f));
        }

        [Test]
        public void RestartRestoresInitialPlacementsAndEachOwnedSimulationIsDisposedOnce()
        {
            var first = new Simulation();
            var second = new Simulation();
            var queue = new Queue<Simulation>(new[] { first, second });
            var session = Session(() => queue.Dequeue());
            try
            {
                session.Restart();
                session.TryBuild("tower", 4, 4, out _);
                session.Restart();
                Assert.That(first.DisposeCount, Is.EqualTo(1));
                Assert.That(session.MapVersion, Is.Zero);
                Assert.That(session.Snapshot.facilities, Is.Empty);
                Assert.That(session.Generation, Is.EqualTo(2));
            }
            finally
            {
                session.Dispose();
                session.Dispose();
            }
            Assert.That(second.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void InvalidPlayProfileDoesNotAllocateSimulation()
        {
            int created = 0;
            using var session = Session(() => { created++; return new Simulation(); }, new MapLayout());
            Assert.Throws<ArgumentException>(() => session.Restart());
            Assert.That(created, Is.Zero);
        }

        [Test]
        public void RealEcsSessionAdvancesAndReleasesItsWorldAfterSpatialEditsAndRestart()
        {
            int worlds = World.All.Count;
            using (var session = Session(() => new EcsBattleSimulation()))
            {
                session.Restart();
                Assert.That(session.TryBuild("tower", 4, 4, out _), Is.True);
                for (int i = 0; i < 20; i++)
                    session.Advance(0.02f);
                Assert.That(session.SimulationTime, Is.GreaterThan(0.3));
                Assert.That(session.Units.Count, Is.EqualTo(2));
                Assert.That(session.TryRemove(-7.5f, -3.5f, out _), Is.True);
                session.Restart();
                Assert.That(session.MapVersion, Is.Zero);
            }
            Assert.That(World.All.Count, Is.EqualTo(worlds));
        }

        [Test]
        public void ApplicationSessionHasNoUnityEcsOrSerializationDependency()
        {
            var references = typeof(MapPlaySession).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Assert.That(references.Any(n => n.StartsWith("Unity") || n == "Game.Battle.Simulation" || n == "Game.Maps.Serialization"), Is.False);
        }

        private sealed class Input : IHeroInput
        {
            public float Y;
            public MoveIntent Read() => new MoveIntent(0, Y);
        }

        private sealed class Simulation : ISpatialBattleSimulation
        {
            private BattleDefinition _definition;
            public double ElapsedTime { get; private set; }
            public int ContactCount => 0;
            public GridMap Navigation;
            public MapPoint HeroPosition;
            public bool FailNextUpdate;
            public bool FailEveryUpdate;
            public bool FailReset;
            public int DisposeCount;
            public int UpdateCount;

            public void Reset(BattleDefinition definition)
            {
                if (FailReset)
                    throw new InvalidOperationException("Candidate failed.");
                _definition = definition;
                Navigation = definition.NavigationMap;
                HeroPosition = definition.HeroSpawn;
                ElapsedTime = 0;
            }

            public void Step(float deltaTime, MoveIntent intent) => ElapsedTime += deltaTime;

            public void CopyUnits(List<UnitSnapshot> destination)
            {
                destination.Clear();
                destination.Add(new UnitSnapshot(1, UnitKind.Hero, HeroPosition.X, HeroPosition.Y, 1, _definition.Hero.MaxHealth, _definition.Hero.MaxHealth, -100));
            }

            public void UpdateNavigationMap(GridMap map, NavigationDefinition navigation)
            {
                UpdateCount++;
                Navigation = map;
                if (FailNextUpdate || FailEveryUpdate)
                {
                    FailNextUpdate = false;
                    throw new InvalidOperationException("Spatial replacement failed.");
                }
            }

            public void Dispose() => DisposeCount++;
        }
    }
}
