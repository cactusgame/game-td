using System.Collections.Generic;
using Game.Battle.Application.Sessions;
using Game.Battle.Contracts.Commands;
using Game.Battle.Contracts.Ports;
using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.Definitions;
using Game.Battle.Domain.ValueObjects;
using Game.Battle.Simulation.Bridges;
using Game.MapEngine.Domain;
using NUnit.Framework;

namespace Game.Battle.Tests
{
    public sealed class PlayerOperationTests
    {
        private static BattleDefinition Definition(bool enemies = false, float selectionRadius = 4)
        {
            var hero = new UnitDefinition(UnitKind.Hero, 1000, 0, 0, 4, 0.3f, 1, skills: new[]
            {
                new HeroAreaSkillDefinition("burst", true, 4, 30, 3),
                new HeroAreaSkillDefinition("heavy", true, 6, 60, 8)
            });
            var goblin = new UnitDefinition(UnitKind.Goblin, 100, 0, 0, 0.01f, 0.2f, 1);
            var runner = new UnitDefinition(UnitKind.Runner, 100, 0, 0, 0.01f, 0.2f, 1);
            return new BattleDefinition(hero, goblin, runner, enemies ? 2 : 0, 0, 40, 40, 0.02f, 7,
                enemySpawns: new[] { new MapPoint(2, 0), new MapPoint(8, 0) }, soldiers: new[]
                {
                    new UnitDefinition(UnitKind.Soldier, 100, 0, 0, 2, 0.2f, 1),
                    new UnitDefinition(UnitKind.Archer, 100, 0, 0, 2, 0.2f, 1)
                }, soldierSelectionRadius: selectionRadius);
        }
        private static List<UnitSnapshot> Units(EcsBattleSimulation simulation)
        {
            var units = new List<UnitSnapshot>();
            simulation.CopyUnits(units);
            return units;
        }
        private static ulong Spawn(EcsBattleSimulation simulation, UnitKind kind, float x, float y = 0)
        {
            Assert.That(simulation.TrySpawnSoldier(kind, new MapPoint(x, y), new MapPoint(x, y), out ulong id), Is.True);
            return id;
        }
        private static UnitSnapshot Find(EcsBattleSimulation simulation, ulong id) => Units(simulation).Find(u => u.Id == id);

        [TestCase(PlayerOperation.NearbyTap, 2, 0)]
        [TestCase(PlayerOperation.NearbyHold, 2, 0)]
        [TestCase(PlayerOperation.NearbyTap, 6, 2)]
        [TestCase(PlayerOperation.NearbyHold, 6, 2)]
        public void NearbySelection_UsesConfiguredRadiusForTapHoldAndWhiteCircle(PlayerOperation operation, float radius, int expected)
        {
            using var simulation = new EcsBattleSimulation();
            simulation.Reset(Definition(selectionRadius: radius));
            Spawn(simulation, UnitKind.Soldier, 3);
            Spawn(simulation, UnitKind.Archer, 5);
            simulation.Execute(operation);
            Assert.That(simulation.PlayerControls.SelectionRadius, Is.EqualTo(radius));
            Assert.That(simulation.PlayerControls.SelectedCount, Is.EqualTo(expected));
        }

        [Test]
        public void PressGesture_ShortHoldLowFrameRateAndResetAreExclusive()
        {
            var gesture = new PressGesture();
            Assert.That(gesture.Update(true, 0), Is.Zero);
            Assert.That(gesture.Update(false, 0.1), Is.EqualTo(1));
            gesture.Update(true, 1);
            Assert.That(gesture.Update(true, 1.5), Is.EqualTo(2));
            Assert.That(gesture.Update(true, 2), Is.Zero);
            Assert.That(gesture.Update(false, 2.1), Is.EqualTo(3));
            gesture.Update(true, 3);
            Assert.That(gesture.Update(false, 4), Is.EqualTo(4));
            gesture.Update(true, 5);
            gesture.Reset();
            Assert.That(gesture.Update(false, 6), Is.Zero);
        }

        [Test]
        public void Skill_DamagesOnlyNearbyEnemiesHonorsIndependentCooldownAndRestart()
        {
            using var simulation = new EcsBattleSimulation();
            simulation.Reset(Definition(true));
            ulong ally = Spawn(simulation, UnitKind.Soldier, 1, 1);
            simulation.Execute(PlayerOperation.SkillOne);
            Assert.That(Find(simulation, 2).Health, Is.EqualTo(70));
            Assert.That(Find(simulation, 3).Health, Is.EqualTo(100));
            Assert.That(Find(simulation, ally).Health, Is.EqualTo(100));
            simulation.Execute(PlayerOperation.SkillOne);
            Assert.That(Find(simulation, 2).Health, Is.EqualTo(70));
            simulation.Execute(PlayerOperation.SkillTwo);
            Assert.That(Find(simulation, 2).Health, Is.EqualTo(10));
            for (int i = 0; i < 151; i++) simulation.Step(0.02f, default);
            simulation.Execute(PlayerOperation.SkillOne);
            Assert.That(Find(simulation, 2).Health, Is.Zero);
            Assert.That(simulation.PlayerControls.SkillTwoRemaining, Is.GreaterThan(0));
            simulation.Reset(Definition(true));
            Assert.That(simulation.PlayerControls.SkillOneRemaining, Is.Zero);
            simulation.Execute(PlayerOperation.SkillOne);
            Assert.That(Find(simulation, 2).Health, Is.EqualTo(70));
        }

        [Test]
        public void NearbyTap_StartsFollowingAndSecondTapStationsAtCurrentPosition()
        {
            using var simulation = new EcsBattleSimulation();
            simulation.Reset(Definition());
            ulong near = Spawn(simulation, UnitKind.Soldier, 3);
            ulong far = Spawn(simulation, UnitKind.Archer, 9);
            simulation.Execute(PlayerOperation.NearbyTap);
            Assert.That(Find(simulation, near).IsSelected, Is.True);
            Assert.That(Find(simulation, far).IsSelected, Is.False);
            for (int i = 0; i < 100; i++) simulation.Step(0.02f, new MoveIntent(0, 1));
            for (int i = 0; i < 300; i++) simulation.Step(0.02f, default);
            Assert.That(Find(simulation, near).Y, Is.GreaterThan(6));
            var stopped = Find(simulation, near);
            simulation.Execute(PlayerOperation.NearbyTap);
            Assert.That(simulation.PlayerControls.SelectedCount, Is.Zero);
            for (int i = 0; i < 100; i++) simulation.Step(0.02f, new MoveIntent(0, -1));
            Assert.That(Find(simulation, near).X, Is.EqualTo(stopped.X).Within(0.001));
            Assert.That(Find(simulation, near).Y, Is.EqualTo(stopped.Y).Within(0.001));
            simulation.Execute(PlayerOperation.CallAllRanged, true);
            Assert.That(System.Math.Abs(Find(simulation, far).X), Is.LessThan(3));
            Assert.That(Find(simulation, far).IsSelected, Is.True);
        }

        [Test]
        public void MovingSelection_RecruitsFollowDuringAndAfterHold_ExistingSelectionHoldStations()
        {
            using var simulation = new EcsBattleSimulation();
            simulation.Reset(Definition());
            ulong first = Spawn(simulation, UnitKind.Soldier, 2);
            ulong second = Spawn(simulation, UnitKind.Archer, 6);
            simulation.Execute(PlayerOperation.NearbyHold);
            Assert.That(simulation.PlayerControls.IsSelectingWhileMoving, Is.True);
            Assert.That(simulation.PlayerControls.SelectedCount, Is.EqualTo(1));
            for (int i = 0; i < 35; i++) simulation.Step(0.02f, new MoveIntent(1, 0));
            Assert.That(Find(simulation, second).IsSelected, Is.True);
            simulation.Execute(PlayerOperation.NearbyRelease, true);
            Assert.That(simulation.PlayerControls.IsSelectingWhileMoving, Is.False);
            Assert.That(simulation.PlayerControls.SelectedCount, Is.EqualTo(2));
            for (int i = 0; i < 100; i++) simulation.Step(0.02f, new MoveIntent(0, 1));
            for (int i = 0; i < 300; i++) simulation.Step(0.02f, default);
            Assert.That(Find(simulation, first).Y, Is.GreaterThan(6));
            Assert.That(Find(simulation, second).Y, Is.GreaterThan(6));
            var stationed = Find(simulation, first);
            simulation.Execute(PlayerOperation.NearbyHold);
            Assert.That(simulation.PlayerControls.IsSelectingWhileMoving, Is.False);
            Assert.That(simulation.PlayerControls.SelectedCount, Is.Zero);
            for (int i = 0; i < 30; i++) simulation.Step(0.02f, new MoveIntent(-1, 0));
            Assert.That(Find(simulation, first).X, Is.EqualTo(stationed.X).Within(0.2));
            Assert.That(Find(simulation, first).Y, Is.EqualTo(stationed.Y).Within(0.001));
        }

        [Test]
        public void NumberCalls_FilterTypeAndCallOnlyOneOrAll()
        {
            using var simulation = new EcsBattleSimulation();
            simulation.Reset(Definition());
            Spawn(simulation, UnitKind.Soldier, 8);
            Spawn(simulation, UnitKind.Soldier, 10);
            ulong archer = Spawn(simulation, UnitKind.Archer, 12);
            simulation.Execute(PlayerOperation.CallOneMelee, true);
            Assert.That(Units(simulation).FindAll(u => u.Kind == UnitKind.Soldier && System.Math.Abs(u.X) < 4).Count, Is.EqualTo(1));
            Assert.That(Find(simulation, archer).X, Is.EqualTo(12));
            simulation.Execute(PlayerOperation.CallAll, true);
            Assert.That(Units(simulation).FindAll(u => UnitKinds.IsSoldier(u.Kind) && System.Math.Abs(u.X) < 4).Count, Is.EqualTo(3));
            Assert.That(simulation.PlayerControls.SelectedCount, Is.EqualTo(3));
        }

        [TestCase(PlayerOperation.CallOne, 1)]
        [TestCase(PlayerOperation.CallAll, 3)]
        [TestCase(PlayerOperation.CallOneMelee, 1)]
        [TestCase(PlayerOperation.CallAllMelee, 2)]
        [TestCase(PlayerOperation.CallOneRanged, 1)]
        [TestCase(PlayerOperation.CallAllRanged, 1)]
        public void EveryNumberCall_KeepsSelectionAndTracksHeroAfterArrival(PlayerOperation operation, int expected)
        {
            using var simulation = new EcsBattleSimulation();
            simulation.Reset(Definition());
            Spawn(simulation, UnitKind.Soldier, 8);
            Spawn(simulation, UnitKind.Soldier, 10);
            Spawn(simulation, UnitKind.Archer, 12);
            simulation.Execute(operation, true);
            Assert.That(simulation.PlayerControls.SelectedCount, Is.EqualTo(expected));
            for (int i = 0; i < 75; i++) simulation.Step(0.02f, new MoveIntent(0, -1));
            for (int i = 0; i < 300; i++) simulation.Step(0.02f, default);
            foreach (var unit in Units(simulation))
            {
                if (unit.IsSelected) Assert.That(unit.Y, Is.LessThan(-4));
                else if (UnitKinds.IsSoldier(unit.Kind)) Assert.That(unit.Y, Is.EqualTo(0).Within(0.001));
            }
        }

        [Test]
        public void RepeatedSingleCalls_AddSoldiersAndCancelStopsAnUnfinishedFollowPath()
        {
            using var simulation = new EcsBattleSimulation();
            simulation.Reset(Definition());
            ulong first = Spawn(simulation, UnitKind.Soldier, 8);
            ulong second = Spawn(simulation, UnitKind.Soldier, 10);
            simulation.Execute(PlayerOperation.CallOneMelee);
            simulation.Execute(PlayerOperation.CallOneMelee);
            Assert.That(simulation.PlayerControls.SelectedCount, Is.EqualTo(2));
            for (int i = 0; i < 10; i++) simulation.Step(0.02f, default);
            var firstStopped = Find(simulation, first);
            var secondStopped = Find(simulation, second);
            simulation.Execute(PlayerOperation.NearbyTap);
            for (int i = 0; i < 100; i++) simulation.Step(0.02f, new MoveIntent(0, 1));
            Assert.That(Find(simulation, first).X, Is.EqualTo(firstStopped.X).Within(0.001));
            Assert.That(Find(simulation, second).X, Is.EqualTo(secondStopped.X).Within(0.001));
            Assert.That(simulation.PlayerControls.SelectedCount, Is.Zero);
        }

        [Test]
        public void OperationQueuedDuringZeroStepFrame_IsConsumedOnlyOnceAcrossCatchup()
        {
            using var simulation = new EcsBattleSimulation();
            var input = new OperationInput();
            var session = new BattleSession(new Source(Definition(true)), simulation, input);
            session.Restart();
            input.Next = PlayerOperation.SkillOne;
            session.Advance(0.005f);
            Assert.That(Find(simulation, 2).Health, Is.EqualTo(100));
            session.Advance(0.08f);
            Assert.That(Find(simulation, 2).Health, Is.EqualTo(70));
        }
        private sealed class Source : IBattleDefinitionSource
        {
            private readonly BattleDefinition _definition;
            public Source(BattleDefinition definition) { _definition = definition; }
            public BattleDefinition Load() => _definition;
        }
        private sealed class OperationInput : IHeroInput, IPlayerOperationInput
        {
            public PlayerOperation? Next;
            public MoveIntent Read() => default;
            public void ResetOperations() { Next = null; }
            public void ReadOperations(List<PlayerOperation> destination)
            {
                if (Next.HasValue) destination.Add(Next.Value);
                Next = null;
            }
        }
    }
}
