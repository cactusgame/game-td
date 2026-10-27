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

namespace Game.Battle.Tests
{
    public sealed class HealthRegenerationTests
    {
        [TestCase(0.1f, 10, 1)]
        [TestCase(0.2f, 5, 1)]
        [TestCase(5f, 1, 5)]
        [TestCase(0f, 10, 0)]
        public void FractionalHealing_IsAbsoluteAndIndependentOfStepPartition(float rate, int seconds, int expected)
        {
            using var world = new World("Regeneration partition test");
            var em = world.EntityManager;
            var entity = Spawn(em, rate, 10);
            var system = world.GetOrCreateSystemManaged<HealthRegenerationSystem>();
            for (int i = 0; i < seconds * 50; i++)
            {
                world.SetTime(new TimeData((i + 1) * 0.02, 0.02f));
                system.Update();
            }
            Assert.That(em.GetComponentData<Vitals>(entity).Health, Is.EqualTo(10 + expected));
            em.SetComponentData(entity, new Vitals { Health = 10, MaxHealth = 10000, LastHitTime = -100, LastAttackTime = -100 });
            em.SetComponentData(entity, new HealthRegenerationState { HealthPerSecond = rate });
            world.SetTime(new TimeData(seconds, seconds));
            system.Update();
            Assert.That(em.GetComponentData<Vitals>(entity).Health, Is.EqualTo(10 + expected), "恢复量不随最大生命值或模拟步数改变");
        }

        [TestCase(100)]
        [TestCase(0)]
        [TestCase(-5)]
        public void FullHealthAndDeadUnits_DiscardCredit(int health)
        {
            using var world = new World("Regeneration bounds test");
            var em = world.EntityManager;
            var entity = Spawn(em, 5, health);
            em.SetComponentData(entity, new HealthRegenerationState { HealthPerSecond = 5, AccumulatedHealing = 0.9 });
            world.SetTime(new TimeData(10, 10));
            world.GetOrCreateSystemManaged<HealthRegenerationSystem>().Update();
            Assert.That(em.GetComponentData<Vitals>(entity).Health, Is.EqualTo(health));
            Assert.That(em.GetComponentData<HealthRegenerationState>(entity).AccumulatedHealing, Is.Zero);
            em.SetComponentData(entity, new Vitals { Health = 50, MaxHealth = 100 });
            world.SetTime(new TimeData(10.1, 0.1f));
            world.GetOrCreateSystemManaged<HealthRegenerationSystem>().Update();
            Assert.That(em.GetComponentData<Vitals>(entity).Health, Is.EqualTo(50));
        }

        [TestCase(5f)]
        [TestCase(float.MaxValue)]
        public void Healing_ClampsAndPreservesCombatTimestamps(float rate)
        {
            using var world = new World("Regeneration clamp test");
            var em = world.EntityManager;
            var entity = Spawn(em, rate, 99);
            var before = em.GetComponentData<Vitals>(entity);
            before.LastHitTime = 7;
            before.LastAttackTime = 8;
            em.SetComponentData(entity, before);
            world.SetTime(new TimeData(20, 10));
            world.GetOrCreateSystemManaged<HealthRegenerationSystem>().Update();
            var vitals = em.GetComponentData<Vitals>(entity);
            Assert.That(vitals.Health, Is.EqualTo(100));
            Assert.That(vitals.LastHitTime, Is.EqualTo(7));
            Assert.That(vitals.LastAttackTime, Is.EqualTo(8));
            Assert.That(vitals.NextNormalAttackAt, Is.EqualTo(9));
            Assert.That(em.GetComponentData<HealthRegenerationState>(entity).AccumulatedHealing, Is.Zero);
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Definition_RejectsInvalidRate(float rate)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Definition(UnitKind.Hero, rate));
        }

        [Test]
        public void Simulation_AllFactionsRegenerateAfterPoison_LethalPoisonCannotBeHealed()
        {
            using var simulation = new EcsBattleSimulation();
            simulation.Reset(new BattleDefinition(Definition(UnitKind.Hero, 5), Definition(UnitKind.Goblin, 0.1f),
                Definition(UnitKind.Runner, 0.1f), 1, 0, 40, 40, 0.02f, 7,
                heroSpawn: new MapPoint(-8, -8), enemySpawns: new[] { new MapPoint(8, 8) },
                soldiers: new[] { Definition(UnitKind.Soldier, 0.2f), Definition(UnitKind.Archer, 0.2f) }, heroRespawnSeconds: 1));
            simulation.TrySpawnSoldier(UnitKind.Soldier, new MapPoint(-8, 8), new MapPoint(-8, 8), out _);
            simulation.TrySpawnSoldier(UnitKind.Archer, new MapPoint(8, -8), new MapPoint(8, -8), out _);
            var units = new List<UnitSnapshot>();
            simulation.CopyUnits(units);
            foreach (var unit in units)
                Assert.That(simulation.TryApplyStatus(new ApplyUnitStatusCommand(unit.Id, UnitStatusKind.Poison, 1, 50, 1)), Is.True);
            simulation.Step(1, default);
            simulation.Step(5, default);
            units.Clear();
            simulation.CopyUnits(units);
            Assert.That(units.TrueForAll(unit => unit.Health == 50), Is.True, "最后一次毒伤之后的五秒不能恢复");
            simulation.Step(10, default);
            units.Clear();
            simulation.CopyUnits(units);
            foreach (var unit in units)
                Assert.That(unit.Health, Is.EqualTo(unit.Kind == UnitKind.Hero ? 100 : unit.Kind == UnitKind.Soldier || unit.Kind == UnitKind.Archer ? 52 : 51), unit.Kind.ToString());
            var hero = units.Find(unit => unit.Kind == UnitKind.Hero);
            simulation.TryApplyStatus(new ApplyUnitStatusCommand(hero.Id, UnitStatusKind.Poison, 1, 100, 1));
            simulation.Step(1, default);
            units.Clear(); simulation.CopyUnits(units);
            Assert.That(units.Find(unit => unit.Kind == UnitKind.Hero).Health, Is.Zero);
            simulation.Step(1, default);
            units.Clear(); simulation.CopyUnits(units);
            Assert.That(units.Find(unit => unit.Kind == UnitKind.Hero).Health, Is.EqualTo(100));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AttackAndHit_InterruptCredit_AndOnlyRecoverAfterFiveSeconds(bool attacks)
        {
            using var world = new World("Recovery interruption test");
            var em = world.EntityManager;
            var entity = Spawn(em, 5, 50);
            var vitals = em.GetComponentData<Vitals>(entity);
            if (attacks) vitals.LastAttackTime = 10; else vitals.LastHitTime = 10;
            em.SetComponentData(entity, vitals);
            em.SetComponentData(entity, new HealthRegenerationState { HealthPerSecond = 5, AccumulatedHealing = 0.9 });
            var system = world.GetOrCreateSystemManaged<HealthRegenerationSystem>();
            world.SetTime(new TimeData(14, 4));
            system.Update();
            Assert.That(em.GetComponentData<Vitals>(entity).Health, Is.EqualTo(50));
            Assert.That(em.GetComponentData<HealthRegenerationState>(entity).AccumulatedHealing, Is.Zero);
            world.SetTime(new TimeData(16, 2));
            system.Update();
            Assert.That(em.GetComponentData<Vitals>(entity).Health, Is.EqualTo(55), "跨过脱战时刻只恢复最后一秒");
        }

        [TestCase(UnitStatusKind.Stun)]
        [TestCase(UnitStatusKind.Root)]
        [TestCase(UnitStatusKind.Silence)]
        [TestCase(UnitStatusKind.Poison)]
        public void HarmfulStatuses_BlockHealing_AndExpiryDoesNotBackfillBlockedTime(UnitStatusKind kind)
        {
            using var world = new World("Recovery status test");
            var em = world.EntityManager;
            var entity = Spawn(em, 5, 50);
            var status = new UnitStatusState();
            switch (kind)
            {
                case UnitStatusKind.Stun: status.StunnedUntil = 12; break;
                case UnitStatusKind.Root: status.RootedUntil = 12; break;
                case UnitStatusKind.Silence: status.SilencedUntil = 12; break;
                case UnitStatusKind.Poison:
                    status.PoisonedUntil = 12;
                    status.PoisonDamagePerTick = 1;
                    status.PoisonIntervalSeconds = 10;
                    status.NextPoisonTickAt = 20;
                    break;
            }
            em.AddComponentData(entity, status);
            var statuses = world.GetOrCreateSystemManaged<UnitStatusSystem>();
            var regeneration = world.GetOrCreateSystemManaged<HealthRegenerationSystem>();
            world.SetTime(new TimeData(10, 2));
            statuses.Update(); regeneration.Update();
            Assert.That(em.GetComponentData<Vitals>(entity).Health, Is.EqualTo(50));
            world.SetTime(new TimeData(13, 3));
            statuses.Update(); regeneration.Update();
            Assert.That(em.GetComponentData<Vitals>(entity).Health, Is.EqualTo(55));
        }

        [Test]
        public void Pursuit_BlocksBothAttackerAndTarget_ThroughCooldown_ThenAllowsRecovery()
        {
            using var world = new World("Recovery pursuit test");
            var em = world.EntityManager;
            var attacker = Spawn(em, 5, 50);
            var target = Spawn(em, 5, 50);
            em.AddComponentData(attacker, new CombatTargetState { Target = target });
            var regeneration = world.GetOrCreateSystemManaged<HealthRegenerationSystem>();
            world.SetTime(new TimeData(10, 1)); regeneration.Update();
            world.SetTime(new TimeData(30, 20)); regeneration.Update();
            Assert.That(em.GetComponentData<Vitals>(attacker).Health, Is.EqualTo(50));
            Assert.That(em.GetComponentData<Vitals>(target).Health, Is.EqualTo(50));
            em.SetComponentData(attacker, default(CombatTargetState));
            world.SetTime(new TimeData(35, 5)); regeneration.Update();
            Assert.That(em.GetComponentData<Vitals>(target).Health, Is.EqualTo(50));
            world.SetTime(new TimeData(36, 1)); regeneration.Update();
            Assert.That(em.GetComponentData<Vitals>(attacker).Health, Is.EqualTo(55));
            Assert.That(em.GetComponentData<Vitals>(target).Health, Is.EqualTo(55));
        }

        [Test]
        public void ExternalWallAttack_CancelsRecoveryAtSameSimulationTime_AlsoWhileCoolingDown()
        {
            using var simulation = new EcsBattleSimulation();
            simulation.Reset(new BattleDefinition(Definition(UnitKind.Hero, 5), Definition(UnitKind.Goblin, 1),
                Definition(UnitKind.Runner, 1), 1, 0, 40, 40, 0.02f, 7,
                heroSpawn: new MapPoint(-8, -8), enemySpawns: new[] { new MapPoint(8, 8) }));
            simulation.TryApplyStatus(new ApplyUnitStatusCommand(2, UnitStatusKind.Poison, 1, 50, 1));
            simulation.Step(1, default);
            simulation.Step(6, default);
            var units = new List<UnitSnapshot>();
            simulation.CopyUnits(units);
            Assert.That(units.Find(unit => unit.Id == 2).Health, Is.EqualTo(51));
            Assert.That(simulation.TryAttackExternalTarget(2, new MapPoint(8, 8), simulation.ElapsedTime, damage => true), Is.True);
            units.Clear(); simulation.CopyUnits(units);
            Assert.That(units.Find(unit => unit.Id == 2).Health, Is.EqualTo(50));
            Assert.That(simulation.TryAttackExternalTarget(2, new MapPoint(8, 8), simulation.ElapsedTime, damage => true), Is.False);
            units.Clear(); simulation.CopyUnits(units);
            Assert.That(units.Find(unit => unit.Id == 2).Health, Is.EqualTo(50), "同一步不能重复撤销恢复");
            simulation.Step(0.5f, default);
            units.Clear(); simulation.CopyUnits(units);
            Assert.That(units.Find(unit => unit.Id == 2).Health, Is.EqualTo(50));
        }

        [Test]
        public void Simulation_ContinuousMelee_NeverHealsBetweenNormalAttacks()
        {
            using var simulation = new EcsBattleSimulation();
            var hero = new UnitDefinition(UnitKind.Hero, 100, 1, 0, 0.01f, 0.2f, 1, healthRegenerationPerSecond: 100);
            var enemy = new UnitDefinition(UnitKind.Goblin, 100, 1, 0, 0.01f, 0.2f, 1, healthRegenerationPerSecond: 100);
            simulation.Reset(new BattleDefinition(hero, enemy, Definition(UnitKind.Runner, 100), 1, 0,
                40, 40, 0.02f, 7, heroSpawn: new MapPoint(0, 0), enemySpawns: new[] { new MapPoint(0.3f, 0) }));
            simulation.TryApplyStatus(new ApplyUnitStatusCommand(1, UnitStatusKind.Poison, 1, 20, 1));
            simulation.TryApplyStatus(new ApplyUnitStatusCommand(2, UnitStatusKind.Poison, 1, 20, 1));
            simulation.Step(1, default);
            var units = new List<UnitSnapshot>();
            simulation.CopyUnits(units);
            int heroHealth = units.Find(unit => unit.Id == 1).Health;
            int enemyHealth = units.Find(unit => unit.Id == 2).Health;
            for (int i = 0; i < 100; i++)
            {
                simulation.Step(0.1f, default);
                units.Clear(); simulation.CopyUnits(units);
                var currentHero = units.Find(unit => unit.Id == 1);
                var currentEnemy = units.Find(unit => unit.Id == 2);
                Assert.That(currentHero.Health, Is.LessThanOrEqualTo(heroHealth));
                Assert.That(currentEnemy.Health, Is.LessThanOrEqualTo(enemyHealth));
                heroHealth = currentHero.Health;
                enemyHealth = currentEnemy.Health;
            }
            Assert.That(heroHealth, Is.LessThan(80));
            Assert.That(enemyHealth, Is.LessThan(80));
        }

        [Test]
        public void ExpiredOrDeadTargetsAndInvulnerability_DoNotPreventRecovery()
        {
            using var world = new World("Recovery invalid target test");
            var em = world.EntityManager;
            var actor = Spawn(em, 5, 50);
            var target = Spawn(em, 5, 0);
            em.AddComponentData(actor, new CombatTargetState { Target = target });
            em.AddComponentData(actor, new UnitStatusState { InvulnerableUntil = 100 });
            var regeneration = world.GetOrCreateSystemManaged<HealthRegenerationSystem>();
            world.SetTime(new TimeData(10, 1)); regeneration.Update();
            Assert.That(em.GetComponentData<Vitals>(actor).Health, Is.EqualTo(55));
            em.DestroyEntity(target);
            world.SetTime(new TimeData(11, 1)); regeneration.Update();
            Assert.That(em.GetComponentData<Vitals>(actor).Health, Is.EqualTo(60));
        }

        [Test]
        public void AttackSkill_CancelsSameStepHealing_EvenWhenNoEnemyIsHit()
        {
            using var simulation = new EcsBattleSimulation();
            var hero = new UnitDefinition(UnitKind.Hero, 100, 0, 0, 0.01f, 0.2f, 1,
                skills: new[] { new HeroAreaSkillDefinition("test", true, 1, 10, 1) }, healthRegenerationPerSecond: 5);
            simulation.Reset(new BattleDefinition(hero, Definition(UnitKind.Goblin, 1), Definition(UnitKind.Runner, 1),
                1, 0, 40, 40, 0.02f, 7, heroSpawn: new MapPoint(-8, -8), enemySpawns: new[] { new MapPoint(8, 8) }));
            simulation.TryApplyStatus(new ApplyUnitStatusCommand(1, UnitStatusKind.Poison, 1, 50, 1));
            simulation.Step(1, default);
            simulation.Step(6, default);
            var units = new List<UnitSnapshot>();
            simulation.CopyUnits(units);
            Assert.That(units.Find(unit => unit.Id == 1).Health, Is.EqualTo(55));
            simulation.Execute(PlayerOperation.SkillOne);
            units.Clear(); simulation.CopyUnits(units);
            Assert.That(units.Find(unit => unit.Id == 1).Health, Is.EqualTo(50));
        }

        [Test]
        public void TowerTarget_CannotHealDuringLongShotCooldownOrInvulnerability()
        {
            using var world = new World("Recovery tower target test");
            var em = world.EntityManager;
            var monster = Spawn(em, 5, 50);
            em.AddComponent<MonsterTag>(monster);
            em.AddComponent<GroundTransform>(monster);
            em.AddComponentData(monster, new UnitStatusState { InvulnerableUntil = 100 });
            var tower = em.CreateEntity(typeof(DefenseStructure));
            em.SetComponentData(tower, new DefenseStructure { Health = 100, Attack = 1, Range = 10, NextAttack = 1000 });
            var combat = world.GetOrCreateSystemManaged<StructureCombatSystem>();
            var regeneration = world.GetOrCreateSystemManaged<HealthRegenerationSystem>();
            world.SetTime(new TimeData(10, 10));
            combat.Update(); regeneration.Update();
            Assert.That(em.GetComponentData<Vitals>(monster).Health, Is.EqualTo(50));
            Assert.That(em.GetComponentData<Vitals>(monster).LastHitTime, Is.EqualTo(-100), "锁定本身不触发受击反馈");
            em.DestroyEntity(tower);
            world.SetTime(new TimeData(16, 6));
            regeneration.Update();
            Assert.That(em.GetComponentData<Vitals>(monster).Health, Is.EqualTo(55));
        }

        private static UnitDefinition Definition(UnitKind kind, float rate)
        {
            return new UnitDefinition(kind, 100, 0, 0, 0.01f, 0.2f, 1, healthRegenerationPerSecond: rate);
        }

        private static Entity Spawn(EntityManager em, float rate, int health)
        {
            var entity = em.CreateEntity(typeof(Vitals), typeof(HealthRegenerationState));
            em.SetComponentData(entity, new Vitals { Health = health, MaxHealth = 100, LastHitTime = -100, LastAttackTime = -100, NextNormalAttackAt = 9 });
            em.SetComponentData(entity, new HealthRegenerationState { HealthPerSecond = rate });
            return entity;
        }
    }
}
