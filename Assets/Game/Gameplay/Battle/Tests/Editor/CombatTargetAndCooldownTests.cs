using System;
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
    public sealed class CombatTargetAndCooldownTests
    {
        private World _world;
        private EntityManager Em => _world.EntityManager;
        [SetUp]
        public void SetUp()
        {
            _world = new World("Shared targets and normal attack cooldown");
            _world.SetTime(new TimeData(1, 0.02f));
            var hero = Spawn(new float2(-10, -10));
            Em.AddComponent<HeroTag>(hero);
        }
        [TearDown]
        public void TearDown() => _world.Dispose();
        private Entity Spawn(float2 position)
        {
            var entity = Em.CreateEntity(typeof(GroundTransform), typeof(Vitals), typeof(UnitMotion), typeof(CombatProfile),
                typeof(CombatTargetState), typeof(MonsterNavigationState), typeof(WanderState));
            Em.AddBuffer<NavigationWaypoint>(entity);
            Em.SetComponentData(entity, new GroundTransform { Position = position, Previous = position });
            Em.SetComponentData(entity, new Vitals { Health = 100, MaxHealth = 100, Attack = 10, LastAttackTime = -100 });
            Em.SetComponentData(entity, new UnitMotion { Radius = 0.2f, Speed = 2 });
            Em.SetComponentData(entity, new CombatProfile { AttackRange = 0.15f, AttackInterval = 1 });
            return entity;
        }
        private Entity Monster(float x)
        {
            var entity = Spawn(new float2(x, 0));
            Em.AddComponent<MonsterTag>(entity);
            return entity;
        }
        private Entity Soldier(float x)
        {
            var entity = Spawn(new float2(x, 0));
            Em.AddComponentData(entity, new SoldierState { Rally = new float2(x, 0) });
            return entity;
        }
        private void Select() => _world.GetOrCreateSystemManaged<CombatTargetSelectionSystem>().Update();
        private void Move(Entity entity, float x) => Em.SetComponentData(entity, new GroundTransform { Position = new float2(x, 0), Previous = new float2(x, 0) });

        [Test]
        public void Soldier_PursuesAndStrikesSameRetainedTarget_DeathSwitchDoesNotResetCooldown()
        {
            var soldier = Soldier(0);
            var first = Monster(3);
            var second = Monster(3.1f);
            Select();
            var navigation = _world.GetOrCreateSystemManaged<SoldierNavigationSystem>();
            navigation.Configure(GridMap.CreateOpenArena(40, 40, 1));
            navigation.Update();
            var path = Em.GetBuffer<NavigationWaypoint>(soldier);
            Assert.That(path[path.Length - 1].Position, Is.EqualTo(new float2(3, 0)));
            Move(first, 0.5f);
            Move(second, 0.45f);
            Select();
            Assert.That(Em.GetComponentData<CombatTargetState>(soldier).Target, Is.EqualTo(first));
            var combat = _world.GetOrCreateSystemManaged<SoldierCombatSystem>();
            combat.Update();
            Assert.That(Em.GetComponentData<Vitals>(first).Health, Is.EqualTo(90));
            Assert.That(Em.GetComponentData<Vitals>(second).Health, Is.EqualTo(100));
            var dead = Em.GetComponentData<Vitals>(first); dead.Health = 0; Em.SetComponentData(first, dead);
            combat.Update();
            Assert.That(Em.GetComponentData<CombatTargetState>(soldier).Target, Is.EqualTo(second));
            Assert.That(Em.GetComponentData<Vitals>(second).Health, Is.EqualTo(100));
            _world.SetTime(new TimeData(2, 0.02f));
            combat.Update();
            Assert.That(Em.GetComponentData<Vitals>(second).Health, Is.EqualTo(90));
        }

        [Test]
        public void Retaliation_DoesNotReplaceStillValidTarget_WhenAnotherEnemyHits()
        {
            var soldier = Soldier(0);
            var first = Monster(1);
            var other = Monster(1.1f);
            Select();
            var state = Em.GetComponentData<SoldierState>(soldier); state.RetaliationTarget = other; Em.SetComponentData(soldier, state);
            Select();
            Assert.That(Em.GetComponentData<CombatTargetState>(soldier).Target, Is.EqualTo(first));
            Move(first, 12);
            Select();
            Assert.That(Em.GetComponentData<CombatTargetState>(soldier).Target, Is.EqualTo(other));
        }

        [Test]
        public void Monster_KeepsNavigationTarget_WhenSecondSoldierBecomesCloser()
        {
            var monster = Monster(0);
            var first = Soldier(3);
            var other = Soldier(3.1f);
            Select();
            var navigation = _world.GetOrCreateSystemManaged<MonsterNavigationSystem>();
            navigation.Configure(GridMap.CreateOpenArena(40, 40, 1), new NavigationDefinition());
            navigation.Update();
            var path = Em.GetBuffer<NavigationWaypoint>(monster);
            Assert.That(path[path.Length - 1].Position, Is.EqualTo(new float2(3, 0)));
            Move(first, 0.5f); Move(other, 0.45f);
            var vital = Em.GetComponentData<Vitals>(first); vital.Attack = 0; Em.SetComponentData(first, vital);
            vital = Em.GetComponentData<Vitals>(other); vital.Attack = 0; Em.SetComponentData(other, vital);
            _world.GetOrCreateSystemManaged<SoldierCombatSystem>().Update();
            Assert.That(Em.GetComponentData<CombatTargetState>(monster).Target, Is.EqualTo(first));
            Assert.That(Em.GetComponentData<Vitals>(first).Health, Is.EqualTo(90));
            Assert.That(Em.GetComponentData<Vitals>(other).Health, Is.EqualTo(100));
        }

        private static BattleDefinition Definition()
        {
            return new BattleDefinition(new UnitDefinition(UnitKind.Hero, 100, 0, 0, 2, 0.2f, 1),
                new UnitDefinition(UnitKind.Goblin, 100, 10, 0, 2, 0.2f, 1, attackInterval: 1),
                new UnitDefinition(UnitKind.Runner, 100, 0, 0, 2, 0.2f, 1),
                1, 0, 40, 40, 0.02f, 7, heroSpawn: new MapPoint(-10, -10),
                enemySpawns: new[] { new MapPoint(0, 0) }, soldiers: new[]
                { new UnitDefinition(UnitKind.Soldier, 100, 0, 0, 2, 0.2f, 1) });
        }
        private static List<UnitSnapshot> Units(EcsBattleSimulation simulation)
        {
            var units = new List<UnitSnapshot>(); simulation.CopyUnits(units); return units;
        }

        [Test]
        public void UnitAttack_ThenWallAndGate_AttacksShareOneCooldown()
        {
            using var simulation = new EcsBattleSimulation();
            simulation.Reset(Definition());
            Assert.That(simulation.TrySpawnSoldier(UnitKind.Soldier, new MapPoint(0.54f, 0), new MapPoint(0.54f, 0), out ulong soldier), Is.True);
            ulong monster = Units(simulation).Find(unit => unit.Kind == UnitKind.Goblin).Id;
            simulation.Step(0.125f, default);
            Assert.That(Units(simulation).Find(unit => unit.Id == soldier).Health, Is.EqualTo(90));
            bool committed = false;
            Assert.That(simulation.TryAttackExternalTarget(monster, default, simulation.ElapsedTime, damage => { committed = true; return true; }), Is.False);
            Assert.That(committed, Is.False);
            simulation.DismissSoldiers(new[] { soldier });
            simulation.Step(0.5f, default);
            Assert.That(simulation.TryAttackExternalTarget(monster, default, simulation.ElapsedTime, damage => true), Is.False);
            simulation.Step(0.5f, default);
            Assert.That(simulation.TryAttackExternalTarget(monster, new MapPoint(1, 0), simulation.ElapsedTime, damage => damage == 10), Is.True);
            Assert.That(simulation.TryAttackExternalTarget(monster, new MapPoint(2, 0), simulation.ElapsedTime, damage => true), Is.False);
        }

        [Test]
        public void ExternalAttack_FailedCommitDoesNotConsumeCooldown_AndStaleTimeIsRejected()
        {
            using var simulation = new EcsBattleSimulation(); simulation.Reset(Definition());
            ulong monster = Units(simulation).Find(unit => unit.Kind == UnitKind.Goblin).Id;
            Assert.Throws<InvalidOperationException>(() => simulation.TryAttackExternalTarget(monster, default, 0,
                damage => throw new InvalidOperationException("Spatial commit failed")));
            Assert.That(Units(simulation).Find(unit => unit.Id == monster).LastAttackTime, Is.EqualTo(-100));
            Assert.That(simulation.TryAttackExternalTarget(monster, default, 0, damage => true), Is.True);
            Assert.That(simulation.TryAttackExternalTarget(monster, default, 0, damage => true), Is.False);
            simulation.Step(1, default);
            Assert.That(simulation.TryAttackExternalTarget(monster, default, 0, damage => true), Is.False);
            Assert.That(simulation.TryAttackExternalTarget(monster, default, 1, damage => true), Is.True);
        }

        [Test]
        public void WallAttack_AndCastleAttack_ShareUnitCooldownInBothDirections()
        {
            using var simulation = new EcsBattleSimulation();
            simulation.Reset(Definition());
            ulong monster = Units(simulation).Find(unit => unit.Kind == UnitKind.Goblin).Id;
            simulation.SynchronizeStructures(new[] { new DefenseStructureDefinition("castle", true, 0.25f, -1, 2, 1, 100, 0, 0, 1) });
            Assert.That(simulation.TryAttackExternalTarget(monster, default, 0, damage => true), Is.True);
            simulation.Step(0.5f, default);
            var structures = new List<DefenseStructureSnapshot>();
            simulation.CopyStructures(structures);
            Assert.That(structures[0].Health, Is.EqualTo(100));
            simulation.Step(0.5f, default);
            simulation.CopyStructures(structures);
            Assert.That(structures[0].Health, Is.EqualTo(90));
            Assert.That(simulation.TryAttackExternalTarget(monster, default, 1, damage => true), Is.False);
        }

        [Test]
        public void CastleObjective_KeepsLivingReachableCastle_WhenAnotherBecomesCloser()
        {
            var monster = Monster(0);
            Em.SetComponentData(monster, new CombatProfile { TargetMainCastle = 1, AttackInterval = 1, AttackRange = 0.15f });
            var first = Em.CreateEntity(typeof(DefenseStructure));
            var second = Em.CreateEntity(typeof(DefenseStructure));
            Em.SetComponentData(first, new DefenseStructure { Min = new float2(3, -1), Max = new float2(4, 1), Health = 100, IsMainCastle = 1 });
            Em.SetComponentData(second, new DefenseStructure { Min = new float2(-5, -1), Max = new float2(-4, 1), Health = 100, IsMainCastle = 1 });
            var objectives = _world.GetOrCreateSystemManaged<MonsterObjectiveSystem>();
            objectives.Configure(GridMap.CreateOpenArena(40, 40, 1));
            objectives.Update();
            Assert.That(Em.GetComponentData<MonsterNavigationState>(monster).StructureTarget, Is.EqualTo(first));
            Move(monster, -2);
            objectives.Update();
            Assert.That(Em.GetComponentData<MonsterNavigationState>(monster).StructureTarget, Is.EqualTo(first));
            var dead = Em.GetComponentData<DefenseStructure>(first); dead.Health = 0; Em.SetComponentData(first, dead);
            objectives.Update();
            Assert.That(Em.GetComponentData<MonsterNavigationState>(monster).StructureTarget, Is.EqualTo(second));
        }
    }
}
