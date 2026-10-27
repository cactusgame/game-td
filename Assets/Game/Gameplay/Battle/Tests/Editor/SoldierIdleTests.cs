using System.Collections.Generic;
using Game.Battle.Contracts.Commands;
using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.Definitions;
using Game.Battle.Domain.ValueObjects;
using Game.Battle.Simulation.Bridges;
using Game.Battle.Simulation.Components;
using Game.Battle.Simulation.Systems;
using Game.MapEngine.Domain;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Tests
{
    public sealed class SoldierIdleTests
    {
        private World _world;
        private SoldierNavigationSystem _navigation;
        private EntityManager Em => _world.EntityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Soldier idle behavior tests");
            _world.SetTime(new TimeData(1, 0.02f));
            _navigation = _world.GetOrCreateSystemManaged<SoldierNavigationSystem>();
            _navigation.Configure(GridMap.CreateOpenArena(40, 40, 1));
            _navigation.DetectionRadius = 5;
            var hero = Spawn(new float2(-10, -10));
            Em.AddComponent<HeroTag>(hero);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private Entity Spawn(float2 position)
        {
            var entity = Em.CreateEntity(typeof(GroundTransform), typeof(Vitals), typeof(UnitMotion),
                typeof(CombatProfile), typeof(MonsterNavigationState), typeof(UnitIdentity), typeof(CombatTargetState));
            Em.SetComponentData(entity, new GroundTransform { Position = position, Previous = position });
            Em.SetComponentData(entity, new Vitals { Health = 100, Attack = 10, LastAttackTime = -100 });
            Em.SetComponentData(entity, new UnitMotion { Radius = 0.2f, Speed = 2 });
            Em.SetComponentData(entity, new CombatProfile { AttackRange = 0.15f, AttackInterval = 1 });
            Em.AddBuffer<NavigationWaypoint>(entity);
            return entity;
        }

        private void SelectTargets()
        {
            var targets = _world.GetOrCreateSystemManaged<CombatTargetSelectionSystem>();
            targets.SoldierDetectionRadius = _navigation.DetectionRadius;
            targets.Update();
        }

        private Entity Soldier(bool stationed = false)
        {
            var soldier = Spawn(float2.zero);
            Em.AddComponentData(soldier, new SoldierState { Stance = stationed ? CombatStance.HoldGround : CombatStance.Alert });
            Em.SetComponentData(soldier, new UnitIdentity { Id = 2, Kind = UnitKind.Soldier });
            return soldier;
        }

        private Entity Monster(float x)
        {
            var monster = Spawn(new float2(x, 0));
            Em.AddComponent<MonsterTag>(monster);
            Em.SetComponentData(monster, new UnitIdentity { Id = 3, Kind = UnitKind.Goblin });
            return monster;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Selection_WithoutFollowCommand_DoesNotMoveTowardHero(bool selected)
        {
            var soldier = Soldier();
            Em.SetComponentData(soldier, new SoldierState { IsSelected = selected });
            SelectTargets();
            _navigation.Update();
            Assert.That(Em.GetBuffer<NavigationWaypoint>(soldier).Length, Is.Zero);
            Assert.That(Em.GetComponentData<SoldierState>(soldier).Behavior, Is.EqualTo(UnitBehavior.Idle));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FollowCommand_IndependentOfSelection_UsesSameFormationTarget(bool selected)
        {
            var soldier = Soldier();
            Em.SetComponentData(soldier, new SoldierState
            {
                IsSelected = selected,
                Command = SoldierCommand.FollowHero,
                FollowOffset = new float2(1, 0)
            });
            SelectTargets();
            _navigation.Update();
            var path = Em.GetBuffer<NavigationWaypoint>(soldier);
            Assert.That(path[path.Length - 1].Position, Is.EqualTo(new float2(-9, -10)));
            var state = Em.GetComponentData<SoldierState>(soldier);
            Assert.That(state.Behavior, Is.EqualTo(UnitBehavior.Regrouping));
            Assert.That(state.IsSelected, Is.EqualTo(selected));
        }

        [Test]
        public void HoldStance_WithFollowCommand_SuppressesMovementWithoutRewritingCommand()
        {
            var soldier = Soldier();
            Em.SetComponentData(soldier, new SoldierState
            {
                Command = SoldierCommand.FollowHero,
                Stance = CombatStance.HoldGround,
                Behavior = UnitBehavior.Regrouping
            });
            SelectTargets();
            _navigation.Update();
            Assert.That(Em.GetBuffer<NavigationWaypoint>(soldier).Length, Is.Zero);
            var state = Em.GetComponentData<SoldierState>(soldier);
            Assert.That(state.Command, Is.EqualTo(SoldierCommand.FollowHero));
            Assert.That(state.Behavior, Is.EqualTo(UnitBehavior.Idle));
            state.Stance = CombatStance.Alert;
            Em.SetComponentData(soldier, state);
            SelectTargets();
            _navigation.Update();
            Assert.That(Em.GetBuffer<NavigationWaypoint>(soldier).Length, Is.GreaterThan(0));
            Assert.That(Em.GetComponentData<SoldierState>(soldier).Behavior, Is.EqualTo(UnitBehavior.Regrouping));
        }

        [Test]
        public void MoveToPosition_HeroMoves_KeepsDestinationAndReturnsAfterPursuit()
        {
            var soldier = Soldier();
            Em.SetComponentData(soldier, new SoldierState
            {
                Command = SoldierCommand.MoveToPosition,
                Rally = new float2(2, 0),
                Behavior = UnitBehavior.Regrouping
            });
            var enemy = Monster(3);
            SelectTargets();
            _navigation.Update();
            var path = Em.GetBuffer<NavigationWaypoint>(soldier);
            Assert.That(path[path.Length - 1].Position, Is.EqualTo(new float2(2, 0)));
            Em.SetComponentData(soldier, new GroundTransform { Position = new float2(2, 0) });
            _world.SetTime(new TimeData(2, 0.02f));
            SelectTargets();
            _navigation.Update();
            Assert.That(Em.GetComponentData<SoldierState>(soldier).Behavior, Is.EqualTo(UnitBehavior.Chasing));
            Em.SetComponentData(enemy, new Vitals());
            Em.SetComponentData(soldier, new GroundTransform { Position = new float2(3, 0) });
            using var heroes = Em.CreateEntityQuery(typeof(HeroTag));
            Em.SetComponentData(heroes.GetSingletonEntity(), new GroundTransform { Position = new float2(8, 8) });
            _world.SetTime(new TimeData(3, 0.02f));
            SelectTargets();
            _navigation.Update();
            path = Em.GetBuffer<NavigationWaypoint>(soldier);
            Assert.That(path[path.Length - 1].Position, Is.EqualTo(new float2(2, 0)));
            Assert.That(Em.GetComponentData<SoldierState>(soldier).Behavior, Is.EqualTo(UnitBehavior.Returning));
        }

        [Test]
        public void Regrouping_SuppressesAttacksButNotIncomingDamageAndReleasesOnArrival()
        {
            var soldier = Soldier();
            var monster = Monster(0.45f);
            Em.SetComponentData(soldier, new SoldierState { Command = SoldierCommand.FollowHero, Behavior = UnitBehavior.Regrouping });
            SelectTargets();
            _navigation.Update();
            Assert.That(Em.GetBuffer<NavigationWaypoint>(soldier).Length, Is.GreaterThan(0));
            var combat = _world.GetOrCreateSystemManaged<SoldierCombatSystem>();
            combat.Update();
            Assert.That(Em.GetComponentData<Vitals>(monster).Health, Is.EqualTo(100));
            Assert.That(Em.GetComponentData<Vitals>(soldier).Health, Is.EqualTo(90));
            Assert.That(Em.GetComponentData<SoldierState>(soldier).RetaliationTarget, Is.EqualTo(Entity.Null));
            using var query = Em.CreateEntityQuery(typeof(HeroTag));
            Em.SetComponentData(query.GetSingletonEntity(), new GroundTransform());
            _world.SetTime(new TimeData(3, 0.02f));
            SelectTargets();
            _navigation.Update();
            Assert.That(Em.GetComponentData<SoldierState>(soldier).Behavior, Is.Not.EqualTo(UnitBehavior.Regrouping));
            combat.Update();
            Assert.That(Em.GetComponentData<Vitals>(monster).Health, Is.EqualTo(90));
        }

        [Test]
        public void Following_RejectsEnemiesOutsideHeroAreaAndRegroupsWhenHeroLeaves()
        {
            using var query = Em.CreateEntityQuery(typeof(HeroTag));
            var hero = query.GetSingletonEntity();
            Em.SetComponentData(hero, new GroundTransform());
            var soldier = Soldier();
            Em.SetComponentData(soldier, new GroundTransform { Position = new float2(4.5f, 0) });
            Em.SetComponentData(soldier, new SoldierState { Command = SoldierCommand.FollowHero, FollowOffset = new float2(1, 0) });
            Em.SetComponentData(soldier, new CombatProfile { AttackRange = 4.5f, AttackInterval = 1 });
            var enemy = Monster(6);
            SelectTargets();
            _navigation.Update();
            var path = Em.GetBuffer<NavigationWaypoint>(soldier);
            Assert.That(path[path.Length - 1].Position, Is.EqualTo(new float2(1, 0)));
            _world.GetOrCreateSystemManaged<SoldierCombatSystem>().Update();
            Assert.That(Em.GetComponentData<Vitals>(enemy).Health, Is.EqualTo(100), "远程兵也不能在跟随时被圈外目标牵制");
            Em.SetComponentData(hero, new GroundTransform { Position = new float2(-3, 0) });
            SelectTargets();
            _navigation.Update();
            Assert.That(Em.GetComponentData<SoldierState>(soldier).Behavior, Is.EqualTo(UnitBehavior.Regrouping));
            path = Em.GetBuffer<NavigationWaypoint>(soldier);
            Assert.That(path[path.Length - 1].Position, Is.EqualTo(new float2(-2, 0)));
        }

        [TestCase(3, 5, true)]
        [TestCase(6, 5, false)]
        [TestCase(3, 2, false)]
        [TestCase(5, 5, true)]
        public void Alert_DetectsOnlyInsideConfiguredRadius(float enemyX, float radius, bool chases)
        {
            var soldier = Soldier();
            Monster(enemyX);
            _navigation.DetectionRadius = radius;
            SelectTargets();
            _navigation.Update();
            var path = Em.GetBuffer<NavigationWaypoint>(soldier);
            Assert.That(path.Length > 0, Is.EqualTo(chases));
            if (chases)
                Assert.That(path[path.Length - 1].Position.x, Is.EqualTo(enemyX));
        }

        [Test]
        public void Alert_AttackedOutsideDetectionRange_PursuesAttackerThenReturnsAfterDeath()
        {
            var soldier = Soldier();
            var monster = Monster(3);
            // 长攻击距离模拟警戒圈外的来袭；零伤害同样构成攻击事实。
            Em.SetComponentData(monster, new CombatProfile { AttackRange = 5, AttackInterval = 1 });
            var vital = Em.GetComponentData<Vitals>(monster);
            vital.Attack = 0;
            Em.SetComponentData(monster, vital);
            _navigation.DetectionRadius = 1;
            _world.GetOrCreateSystemManaged<SoldierCombatSystem>().Update();
            Assert.That(Em.GetComponentData<SoldierState>(soldier).RetaliationTarget, Is.EqualTo(monster));
            SelectTargets();
            _navigation.Update();
            Assert.That(Em.GetBuffer<NavigationWaypoint>(soldier).Length, Is.GreaterThan(0));
            vital.Health = 0;
            Em.SetComponentData(monster, vital);
            _world.SetTime(new TimeData(2, 0.02f));
            SelectTargets();
            _navigation.Update();
            Assert.That(Em.GetComponentData<SoldierState>(soldier).RetaliationTarget, Is.EqualTo(Entity.Null));
            Assert.That(Em.GetBuffer<NavigationWaypoint>(soldier).Length, Is.Zero);
        }

        [TestCase(0.45f, true)]
        [TestCase(3, false)]
        public void HoldGround_ClearsPathsAndAttacksOnlyWithinReach(float enemyX, bool attacks)
        {
            var soldier = Soldier(true);
            var monster = Monster(enemyX);
            Em.GetBuffer<NavigationWaypoint>(soldier).Add(new NavigationWaypoint { Position = new float2(8, 0) });
            SelectTargets();
            _navigation.Update();
            Assert.That(Em.GetBuffer<NavigationWaypoint>(soldier).Length, Is.Zero);
            _world.GetOrCreateSystemManaged<SoldierCombatSystem>().Update();
            Assert.That(Em.GetComponentData<Vitals>(monster).Health, Is.EqualTo(attacks ? 90 : 100));
            var separation = _world.GetOrCreateSystemManaged<MonsterSeparationSystem>();
            separation.Configure(0.12f, 3);
            separation.Update();
            Assert.That(Em.GetComponentData<GroundTransform>(soldier).Position, Is.EqualTo(float2.zero));
            if (attacks)
                Assert.That(Em.GetComponentData<GroundTransform>(monster).Position.x, Is.GreaterThan(enemyX));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Alert_EnemyDiesOrLeavesRadius_ReturnsToRally(bool dies)
        {
            var soldier = Soldier();
            var ground = Em.GetComponentData<GroundTransform>(soldier);
            ground.Position = ground.Previous = new float2(2, 0);
            Em.SetComponentData(soldier, ground);
            var enemy = Monster(4);
            SelectTargets();
            _navigation.Update();
            if (dies)
            {
                var vital = Em.GetComponentData<Vitals>(enemy);
                vital.Health = 0;
                Em.SetComponentData(enemy, vital);
            }
            else
            {
                var enemyGround = Em.GetComponentData<GroundTransform>(enemy);
                enemyGround.Position = new float2(12, 0);
                Em.SetComponentData(enemy, enemyGround);
            }
            _world.SetTime(new TimeData(2, 0.02f));
            SelectTargets();
            _navigation.Update();
            var path = Em.GetBuffer<NavigationWaypoint>(soldier);
            Assert.That(path[path.Length - 1].Position, Is.EqualTo(float2.zero));
        }

        [Test]
        public void Alert_FixedSteps_MoveIntoReachAndDamageEnemy()
        {
            var hero = new UnitDefinition(UnitKind.Hero, 100, 0, 0, 4, 0.3f, 1);
            var enemy = new UnitDefinition(UnitKind.Goblin, 100, 0, 0, 0.01f, 0.2f, 1);
            var runner = new UnitDefinition(UnitKind.Runner, 100, 0, 0, 1, 0.2f, 1);
            using var simulation = new EcsBattleSimulation();
            simulation.Reset(new BattleDefinition(hero, enemy, runner, 1, 0, 40, 40, 0.02f, 7,
                heroSpawn: new MapPoint(-10, -10), enemySpawns: new[] { new MapPoint(3, 0) },
                soldiers: new[] { new UnitDefinition(UnitKind.Soldier, 100, 10, 0, 2, 0.2f, 1) }));
            Assert.That(simulation.TrySpawnSoldier(UnitKind.Soldier, new MapPoint(0, 0), new MapPoint(0, 0), out ulong id), Is.True);
            for (int i = 0; i < 100; i++) simulation.Step(0.02f, default);
            var units = new List<UnitSnapshot>();
            simulation.CopyUnits(units);
            Assert.That(units.Find(u => u.Id == id).X, Is.GreaterThan(1));
            Assert.That(units.Find(u => u.Kind == UnitKind.Goblin).Health, Is.LessThan(100));
        }

        [Test]
        public void HoldCommand_SnapshotShowsUnselectedMarker_RecallClearsHold()
        {
            var hero = new UnitDefinition(UnitKind.Hero, 100, 0, 0, 4, 0.3f, 1);
            var enemy = new UnitDefinition(UnitKind.Goblin, 100, 0, 0, 1, 0.2f, 1);
            var runner = new UnitDefinition(UnitKind.Runner, 100, 0, 0, 1, 0.2f, 1);
            using var simulation = new EcsBattleSimulation();
            simulation.Reset(new BattleDefinition(hero, enemy, runner, 0, 0, 40, 40, 0.02f, 7,
                soldiers: new[] { new UnitDefinition(UnitKind.Soldier, 100, 10, 0, 2, 0.2f, 1) }));
            Assert.That(simulation.TrySpawnSoldier(UnitKind.Soldier, new MapPoint(2, 0), new MapPoint(2, 0), out ulong id), Is.True);
            var units = new List<UnitSnapshot>();
            simulation.CopyUnits(units);
            Assert.That(units.Find(u => u.Id == id).IsStationed, Is.False);
            simulation.Execute(PlayerOperation.NearbyTap);
            simulation.Execute(PlayerOperation.NearbyHold);
            simulation.CopyUnits(units);
            var held = units.Find(u => u.Id == id);
            Assert.That(held.IsStationed, Is.True);
            Assert.That(held.IsSelected, Is.False);
            Assert.That(held.Command, Is.EqualTo(SoldierCommand.MoveToPosition));
            Assert.That(held.Stance, Is.EqualTo(CombatStance.HoldGround));
            Assert.That(held.Behavior, Is.EqualTo(UnitBehavior.Idle));
            for (int i = 0; i < 50; i++) simulation.Step(0.02f, default);
            simulation.CopyUnits(units);
            Assert.That(units.Find(u => u.Id == id).X, Is.EqualTo(held.X));
            simulation.Execute(PlayerOperation.CallAll);
            simulation.CopyUnits(units);
            Assert.That(units.Find(u => u.Id == id).IsStationed, Is.False);
            Assert.That(units.Find(u => u.Id == id).IsSelected, Is.True);
            Assert.That(units.Find(u => u.Id == id).Command, Is.EqualTo(SoldierCommand.FollowHero));
            Assert.That(units.Find(u => u.Id == id).Stance, Is.EqualTo(CombatStance.Alert));
            Assert.That(units.Find(u => u.Id == id).Behavior, Is.EqualTo(UnitBehavior.Regrouping));
        }
    }
}
