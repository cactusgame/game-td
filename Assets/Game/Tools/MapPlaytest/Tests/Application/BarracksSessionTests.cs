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
using Game.Construction.Domain.Aggregates.BarracksRoster;
using Game.Construction.Domain.Definitions;
using Game.Infrastructure.Configuration;
using Game.Infrastructure.Maps.Serialization;
using Game.MapEngine.Domain;
using Game.Maps.Domain;
using Game.Tools.MapPlaytest.Application.Sessions;
using NUnit.Framework;

namespace Game.Tools.MapPlaytest.Tests
{
    public sealed class BarracksSessionTests
    {
        private const string Root = "Assets/Content/Configuration/Resources/GameConfig/";
        private static BattleDefinition Rules() => JsonBattleDefinitionSource.Parse(File.ReadAllText(Root + "battle.json"));
        private static ConstructionContentDefinition Construction() => JsonConstructionContentSource.Parse(File.ReadAllText(Root + "construction-content.json"));
        private static MapLayout Source()
        {
            var map = new MapLayout();
            map.markers.Add(new MapMarker { id = "hero", name = "Hero", kind = MarkerKind.HeroSpawn, x = -9.5f, y = 6.5f });
            map.markers.Add(new MapMarker { id = "entry", name = "Entry", kind = MarkerKind.EnemyEntry, x = 10.5f, y = 6.5f });
            map.facilities.Add(new InitialFacility { id = "castle", definitionId = "hall", x = 1, y = 1 });
            map.facilities.Add(new InitialFacility { id = "barracks", definitionId = "barracks.melee", x = 10, y = 8 });
            return map;
        }
        private sealed class Input : IHeroInput { public MoveIntent Read() => default; }
        private static MapPlaySession Session(Simulation simulation, int gold = 1000)
        {
            var catalog = FacilityCatalogJson.Read(File.ReadAllText(Root + "facilities.json"));
            return new MapPlaySession(new MapDefinition(Source(), catalog), catalog, Rules(), new Input(), () => simulation,
                construction: Construction(), initialGold: gold);
        }
        private static UnitSnapshot[] Soldiers(MapPlaySession session) => session.Units.Where(u => UnitKinds.IsSoldier(u.Kind) && u.Health > 0).ToArray();

        [Test]
        public void ReferenceCitadelStartsWithBothActualBarracksAndDeploysEightVisibleSoldiers()
        {
            var catalog = FacilityCatalogJson.Read(File.ReadAllText(Root + "facilities.json"));
            var map = MapJson.LoadDefinition(File.ReadAllText("Assets/Content/Maps/Styles/PixelKingdom/reference-citadel.map.json"), catalog);
            using var session = new MapPlaySession(map, catalog, Rules(), new Input(), () => new EcsBattleSimulation(), construction: Construction());
            session.Restart();
            for (int i = 0; i < 200; i++) session.Advance(0.02f);
            Assert.That(Soldiers(session).Count(u => u.Kind == UnitKind.Soldier), Is.EqualTo(4));
            Assert.That(Soldiers(session).Count(u => u.Kind == UnitKind.Archer), Is.EqualTo(4));
            var statuses = session.GetBarracksStatuses();
            Assert.That(statuses.Count, Is.EqualTo(2));
            Assert.That(statuses.All(s => s.Alive == 4 && s.Training == 0 && s.WaitingAtExit == 0), Is.True);
            var hero = session.Units.Single(u => u.Kind == UnitKind.Hero);
            Assert.That(Soldiers(session).All(u => Math.Abs(u.X - hero.X) < 5 && Math.Abs(u.Y - hero.Y) < 4), Is.True,
                "开战后士兵应在英雄附近的广场上，而非视野外或建筑占地内");
        }

        [Test]
        public void BarracksReadModelDistinguishesWaitingAtExitFromTraining()
        {
            var simulation = new Simulation { Blocked = true };
            using var session = Session(simulation);
            session.Restart();
            var waiting = session.GetBarracksStatuses().Single();
            Assert.That(waiting.WaitingAtExit, Is.EqualTo(4));
            Assert.That(waiting.Training, Is.Zero);
            simulation.Blocked = false;
            session.Advance(0.02f);
            simulation.Kill(Soldiers(session)[0].Id);
            session.Advance(0.02f);
            var training = session.GetBarracksStatuses().Single();
            Assert.That(training.Alive, Is.EqualTo(3));
            Assert.That(training.Training, Is.EqualTo(1));
            Assert.That(training.WaitingAtExit, Is.Zero);
            Assert.That(training.NextReadySeconds, Is.EqualTo(8).Within(0.0001));
        }

        [Test]
        public void StartsWithFourSoldiersAndEachDeathStartsExactlyOneCooldown()
        {
            var simulation = new Simulation();
            using var session = Session(simulation);
            session.Restart();
            Assert.That(Soldiers(session).Length, Is.EqualTo(4));
            Assert.That(session.Units.Count(u => UnitKinds.IsEnemy(u.Kind)), Is.EqualTo(1));
            ulong dead = Soldiers(session)[0].Id;
            simulation.Kill(dead);
            session.Advance(0.02f);
            for (int i = 0; i < 399; i++) session.Advance(0.02f);
            Assert.That(Soldiers(session).Length, Is.EqualTo(3), "补兵冷却未结束时不能复活");
            for (int i = 0; i < 3; i++) session.Advance(0.02f);
            Assert.That(Soldiers(session).Length, Is.EqualTo(4));
            Assert.That(Soldiers(session).All(u => u.Id != dead), Is.True, "补兵生成新身份，旧尸体继续走死亡表现");
            Assert.That(simulation.Deployments, Is.EqualTo(5));
        }

        [Test]
        public void BlockedExitWaitsWithoutLosingSlotsAndRestartRestoresGoldAndRoster()
        {
            var simulation = new Simulation { Blocked = true };
            using var session = Session(simulation);
            session.Restart();
            Assert.That(Soldiers(session), Is.Empty);
            for (int i = 0; i < 20; i++) session.Advance(0.02f);
            simulation.Blocked = false;
            session.Advance(0.02f);
            Assert.That(Soldiers(session).Length, Is.EqualTo(4));
            Assert.That(session.TryUpgradeBarracks("barracks", out _), Is.True);
            Assert.That(session.Gold, Is.EqualTo(850));
            Assert.That(session.BarracksCapacity("barracks"), Is.EqualTo(6));
            Assert.That(Soldiers(session).Length, Is.EqualTo(4));
            session.Restart();
            Assert.That(session.Gold, Is.EqualTo(1000));
            Assert.That(session.BarracksLevel("barracks"), Is.EqualTo(1));
            Assert.That(Soldiers(session).Length, Is.EqualTo(4));
        }

        [Test]
        public void PaidUpgradesExpandToSixThenEightAndInsufficientFundsDoNotChangeState()
        {
            var simulation = new Simulation();
            using var poor = Session(simulation, 100);
            poor.Restart();
            Assert.That(poor.TryUpgradeBarracks("barracks", out _), Is.False);
            Assert.That(poor.Gold, Is.EqualTo(100));
            Assert.That(poor.BarracksCapacity("barracks"), Is.EqualTo(4));
            using var session = Session(new Simulation());
            session.Restart();
            Assert.That(session.TryUpgradeBarracks("barracks", out _), Is.True);
            Assert.That(session.TryUpgradeBarracks("barracks", out _), Is.True);
            Assert.That(session.Gold, Is.EqualTo(600));
            Assert.That(session.BarracksCapacity("barracks"), Is.EqualTo(8));
            Assert.That(session.TryUpgradeBarracks("barracks", out _), Is.False);
            for (int i = 0; i < 402; i++) session.Advance(0.02f);
            Assert.That(Soldiers(session).Length, Is.EqualTo(8));
        }

        [Test]
        public void DestroyedBarracksDoesNotRetrainAndFailedConstructionDoesNotSpendGold()
        {
            var simulation = new Simulation();
            using var session = Session(simulation);
            session.Restart();
            simulation.FailNextUpdate = true;
            Assert.That(session.TryBuild("barracks.ranged", 17, 8, out _), Is.False);
            Assert.That(session.Gold, Is.EqualTo(1000));
            simulation.DestroyBarracks = true;
            simulation.Kill(Soldiers(session)[0].Id);
            for (int i = 0; i < 500; i++) session.Advance(0.02f);
            Assert.That(Soldiers(session).Length, Is.EqualTo(3));
            Assert.That(session.TryUpgradeBarracks("barracks", out _), Is.False);
        }

        [TestCase(UnitKind.Soldier)]
        [TestCase(UnitKind.Archer)]
        public void RealEcsSoldierTracksMovingHeroAndWaitsNearby(UnitKind kind)
        {
            var rules = Rules();
            var definition = new BattleDefinition(rules.Hero, rules.Goblin, rules.Runner, 0, 0, 30, 20, 0.02f, 1,
                heroSpawn: new MapPoint(4, 0), soldiers: rules.Soldiers);
            using var simulation = new EcsBattleSimulation();
            simulation.Reset(definition);
            Assert.That(simulation.TrySpawnSoldier(kind, new MapPoint(-3, 0), new MapPoint(-5, 0), out var id), Is.True);
            var units = new List<UnitSnapshot>();
            for (int i = 0; i < 200; i++) simulation.Step(0.02f, default);
            simulation.CopyUnits(units);
            var arrived = units.Single(u => u.Id == id);
            Assert.That(arrived.X, Is.GreaterThan(2), "应跟随英雄，而非走向旧驻守点");
            Assert.That(arrived.X, Is.LessThan(4 - rules.Hero.Radius - definition.GetUnit(kind).Radius));
            for (int i = 0; i < 30; i++) simulation.Step(0.02f, default);
            simulation.CopyUnits(units);
            Assert.That(units.Single(u => u.Id == id).X, Is.EqualTo(arrived.X).Within(0.01f));
            for (int i = 0; i < 80; i++) simulation.Step(0.02f, new MoveIntent(1, 0));
            for (int i = 0; i < 180; i++) simulation.Step(0.02f, default);
            simulation.CopyUnits(units);
            var followed = units.Single(u => u.Id == id);
            var hero = units.Single(u => u.Kind == UnitKind.Hero);
            Assert.That(followed.X, Is.GreaterThan(arrived.X + 2));
            Assert.That(Math.Abs(hero.X - followed.X), Is.LessThan(2));
            Assert.That(followed.LastAttackTime, Is.EqualTo(-100), "跟随不产生攻击事实");
        }

        [TestCase(UnitKind.Soldier)]
        [TestCase(UnitKind.Archer)]
        public void RealEcsSoldierFollowsHeroAndAttacksOnlyEnemiesInRange(UnitKind kind)
        {
            var rules = Rules();
            var definition = new BattleDefinition(rules.Hero, rules.Goblin, rules.Runner, 0, 0, 24, 16, 0.02f, 1,
                heroSpawn: new MapPoint(7, 0), soldiers: rules.Soldiers);
            using var simulation = new EcsBattleSimulation();
            simulation.Reset(definition);
            Assert.That(simulation.TrySpawnSoldier(kind, new MapPoint(2, 0), new MapPoint(4, 0), out var id), Is.True);
            var units = new List<UnitSnapshot>();
            for (int i = 0; i < 55; i++) simulation.Step(0.02f, default);
            simulation.CopyUnits(units);
            Assert.That(units.Single(u => u.Id == id).X, Is.GreaterThan(3.5f));
            float enemyX = kind == UnitKind.Soldier ? units.Single(u => u.Id == id).X - 1 : 6;
            simulation.SpawnEnemies(new[] { new MapPoint(enemyX, 0) }, 1);
            bool attacked = false;
            for (int i = 0; i < 300; i++)
            {
                float previousX = units.Single(u => u.Id == id).X;
                simulation.Step(0.02f, default);
                simulation.CopyUnits(units);
                var soldier = units.Single(u => u.Id == id);
                var enemy = units.Single(u => UnitKinds.IsEnemy(u.Kind));
                if (soldier.LastAttackTime < 0) continue;
                float distance = (float)Math.Sqrt(Math.Pow(soldier.X - enemy.X, 2) + Math.Pow(soldier.Y - enemy.Y, 2));
                Assert.That(distance, Is.LessThanOrEqualTo(definition.GetUnit(kind).AttackRange + definition.GetUnit(kind).Radius + rules.Goblin.Radius + 0.05f));
                Assert.That(enemy.Health, Is.LessThan(rules.Goblin.MaxHealth));
                if (kind == UnitKind.Archer)
                    Assert.That(soldier.X, Is.EqualTo(previousX).Within(0.001f), "射程内有敌人时停步战斗");
                attacked = true;
                break;
            }
            Assert.That(attacked, Is.True);
            Assert.That(units.Single(u => u.Kind == UnitKind.Hero).Health, Is.EqualTo(rules.Hero.MaxHealth));
            for (int i = 0; i < 500 && units.Any(u => UnitKinds.IsEnemy(u.Kind) && u.Health > 0); i++)
            {
                simulation.Step(0.02f, default);
                simulation.CopyUnits(units);
            }
            Assert.That(units.Any(u => UnitKinds.IsEnemy(u.Kind) && u.Health > 0), Is.False);
            float combatX = units.Single(u => u.Id == id).X;
            for (int i = 0; i < 50; i++) simulation.Step(0.02f, new MoveIntent(1, 0));
            for (int i = 0; i < 100; i++) simulation.Step(0.02f, default);
            simulation.CopyUnits(units);
            Assert.That(units.Single(u => u.Id == id).Health, Is.GreaterThan(0));
            Assert.That(units.Single(u => u.Id == id).X, Is.GreaterThan(combatX + 0.5f), "战斗结束后恢复跟随英雄");
        }

        [Test]
        public void RemovedBarracksCannotBeUpgradedOrRetrainItsDeadSoldier()
        {
            var simulation = new Simulation();
            using var session = Session(simulation);
            session.Restart();
            simulation.Kill(Soldiers(session)[0].Id);
            Assert.That(session.TryRemove(-1, 1, out _), Is.True);
            Assert.That(session.TryUpgradeBarracks("barracks", out _), Is.False);
            Assert.That(session.Gold, Is.EqualTo(1000));
            for (int i = 0; i < 500; i++) session.Advance(0.02f);
            Assert.That(Soldiers(session).Length, Is.EqualTo(3));
        }

        [Test]
        public void RosterCannotApplyUpgradeTwiceOrResetAnExistingSoldiersTraining()
        {
            var roster = new BarracksRoster("test-barracks", "barracks.melee", 4);
            roster.Assign(0, 11);
            roster.ObserveDeath(0, 1, 8);
            roster.ObserveDeath(0, 5, 8);
            Assert.That(roster.IsReady(0, 9), Is.True);
            var upgrade = Construction().Upgrades["upgrade.barracks.melee.2"];
            roster.Upgrade(upgrade, 5, 8);
            Assert.That(roster.IsReady(0, 9), Is.True);
            Assert.That(roster.IsReady(4, 12), Is.False);
            Assert.That(roster.CanUpgrade(upgrade), Is.False);
        }

        private sealed class Simulation : IDefenseBattleSimulation
        {
            private readonly List<UnitSnapshot> _units = new List<UnitSnapshot>();
            private readonly List<DefenseStructureDefinition> _structures = new List<DefenseStructureDefinition>();
            private ulong _nextId;
            public bool Blocked, DestroyBarracks, FailNextUpdate;
            public int Deployments;
            public double ElapsedTime { get; private set; }
            public int ContactCount => 0;
            public void Reset(BattleDefinition definition)
            {
                ElapsedTime = 0;
                _nextId = 3;
                _units.Clear();
                _units.Add(new UnitSnapshot(1, UnitKind.Hero, definition.HeroSpawn.X, definition.HeroSpawn.Y, 1, 100, 100, -100));
                _units.Add(new UnitSnapshot(2, UnitKind.Goblin, 10.5f, 6.5f, 1, 100, 100, -100));
                DestroyBarracks = false;
                Deployments = 0;
            }
            public void Kill(ulong id)
            {
                int index = _units.FindIndex(u => u.Id == id);
                var u = _units[index];
                _units[index] = new UnitSnapshot(id, u.Kind, u.X, u.Y, 1, 0, u.MaxHealth, (float)ElapsedTime);
            }
            public void Step(float deltaTime, MoveIntent intent) => ElapsedTime += deltaTime;
            public void CopyUnits(List<UnitSnapshot> destination) { destination.Clear(); destination.AddRange(_units); }
            public bool TrySpawnSoldier(UnitKind kind, MapPoint exit, MapPoint rally, out ulong unitId)
            {
                unitId = 0;
                if (Blocked) return false;
                unitId = _nextId++;
                _units.Add(new UnitSnapshot(unitId, kind, exit.X, exit.Y, 1, 100, 100, -100));
                Deployments++;
                return true;
            }
            public void UpdateNavigationMap(GridMap map, NavigationDefinition navigation, GridMap friendlyMap = null)
            {
                if (FailNextUpdate) { FailNextUpdate = false; throw new InvalidOperationException("Spatial update failed"); }
            }
            public void SynchronizeStructures(IReadOnlyList<DefenseStructureDefinition> structures) { _structures.Clear(); _structures.AddRange(structures); }
            public void SpawnEnemies(IReadOnlyList<MapPoint> entries, int countPerEntry) { }
            public void CopyStructures(List<DefenseStructureSnapshot> destination)
            {
                destination.Clear();
                foreach (var structure in _structures)
                    destination.Add(new DefenseStructureSnapshot(structure.Id, DestroyBarracks && structure.Id == "barracks" ? 0 : structure.MaxHealth, structure.IsMainCastle));
            }
            public void RecordExternalAttack(ulong attackerId, MapPoint target) { }
            public void Dispose() { }
        }
    }
}
