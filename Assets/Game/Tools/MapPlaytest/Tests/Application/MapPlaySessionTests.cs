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
            map.facilities.Add(new InitialFacility { id = "castle", definitionId = "hall", x = 1, y = 1 });
            return map;
        }

        private static MapContentCatalog Catalog() => new MapContentCatalog(new[]
        {
            new FacilityDefinition { id = "tower", width = 1, height = 1 },
            new FacilityDefinition { id = "hall", width = 2, height = 2, maxHealth = 500 }
        });

        private static BattleDefinition Rules() => JsonBattleDefinitionSource.Parse(File.ReadAllText("Assets/Content/Configuration/Resources/GameConfig/battle.json"));

        [Test]
        public void ConfiguredSelectionRadius_ReachesMapSessionAndCircleSnapshot()
        {
            string json = File.ReadAllText("Assets/Content/Configuration/Resources/GameConfig/battle.json");
            json = System.Text.RegularExpressions.Regex.Replace(json,
                "\"soldierSelectionRadius\"\\s*:\\s*[0-9.]+", "\"soldierSelectionRadius\": 6.5");
            var rules = JsonBattleDefinitionSource.Parse(json);
            var catalog = Catalog();
            using var session = new MapPlaySession(new MapDefinition(Source(), catalog), catalog, rules,
                new Input(), () => new EcsBattleSimulation());
            session.Restart();
            Assert.That(session.Definition.SoldierSelectionRadius, Is.EqualTo(6.5f));
            Assert.That(session.PlayerControls.SelectionRadius, Is.EqualTo(6.5f));
        }

        private static MapPlaySession Session(Func<ISpatialBattleSimulation> create, MapLayout source = null, IHeroInput input = null)
        {
            var catalog = Catalog();
            return new MapPlaySession(new MapDefinition(source ?? Source(), catalog), catalog, Rules(), input ?? new Input(), create);
        }

        /// <summary>验证表现开门、敌方阻挡、破门空间提交和源地图隔离。</summary>
        /// <summary>沿真实 ECS 移动和攻城路径验证敌人不能穿过完整门，破门后继续追击。</summary>
        [Test]
        public void RealEcsEnemy_StopsAttacksAndCrossesOnlyAfterGateDestruction()
        {
            var source = Source();
            source.markers.Find(marker => marker.kind == MarkerKind.HeroSpawn).x = 0.5f;
            source.markers.Find(marker => marker.kind == MarkerKind.EnemyEntry).x = 4.5f;
            source.wallNodes.Add(new WallNode { id = "a", x = 2.5f, y = -7.5f });
            source.wallNodes.Add(new WallNode { id = "b", x = 2.5f, y = 7.5f });
            source.wallEdges.Add(new WallEdge { id = "vertical", from = "a", to = "b",
                gates = new List<WallGate> { new WallGate { id = "door", startTile = 6, isOpen = true } } });
            using var session = Session(() => new EcsBattleSimulation(), source);
            session.Restart();
            bool damaged = false;
            bool crossed = false;
            int initialHealth = session.GetGateHealth("door");
            for (int i = 0; i < 5000; i++)
            {
                session.Advance(0.02f);
                var enemies = session.Units.Where(unit => unit.Kind != UnitKind.Hero && unit.Health > 0).ToArray();
                int health = session.GetGateHealth("door");
                if (health < initialHealth && !damaged)
                {
                    Assert.That(enemies.Any(enemy => System.Math.Abs(enemy.LastAttackTime - session.SimulationTime) < 0.0001), Is.True,
                        "破门伤害必须在同一步发布可播放的攻击时点");
                    Assert.That(enemies.Where(enemy => System.Math.Abs(enemy.LastAttackTime - session.SimulationTime) < 0.0001)
                        .All(enemy => enemy.FacingX < 0), Is.True, "攻击时应朝向城门");
                }
                if (health > 0)
                    Assert.That(enemies.All(enemy => enemy.X > 2.5f), Is.True);
                damaged |= health < initialHealth;
                if (enemies.Any(enemy => enemy.X < 2))
                {
                    Assert.That(health, Is.Zero);
                    crossed = true;
                    break;
                }
            }
            Assert.That(damaged, Is.True, $"health={session.GetGateHealth("door")} enemy=({session.Units.Last().X}, {session.Units.Last().Y})");
            Assert.That(crossed, Is.True, $"health={session.GetGateHealth("door")} enemy=({session.Units.Last().X}, {session.Units.Last().Y})");
            session.Restart();
            Assert.That(session.GetGateHealth("door"), Is.EqualTo(initialHealth));
        }

        [Test]
        public void GatePassage_FriendlyOpensButEnemyOnlyPassesAfterDestruction()
        {
            var map = GateMap(true);
            var gates = new GatePassageSession(Catalog());
            var simulation = new Simulation();
            var rules = Rules();
            simulation.Reset(rules);
            var opening = new GridCell(12, 10);
            Assert.That(gates.Navigation(map, true).IsWalkable(opening), Is.True);
            Assert.That(gates.Navigation(map, false).IsWalkable(opening), Is.False);
            var units = new List<UnitSnapshot>
            {
                new UnitSnapshot(1, UnitKind.Hero, 0.5f, 2.5f, 1, 100, 100, 0)
            };
            gates.Update(simulation, map, rules, units, 0, false);
            Assert.That(gates.Project(map.Copy()).wallEdges[0].gates[0].isOpen, Is.True);
            Assert.That(gates.Navigation(map, false).IsWalkable(opening), Is.False);
            int health = gates.Health("gate-main");
            units.Clear();
            units.Add(new UnitSnapshot(2, UnitKind.Goblin, 0.5f, 2.5f, 1, 100, 100, 0));
            gates.Update(simulation, map, rules, units, 1, true);
            Assert.That(gates.Health("gate-main"), Is.LessThan(health));
            Assert.That(simulation.Attacks, Is.EqualTo(new ulong[] { 2 }));
            int afterHit = gates.Health("gate-main");
            gates.Update(simulation, map, rules, units, 1, true);
            Assert.That(gates.Health("gate-main"), Is.EqualTo(afterHit));
            Assert.That(simulation.Attacks.Count, Is.EqualTo(1), "冷却期不能每步重启动作");
            for (int i = 2; i < 2000 && gates.Health("gate-main") > 0; i++)
                gates.Update(simulation, map, rules, units, i * 10, true);
            Assert.That(gates.Health("gate-main"), Is.Zero);
            Assert.That(gates.Navigation(map, false).IsWalkable(opening), Is.True);
            Assert.That(gates.Project(map.Copy()).wallEdges[0].gates[0].isDestroyed, Is.True);
            Assert.That(new GatePassageSession(Catalog()).Health("gate-main"), Is.EqualTo(health));
            Assert.That(map.wallEdges[0].gates[0].isDestroyed, Is.False);
            int attackCount = simulation.Attacks.Count;
            gates.Update(simulation, map, rules, units, 30000, true);
            Assert.That(simulation.Attacks.Count, Is.EqualTo(attackCount), "门毁后不再挥击");
        }

        [Test]
        public void FailedGateDestructionDoesNotPublishAttackOrLoseItsCooldown()
        {
            var catalog = new MapContentCatalog(Array.Empty<FacilityDefinition>(),
                new Game.Fortifications.Domain.Definitions.WallDefinition(0.52f, 0.5f, 0.4f, gateHealth: 1));
            var gates = new GatePassageSession(catalog);
            var simulation = new Simulation();
            var rules = Rules();
            simulation.Reset(rules);
            var units = new[] { new UnitSnapshot(2, UnitKind.Goblin, 0.5f, 2.5f, 1, 100, 100, -100) };
            simulation.FailNextUpdate = true;
            Assert.Throws<InvalidOperationException>(() => gates.Update(simulation, GateMap(), rules, units, 1, true));
            Assert.That(gates.Health("gate-main"), Is.EqualTo(1));
            Assert.That(simulation.Attacks, Is.Empty);
            gates.Update(simulation, GateMap(), rules, units, 1, true);
            Assert.That(gates.Health("gate-main"), Is.Zero);
            Assert.That(simulation.Attacks, Is.EqualTo(new ulong[] { 2 }));
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
            Assert.That(source.facilities.Select(f => f.definitionId), Is.EqualTo(new[] { "hall" }));
            Assert.That(session.TryRemove(-7.5f, -3.5f, out _), Is.True);
            Assert.That(session.MapVersion, Is.EqualTo(2));
            Assert.That(simulation.Navigation.IsWalkable(new GridCell(4, 4)), Is.True);
            Assert.That(session.Snapshot.facilities.Select(f => f.definitionId), Is.EqualTo(new[] { "hall" }));
        }

        private static MapLayout GateMap(bool open = false)
        {
            var map = Source();
            map.wallNodes.Add(new WallNode { id = "a", x = -8.5f, y = 2.5f });
            map.wallNodes.Add(new WallNode { id = "b", x = 9.5f, y = 2.5f });
            map.wallEdges.Add(new WallEdge { id = "gate-wall", from = "a", to = "b", gates = new System.Collections.Generic.List<WallGate> { new WallGate { id = "gate-main", isOpen = open } } });
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
            Assert.That(session.TrySetGateOpen("gate-main", true, out _), Is.True);
            Assert.That(session.Navigation.IsWalkable(opening), Is.False);
            Assert.That(session.Navigation.IsWalkable(frame), Is.False);
            Assert.That(simulation.Navigation, Is.SameAs(session.Navigation));
            Assert.That(source.wallEdges[0].gates[0].isOpen, Is.False);
            var snapshot = session.Snapshot;
            snapshot.wallEdges[0].gates[0].isOpen = false;
            Assert.That(session.Snapshot.wallEdges[0].gates[0].isOpen, Is.False);
            int version = session.MapVersion;
            Assert.That(session.TrySetGateOpen("gate-main", true, out _), Is.True);
            Assert.That(session.MapVersion, Is.EqualTo(version));
            session.Restart();
            Assert.That(session.Snapshot.wallEdges[0].gates[0].isOpen, Is.False);
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
            Assert.That(session.TrySetGateOpen("gate-main", false, out var reason), Is.False);
            Assert.That(reason, Does.Contain("单位"));
            Assert.That(session.MapVersion, Is.Zero);
            Assert.That(simulation.UpdateCount, Is.EqualTo(1));
            Assert.That(session.Snapshot.wallEdges[0].gates[0].isOpen, Is.True);
            simulation.HeroPosition = new MapPoint(-5.5f, 0.5f);
            session.Advance(0);
            simulation.FailNextUpdate = true;
            Assert.That(session.TrySetGateOpen("gate-main", false, out reason), Is.False);
            Assert.That(reason, Does.Contain("空间同步失败"));
            Assert.That(session.MapVersion, Is.Zero);
            Assert.That(session.Snapshot.wallEdges[0].gates[0].isOpen, Is.False);
            Assert.That(simulation.Navigation, Is.SameAs(session.Navigation));
            Assert.That(session.TrySetGateOpen("gate-main", false, out _), Is.True);
            Assert.That(session.Snapshot.wallEdges[0].gates[0].isOpen, Is.False);
        }

        [Test]
        public void RealEcsHeroCrossesAutomaticallyWithoutWaiting()
        {
            var source = GateMap();
            source.markers.Find(marker => marker.kind == MarkerKind.HeroSpawn).x = 0.5f;
            var input = new Input { Y = 1 };
            int worlds = World.All.Count;
            using (var session = Session(() => new EcsBattleSimulation(), source, input))
            {
                session.Restart();
                var navigation = session.Navigation;
                for (int i = 0; i < 60; i++)
                {
                    float previous = session.Units.First(unit => unit.Kind == UnitKind.Hero).Y;
                    session.Advance(0.02f);
                    float current = session.Units.First(unit => unit.Kind == UnitKind.Hero).Y;
                    Assert.That(current - previous, Is.EqualTo(session.Definition.Hero.Speed * 0.02f).Within(0.0001f),
                        $"英雄在第 {i} 步过门时减速");
                    Assert.That(session.Navigation, Is.SameAs(navigation));
                }
                Assert.That(session.Units.First(unit => unit.Kind == UnitKind.Hero).Y, Is.GreaterThan(3.5f));
            }
            Assert.That(World.All.Count, Is.EqualTo(worlds));
        }

        [TestCase(UnitKind.Soldier, false, false)]
        [TestCase(UnitKind.Soldier, false, true)]
        [TestCase(UnitKind.Soldier, true, false)]
        [TestCase(UnitKind.Soldier, true, true)]
        [TestCase(UnitKind.Archer, false, false)]
        [TestCase(UnitKind.Archer, false, true)]
        [TestCase(UnitKind.Archer, true, false)]
        [TestCase(UnitKind.Archer, true, true)]
        public void FriendlySoldierCrossesIntactGateInEitherDirectionWithoutStoppingOrDamagingIt(UnitKind kind, bool vertical, bool reverse)
        {
            var map = Source();
            map.wallNodes.Add(new WallNode { id = "a", x = vertical ? 2.5f : -6.5f, y = vertical ? -6.5f : 2.5f });
            map.wallNodes.Add(new WallNode { id = "b", x = vertical ? 2.5f : 6.5f, y = vertical ? 6.5f : 2.5f });
            map.wallEdges.Add(new WallEdge { id = "wall", from = "a", to = "b",
                gates = new List<WallGate> { new WallGate { id = "door", startTile = 5, isOpen = false } } });
            var hero = map.markers.Find(marker => marker.kind == MarkerKind.HeroSpawn);
            float heroAxis = reverse ? -1.5f : 6.5f;
            hero.x = vertical ? heroAxis : 0.5f;
            hero.y = vertical ? 0.5f : heroAxis;
            var catalog = Catalog();
            var definition = new Game.Tools.MapPlaytest.Application.Adapters.MapBattleDefinitionSource(
                new MapDefinition(map, catalog), catalog, Rules()).Load();
            var gates = new GatePassageSession(catalog);
            using var simulation = new EcsBattleSimulation();
            simulation.Reset(definition);
            gates.Synchronize(simulation, map, definition);
            var before = new MapPoint(0.5f, 0.5f);
            var after = vertical ? new MapPoint(4.5f, 0.5f) : new MapPoint(0.5f, 4.5f);
            var exit = reverse ? after : before;
            var rally = reverse ? before : after;
            Assert.That(simulation.TrySpawnSoldier(kind, exit, rally, out ulong id), Is.True);
            var units = new List<UnitSnapshot>();
            bool openedForSoldier = false;
            float previous = vertical ? exit.X : exit.Y;
            for (int i = 0; i < 140; i++)
            {
                simulation.Step(0.02f, default);
                simulation.CopyUnits(units);
                gates.Update(simulation, map, definition, units, simulation.ElapsedTime, true);
                openedForSoldier |= gates.Project(map.Copy()).wallEdges[0].gates[0].isOpen;
                var soldier = units.Single(u => u.Id == id);
                float current = vertical ? soldier.X : soldier.Y;
                float goal = vertical ? rally.X : rally.Y;
                float step = definition.GetUnit(kind).Speed * 0.02f;
                if ((reverse ? previous - goal : goal - previous) > step + 0.08f)
                    Assert.That(Math.Abs(current - previous), Is.EqualTo(step).Within(0.001f), "我方士兵过门不能等待铁栅动画或使用敌方碰撞");
                previous = current;
            }
            var final = units.Single(u => u.Id == id);
            float finalAxis = vertical ? final.X : final.Y;
            Assert.That(reverse ? finalAxis < 1 : finalAxis > 4, Is.True, "跟随英雄穿过门洞，到达城门另一侧");
            Assert.That(openedForSoldier, Is.True);
            Assert.That(gates.Health("door"), Is.EqualTo(catalog.Wall.GateHealth));
            Assert.That(gates.SpatialVersion, Is.Zero, "己方通行不需要破门或反复重建空间");
            Assert.That(final.LastAttackTime, Is.EqualTo(-100));
            var enemyMap = gates.Navigation(map, false);
            var gateCenter = vertical ? new MapPoint(2.5f, 0.5f) : new MapPoint(0.5f, 2.5f);
            Assert.That(enemyMap.TryWorldToCell(gateCenter.X, gateCenter.Y, out var doorCell), Is.True);
            Assert.That(gates.Navigation(map, true).IsWalkable(doorCell), Is.True);
            Assert.That(enemyMap.IsWalkable(doorCell), Is.False,
                "放行己方不能同时给敌方开放完整城门");
        }

        [TestCase(UnitKind.Hero)]
        [TestCase(UnitKind.Soldier)]
        [TestCase(UnitKind.Archer)]
        public void AllFriendlyUnitsUseTheSameAutomaticGateOpeningRule(UnitKind kind)
        {
            var map = GateMap();
            var gates = new GatePassageSession(Catalog());
            var simulation = new Simulation();
            var rules = Rules();
            simulation.Reset(rules);
            var stats = rules.GetUnit(kind);
            float threshold = Catalog().Wall.Width * 0.5f + stats.Radius + stats.Speed * rules.FixedStep + 0.5f;
            var near = new[] { new UnitSnapshot(99, kind, 0.5f, 2.5f - threshold + 0.01f, 1, 100, 100, -100) };
            gates.Update(simulation, map, rules, near, 0, true);
            Assert.That(gates.Project(map.Copy()).wallEdges[0].gates[0].isOpen, Is.True);
            var far = new[] { new UnitSnapshot(99, kind, 0.5f, 2.5f - threshold - 0.01f, 1, 100, 100, -100) };
            gates.Update(simulation, map, rules, far, 0, true);
            Assert.That(gates.Project(map.Copy()).wallEdges[0].gates[0].isOpen, Is.False);
            Assert.That(simulation.Attacks, Is.Empty);
            Assert.That(gates.Health("gate-main"), Is.EqualTo(Catalog().Wall.GateHealth));
        }

        [Test]
        public void RuntimePlaceGate_SpatialFailure_DoesNotPublishDoorOrItsIdentity()
        {
            var source = GateMap();
            source.wallEdges[0].gates.Clear();
            var simulation = new Simulation();
            using var session = Session(() => simulation, source);
            session.Restart();
            simulation.FailNextUpdate = true;
            Assert.That(session.TryPlaceGate("gate-wall", 3, 4, 2, true, out var id, out _), Is.False);
            Assert.That(id, Is.Null);
            Assert.That(session.Snapshot.wallEdges[0].gates, Is.Empty);
            Assert.That(session.MapVersion, Is.Zero);
            Assert.That(session.TryPlaceGate("gate-wall", 3, 4, 2, true, out id, out _), Is.True);
            Assert.That(id, Is.Not.Empty);
            Assert.That(session.Snapshot.wallEdges[0].gates.Single().id, Is.EqualTo(id));
            Assert.That(source.wallEdges[0].gates, Is.Empty);
        }

        [Test]
        public void TwoGates_ClosingOne_DoesNotCloseOrRejectTheOtherOccupiedOpening()
        {
            var source = GateMap(true);
            source.wallEdges[0].gates.Add(new WallGate { id = "gate-other", startTile = 2, isOpen = true });
            var simulation = new Simulation();
            using var session = Session(() => simulation, source);
            session.Restart();
            simulation.HeroPosition = new MapPoint(-4.5f, 2.5f);
            session.Advance(0);
            Assert.That(session.TrySetGateOpen("gate-main", false, out var reason), Is.True, reason);
            Assert.That(session.Snapshot.wallEdges[0].gates[0].isOpen, Is.False);
            Assert.That(session.Snapshot.wallEdges[0].gates[1].isOpen, Is.True);
            Assert.That(session.TrySetGateOpen("gate-other", false, out reason), Is.False);
            Assert.That(reason, Does.Contain("单位"));
            Assert.That(session.MapVersion, Is.EqualTo(1));
            Assert.That(source.wallEdges[0].gates.All(gate => gate.isOpen), Is.True);
        }

        [Test]
        public void RealEcsHero_VerticalGate_CrossesAutomatically()
        {
            var source = Source();
            source.markers.Find(marker => marker.kind == MarkerKind.HeroSpawn).x = 0.5f;
            source.wallNodes.Add(new WallNode { id = "a", x = 2.5f, y = -6.5f });
            source.wallNodes.Add(new WallNode { id = "b", x = 2.5f, y = 6.5f });
            source.wallEdges.Add(new WallEdge { id = "vertical", from = "a", to = "b",
                gates = new List<WallGate> { new WallGate { id = "vertical-door", startTile = 5 } } });
            using var session = Session(() => new EcsBattleSimulation(), source, new Input { X = 1 });
            session.Restart();
            for (int i = 0; i < 60; i++)
                session.Advance(0.02f);
            Assert.That(session.Units.First(unit => unit.Kind == UnitKind.Hero).X, Is.GreaterThan(3.5f));
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
            Assert.That(session.Snapshot.facilities.Select(f => f.definitionId), Is.EqualTo(new[] { "hall" }));
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
            Assert.That(simulation.UpdateCount, Is.EqualTo(1));
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
                Assert.That(session.Snapshot.facilities.Select(f => f.definitionId), Is.EqualTo(new[] { "hall" }));
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
                Assert.That(session.Units.Count, Is.EqualTo(6));
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
            public float X;
            public float Y;
            public MoveIntent Read() => new MoveIntent(X, Y);
        }

        private sealed class Simulation : IDefenseBattleSimulation
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
            public readonly List<ulong> Attacks = new List<ulong>();
            public void RecordExternalAttack(ulong attackerId, MapPoint target) => Attacks.Add(attackerId);
            public bool TrySpawnSoldier(UnitKind kind, MapPoint exit, MapPoint rally, out ulong unitId) { unitId = 0; return false; }

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

            public void UpdateNavigationMap(GridMap map, NavigationDefinition navigation, GridMap friendlyMap = null)
            {
                UpdateCount++;
                Navigation = map;
                if (FailNextUpdate || FailEveryUpdate)
                {
                    FailNextUpdate = false;
                    throw new InvalidOperationException("Spatial replacement failed.");
                }
            }

            public void SynchronizeStructures(IReadOnlyList<DefenseStructureDefinition> structures) { }
            public void SpawnEnemies(IReadOnlyList<MapPoint> entries, int countPerEntry) { }
            public void CopyStructures(List<DefenseStructureSnapshot> destination) => destination.Clear();
            public void Dispose() => DisposeCount++;
        }
    }
}
