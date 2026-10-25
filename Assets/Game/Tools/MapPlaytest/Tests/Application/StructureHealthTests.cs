using System.IO;
using System.Linq;
using Game.Battle.Contracts.Commands;
using Game.Battle.Contracts.Ports;
using Game.Battle.Simulation.Bridges;
using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.ValueObjects;
using Game.Infrastructure.Configuration;
using Game.Infrastructure.Maps.Serialization;
using Game.Maps.Domain;
using Game.Tools.MapPlaytest.Application.Sessions;
using NUnit.Framework;

namespace Game.Tools.MapPlaytest.Tests
{
    public sealed class StructureHealthTests
    {
        private const string Root = "Assets/Content/Configuration/Resources/GameConfig/";
        private sealed class Input : IHeroInput { public MoveIntent Read() => default; }

        private static MapPlaySession CreateSession()
        {
            var catalog = FacilityCatalogJson.Read(File.ReadAllText(Root + "facilities.json"));
            var map = MapJson.ReadDocument(File.ReadAllText("Assets/Content/Maps/Example/defense.map.json"));
            var session = new MapPlaySession(new MapDefinition(map, catalog), catalog,
                JsonBattleDefinitionSource.Parse(File.ReadAllText(Root + "battle.json")), new Input(), () => new EcsBattleSimulation());
            session.Restart();
            return session;
        }

        [Test]
        public void DamageRepairAndDestruction_ReadAuthoritativeHealth_AndRestartRestoresIt()
        {
            using var session = CreateSession();
            var initial = session.Structures.Single(item => item.Id == "first-tower");
            Assert.That(initial.MaxHealth, Is.EqualTo(150));
            Assert.That(session.TryChangeStructureHealth(initial.Id, false, 0.6f, out _), Is.True);
            Assert.That(session.Structures.Single(item => item.Id == initial.Id).Health, Is.EqualTo(60));
            Assert.That(session.TryChangeStructureHealth(initial.Id, true, 1, out _), Is.True);
            Assert.That(session.Structures.Single(item => item.Id == initial.Id).Health, Is.EqualTo(initial.MaxHealth));
            Assert.That(session.TryChangeStructureHealth(initial.Id, false, 1, out _), Is.True);
            Assert.That(session.TryChangeStructureHealth(initial.Id, true, 1, out _), Is.False);
            session.Restart();
            Assert.That(session.Structures.Single(item => item.Id == initial.Id).Health, Is.EqualTo(initial.MaxHealth));
        }

        [Test]
        public void EnemyContactDamagesWallOncePerAttackInterval_WithoutAlsoDamagingGate()
        {
            using var session = CreateSession();
            var catalog = FacilityCatalogJson.Read(File.ReadAllText(Root + "facilities.json"));
            var fortifications = new GatePassageSession(catalog);
            using var simulation = new EcsBattleSimulation();
            simulation.Reset(session.Definition);
            var units = new[] { new UnitSnapshot(100, UnitKind.Goblin, -0.2f, -3.5f, 1, 100, 100, -100) };
            fortifications.Update(simulation, session.Snapshot, session.Definition, units, 0, true);
            int afterHit = fortifications.WallHealth("test-wall");
            Assert.That(afterHit, Is.LessThan(catalog.Wall.WallHealth));
            fortifications.Update(simulation, session.Snapshot, session.Definition, units, 0, true);
            Assert.That(fortifications.WallHealth("test-wall"), Is.EqualTo(afterHit));
            Assert.That(fortifications.Health("test-gate"), Is.EqualTo(catalog.Wall.GateHealth));
        }

        [Test]
        public void WallAndGate_RepairClamps_DestructionPublishesNavigation_AndCannotRevive()
        {
            using var session = CreateSession();
            Assert.That(session.Navigation.CanOccupy(0.5f, -3.5f, 0.1f), Is.False);
            foreach (string id in new[] { "test-gate", "test-wall" })
            {
                Assert.That(session.TryChangeStructureHealth(id, false, 0.6f, out var reason), Is.True, reason);
                Assert.That(session.TryChangeStructureHealth(id, true, 1, out reason), Is.True, reason);
                int version = session.GateViewVersion;
                Assert.That(session.TryChangeStructureHealth(id, false, 1, out reason), Is.True, reason);
                Assert.That(session.GateViewVersion, Is.GreaterThan(version));
                Assert.That(session.TryChangeStructureHealth(id, true, 1, out _), Is.False);
            }
            Assert.That(session.GetWallHealth("test-wall"), Is.Zero);
            Assert.That(session.GetGateHealth("test-gate"), Is.Zero);
            Assert.That(session.Navigation.CanOccupy(0.5f, -3.5f, 0.1f), Is.True);
        }
    }
}
