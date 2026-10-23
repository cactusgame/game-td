using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Battle.Contracts.Commands;
using Game.Battle.Contracts.Ports;
using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.Definitions;
using Game.Battle.Domain.ValueObjects;
using Game.Battle.Simulation.Bridges;
using Game.Encounter.Domain.Aggregates.DefenseEncounter;
using Game.Infrastructure.Configuration;
using Game.Infrastructure.Maps.Serialization;
using Game.Maps.Domain;
using Game.Tools.MapPlaytest.Application.Sessions;
using NUnit.Framework;

namespace Game.Tools.MapPlaytest.Tests
{
    public sealed class DefenseLoopTests
    {
        private static MapContentCatalog Catalog() => FacilityCatalogJson.Read(File.ReadAllText("Assets/Content/Maps/Shared/facilities.json"));
        private static MapLayout Map()
        {
            var map = new MapLayout();
            map.markers.Add(new MapMarker { id = "hero", name = "Hero", kind = MarkerKind.HeroSpawn, x = 8.5f, y = 5.5f });
            map.markers.Add(new MapMarker { id = "entry-a", name = "West", kind = MarkerKind.EnemyEntry, x = -9.5f, y = 0.5f });
            map.markers.Add(new MapMarker { id = "entry-b", name = "North", kind = MarkerKind.EnemyEntry, x = -9.5f, y = 3.5f });
            map.facilities.Add(new InitialFacility { id = "castle", definitionId = "hall", x = 12, y = 7 });
            return map;
        }
        private static MapPlaySession Session(MapLayout map, MapContentCatalog catalog)
        {
            var rules = JsonBattleDefinitionSource.Parse(File.ReadAllText("Assets/Content/Combat/Prototype/battle.json"));
            return new MapPlaySession(new MapDefinition(map, catalog), catalog, rules, new Input(), () => new EcsBattleSimulation());
        }
        [Test]
        public void SameWaveSpawnsApartAndKeepsVisibleSpacingWhileFollowingTheCastle()
        {
            using var session = Session(Map(), Catalog());
            session.Restart();
            AssertEnemySpacing(session.Units, 0.90f);
            for (int step = 0; step < 200; step++)
                session.Advance(0.02f);
            AssertEnemySpacing(session.Units, 0.45f);
        }

        private static void AssertEnemySpacing(IReadOnlyList<UnitSnapshot> units, float minimum)
        {
            var monsters = units.Where(unit => unit.Kind != UnitKind.Hero && unit.Health > 0).ToArray();
            Assert.That(monsters.Length, Is.EqualTo(10));
            for (int i = 0; i < monsters.Length; i++)
            {
                for (int j = i + 1; j < monsters.Length; j++)
                {
                    float x = monsters[i].X - monsters[j].X;
                    float y = monsters[i].Y - monsters[j].Y;
                    Assert.That(x * x + y * y, Is.GreaterThanOrEqualTo(minimum * minimum),
                        $"怪物 {monsters[i].Id} 与 {monsters[j].Id} 重叠");
                }
            }
        }

        [Test]
        public void MapRequiresMainCastleButEditorDocumentCanBeIncomplete()
        {
            var map = Map();
            var catalog = Catalog();
            Assert.That(MapPlayabilityValidator.Validate(map, catalog).Any(i => i.IsError), Is.False);
            map.facilities.Clear();
            Assert.DoesNotThrow(() => new MapDefinition(map, catalog));
            Assert.That(MapPlayabilityValidator.Validate(map, catalog).Any(i => i.Message.Contains("主堡")), Is.True);
        }
        [Test]
        public void FirstWaveCannotWinAndDefeatWinsATie()
        {
            var encounter = new DefenseEncounter();
            Assert.That(encounter.DueCount(0), Is.EqualTo(5));
            encounter.CommitWave();
            encounter.Evaluate(false, 0);
            Assert.That(encounter.IsRunning, Is.True);
            Assert.That(encounter.DueCount(9.99), Is.Zero);
            Assert.That(encounter.DueCount(10), Is.EqualTo(10));
            encounter.CommitWave();
            encounter.Evaluate(true, 0);
            Assert.That(encounter.Outcome, Is.EqualTo("Defeat"));
            encounter.Evaluate(false, 0);
            Assert.That(encounter.Outcome, Is.EqualTo("Defeat"));
        }
        [Test]
        public void EachEntrySpawnsFiveThenTenAndRestartRestoresEverything()
        {
            var map = Map();
            using var session = Session(map, Catalog());
            session.Restart();
            Assert.That(session.Units.Count(u => u.Kind != UnitKind.Hero), Is.EqualTo(10));
            for (int i = 0; i < 499; i++)
                session.Advance(0.02f);
            Assert.That(session.SpawnedWaves, Is.EqualTo(1));
            for (int i = 0; i < 3; i++)
                session.Advance(0.02f);
            Assert.That(session.SpawnedWaves, Is.EqualTo(2));
            Assert.That(session.Units.Count(u => u.Kind != UnitKind.Hero), Is.EqualTo(30));
            Assert.That(session.Units.Select(u => u.Id).Distinct().Count(), Is.EqualTo(31));
            Assert.That(session.TryRemove(0.5f, 0.5f, out _), Is.False);
            session.Restart();
            Assert.That(session.SpawnedWaves, Is.EqualTo(1));
            Assert.That(session.SimulationTime, Is.Zero);
            Assert.That(session.Structures.Single().Health, Is.EqualTo(500));
            Assert.That(map.facilities.Count, Is.EqualTo(1));
        }
        [Test]
        public void EnemiesIgnoreDistantHeroAttackCastleAndStopOnDefeat()
        {
            using var session = Session(Map(), Catalog());
            session.Restart();
            for (int i = 0; i < 6000 && session.Outcome == "Running"; i++)
                session.Advance(0.02f);
            Assert.That(session.Outcome, Is.EqualTo("Defeat"));
            Assert.That(session.Structures.Single().Health, Is.Zero);
            Assert.That(session.HeroHealth, Is.EqualTo(session.Definition.Hero.MaxHealth));
            double time = session.SimulationTime;
            session.Advance(1);
            Assert.That(session.SimulationTime, Is.EqualTo(time));
        }
        [Test]
        public void TowerKillsBothWavesAndVictoryKeepsCastleAlive()
        {
            var map = Map();
            map.facilities.Add(new InitialFacility { id = "tower", definitionId = "tower", x = 9, y = 8 });
            var catalog = new MapContentCatalog(new[]
            {
                new FacilityDefinition { id = "hall", width = 2, height = 2, maxHealth = 500 },
                new FacilityDefinition { id = "tower", attack = 10000, attackRange = 30, attackInterval = 0.02f }
            });
            using var session = Session(map, catalog);
            session.Restart();
            for (int i = 0; i < 1500 && session.Outcome == "Running"; i++)
                session.Advance(0.02f);
            Assert.That(session.Outcome, Is.EqualTo("Victory"));
            Assert.That(session.SpawnedWaves, Is.EqualTo(2));
            Assert.That(session.Units.Count(u => u.Kind != UnitKind.Hero && u.Health > 0), Is.Zero);
            Assert.That(session.Structures.Single(s => s.IsMainCastle).Health, Is.EqualTo(500));
        }
        private sealed class Input : IHeroInput
        {
            public MoveIntent Read() => new MoveIntent(0, 0);
        }
    }
}
