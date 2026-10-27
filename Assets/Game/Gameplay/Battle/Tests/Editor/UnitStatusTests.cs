using Game.Battle.Simulation.Components;
using Game.Battle.Simulation.Systems;
using Game.Battle.Domain.ValueObjects;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Tests
{
    public sealed class UnitStatusTests
    {
        private World _world;
        private EntityManager Em => _world.EntityManager;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Unit status tests");
            _world.SetTime(new TimeData(1, 0.02f));
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private Entity Spawn()
        {
            var entity = Em.CreateEntity(typeof(GroundTransform), typeof(Vitals), typeof(UnitMotion), typeof(CombatProfile), typeof(UnitStatusState), typeof(CombatTargetState));
            Em.SetComponentData(entity, new Vitals { Health = 100, MaxHealth = 100, Attack = 10, LastAttackTime = -100, LastHitTime = -100 });
            Em.SetComponentData(entity, new UnitMotion { Radius = 0.3f });
            Em.SetComponentData(entity, new CombatProfile { AttackRange = 1, AttackInterval = 1 });
            return entity;
        }

        [Test]
        public void ContactProtection_BlocksDamageWithoutBlockingAttacksAndExpiresDuringOverlap()
        {
            Em.CreateEntity(typeof(ContactStatistics));
            var hero = Spawn();
            Em.AddComponent<HeroTag>(hero);
            Em.SetComponentData(hero, new UnitStatusState { InvulnerableUntil = 3 });
            var monster = Spawn();
            Em.AddComponent<MonsterTag>(monster);
            Em.AddComponent<HeroContact>(monster);
            var system = _world.GetOrCreateSystem<CircleContactSystem>();
            system.Update(_world.Unmanaged);
            Assert.That(Em.GetComponentData<Vitals>(hero).Health, Is.EqualTo(100));
            Assert.That(Em.GetComponentData<Vitals>(hero).LastHitTime, Is.EqualTo(-100));
            Assert.That(Em.GetComponentData<Vitals>(monster).Health, Is.EqualTo(90));
            _world.SetTime(new TimeData(2.999, 0.02f));
            system.Update(_world.Unmanaged);
            Assert.That(Em.GetComponentData<Vitals>(hero).Health, Is.EqualTo(100));
            _world.SetTime(new TimeData(3, 0.02f));
            system.Update(_world.Unmanaged);
            Assert.That(Em.GetComponentData<Vitals>(hero).Health, Is.EqualTo(100), "无敌到期不能重置普通攻击冷却");
            _world.SetTime(new TimeData(4, 0.02f));
            system.Update(_world.Unmanaged);
            Assert.That(Em.GetComponentData<Vitals>(hero).Health, Is.EqualTo(90));
        }

        [Test]
        public void SoldierCombat_InvulnerabilityProtectsBothSoldiersAndMonsters()
        {
            var soldier = Spawn();
            Em.AddComponent<SoldierState>(soldier);
            var monster = Spawn();
            Em.AddComponent<MonsterTag>(monster);
            Em.SetComponentData(soldier, new UnitStatusState { InvulnerableUntil = 3 });
            Em.SetComponentData(monster, new UnitStatusState { InvulnerableUntil = 3 });
            var system = _world.GetOrCreateSystemManaged<SoldierCombatSystem>();
            system.Update();
            Assert.That(Em.GetComponentData<Vitals>(soldier).Health, Is.EqualTo(100));
            Assert.That(Em.GetComponentData<Vitals>(monster).Health, Is.EqualTo(100));
            _world.SetTime(new TimeData(3, 0.02f));
            system.Update();
            Assert.That(Em.GetComponentData<Vitals>(soldier).Health, Is.EqualTo(90));
            Assert.That(Em.GetComponentData<Vitals>(monster).Health, Is.EqualTo(90));
        }

        [Test]
        public void TowerCombat_ProtectedMonsterDoesNotLoseHealthOrShowHitFeedback()
        {
            var monster = Spawn();
            Em.AddComponent<MonsterTag>(monster);
            Em.AddComponent<MonsterNavigationState>(monster);
            Em.SetComponentData(monster, new UnitStatusState { InvulnerableUntil = 3 });
            var tower = Em.CreateEntity(typeof(DefenseStructure));
            Em.SetComponentData(tower, new DefenseStructure { Min = new float2(-1), Max = new float2(1), Health = 100, Attack = 10, Range = 5, AttackInterval = 1 });
            var system = _world.GetOrCreateSystemManaged<StructureCombatSystem>();
            system.Update();
            Assert.That(Em.GetComponentData<Vitals>(monster).Health, Is.EqualTo(100));
            Assert.That(Em.GetComponentData<Vitals>(monster).LastHitTime, Is.EqualTo(-100));
            _world.SetTime(new TimeData(3, 0.02f));
            system.Update();
            Assert.That(Em.GetComponentData<Vitals>(monster).Health, Is.EqualTo(90));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Stun_StopsOnlyAffectedSideOfSoldierCombat_AndResumesAtExpiry(bool stunSoldier)
        {
            var soldier = Spawn();
            Em.AddComponent<SoldierState>(soldier);
            var monster = Spawn();
            Em.AddComponent<MonsterTag>(monster);
            Em.SetComponentData(stunSoldier ? soldier : monster, new UnitStatusState { StunnedUntil = 3 });
            var combat = _world.GetOrCreateSystemManaged<SoldierCombatSystem>();
            combat.Update();
            Assert.That(Em.GetComponentData<Vitals>(soldier).Health, Is.EqualTo(stunSoldier ? 90 : 100));
            Assert.That(Em.GetComponentData<Vitals>(monster).Health, Is.EqualTo(stunSoldier ? 100 : 90));
            Assert.That(Em.GetComponentData<Vitals>(stunSoldier ? soldier : monster).LastAttackTime, Is.EqualTo(-100));
            _world.SetTime(new TimeData(3, 0.02f));
            combat.Update();
            Assert.That(Em.GetComponentData<Vitals>(soldier).Health, Is.EqualTo(stunSoldier ? 80 : 90));
            Assert.That(Em.GetComponentData<Vitals>(monster).Health, Is.EqualTo(stunSoldier ? 90 : 80));
        }

        [Test]
        public void RootAndSilence_DoNotPreventNormalAttacks()
        {
            var soldier = Spawn();
            Em.AddComponent<SoldierState>(soldier);
            var monster = Spawn();
            Em.AddComponent<MonsterTag>(monster);
            var status = new UnitStatusState { RootedUntil = 3, SilencedUntil = 3 };
            Em.SetComponentData(soldier, status);
            Em.SetComponentData(monster, status);
            _world.GetOrCreateSystemManaged<SoldierCombatSystem>().Update();
            Assert.That(Em.GetComponentData<Vitals>(soldier).Health, Is.EqualTo(90));
            Assert.That(Em.GetComponentData<Vitals>(monster).Health, Is.EqualTo(90));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ContactStun_RespectsCooldownAndFirstHeroEngagementDelay(bool stunHero)
        {
            Em.CreateEntity(typeof(ContactStatistics));
            var hero = Spawn(); Em.AddComponent<HeroTag>(hero);
            var monster = Spawn(); Em.AddComponent<MonsterTag>(monster); Em.AddComponent<HeroContact>(monster);
            Em.SetComponentData(stunHero ? hero : monster, new UnitStatusState { StunnedUntil = 3 });
            var contact = _world.GetOrCreateSystem<CircleContactSystem>();
            contact.Update(_world.Unmanaged);
            Assert.That(Em.GetComponentData<Vitals>(hero).Health, Is.EqualTo(100));
            Assert.That(Em.GetComponentData<Vitals>(monster).Health, Is.EqualTo(stunHero ? 100 : 90));
            _world.SetTime(new TimeData(2, 0.02f)); contact.Update(_world.Unmanaged);
            Assert.That(Em.GetComponentData<Vitals>(hero).Health, Is.EqualTo(stunHero ? 90 : 100));
            _world.SetTime(new TimeData(3, 0.02f)); contact.Update(_world.Unmanaged);
            Assert.That(Em.GetComponentData<Vitals>(hero).Health, Is.EqualTo(stunHero ? 80 : 100));
            Assert.That(Em.GetComponentData<Vitals>(monster).Health, Is.EqualTo(stunHero ? 90 : 70));
            _world.SetTime(new TimeData(4, 0.02f)); contact.Update(_world.Unmanaged);
            Assert.That(Em.GetComponentData<Vitals>(hero).Health, Is.EqualTo(stunHero ? 70 : 90));
            Assert.That(Em.GetComponentData<Vitals>(monster).Health, Is.EqualTo(stunHero ? 80 : 60));
        }

        [Test]
        public void PoisonCatchup_ChecksInvulnerabilityAtEachTick_AndBypassesDefense()
        {
            var unit = Spawn();
            var vital = Em.GetComponentData<Vitals>(unit);
            vital.Defense = 1000;
            Em.SetComponentData(unit, vital);
            Em.SetComponentData(unit, new UnitStatusState
            {
                InvulnerableUntil = 2, RootedUntil = 5,
                PoisonedUntil = 3, NextPoisonTickAt = 1, PoisonIntervalSeconds = 1, PoisonDamagePerTick = 7
            });
            _world.SetTime(new TimeData(3.5, 0.02f));
            var system = _world.GetOrCreateSystemManaged<UnitStatusSystem>();
            system.Update();
            Assert.That(Em.GetComponentData<Vitals>(unit).Health, Is.EqualTo(86));
            Assert.That(Em.GetComponentData<Vitals>(unit).LastHitTime, Is.EqualTo(3));
            Assert.That(Em.GetComponentData<UnitStatusState>(unit).IsRootedAt(3.5), Is.True);
            system.Update();
            Assert.That(Em.GetComponentData<Vitals>(unit).Health, Is.EqualTo(86));
        }

        [Test]
        public void Root_StillBlocksNeighbors_ButReceivesNoCrowdingDisplacement()
        {
            var rooted = Spawn();
            Em.AddComponent<MonsterTag>(rooted);
            Em.AddComponentData(rooted, new UnitIdentity { Id = 2, Kind = UnitKind.Goblin });
            Em.SetComponentData(rooted, new UnitStatusState { RootedUntil = 3 });
            var other = Spawn();
            Em.AddComponent<SoldierState>(other);
            Em.AddComponentData(other, new UnitIdentity { Id = 3, Kind = UnitKind.Soldier });
            var separation = _world.GetOrCreateSystemManaged<MonsterSeparationSystem>();
            separation.Configure(0.1f, 3);
            separation.Update();
            Assert.That(Em.GetComponentData<GroundTransform>(rooted).Position, Is.EqualTo(float2.zero));
            Assert.That(Em.GetComponentData<GroundTransform>(other).Position, Is.Not.EqualTo(float2.zero));
        }

        [Test]
        public void StunnedMonster_CannotDamageCastle_UntilControlExpires()
        {
            var monster = Spawn();
            Em.AddComponent<MonsterTag>(monster);
            var castle = Em.CreateEntity(typeof(DefenseStructure));
            Em.SetComponentData(castle, new DefenseStructure { Min = new float2(-1), Max = new float2(1), Health = 100 });
            Em.AddComponentData(monster, new MonsterNavigationState { StructureTarget = castle });
            Em.SetComponentData(monster, new UnitStatusState { StunnedUntil = 3 });
            var combat = _world.GetOrCreateSystemManaged<StructureCombatSystem>();
            combat.Update();
            Assert.That(Em.GetComponentData<DefenseStructure>(castle).Health, Is.EqualTo(100));
            _world.SetTime(new TimeData(3, 0.02f));
            combat.Update();
            Assert.That(Em.GetComponentData<DefenseStructure>(castle).Health, Is.EqualTo(90));
        }
    }
}
