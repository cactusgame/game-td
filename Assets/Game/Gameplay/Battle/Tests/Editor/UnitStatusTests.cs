using Game.Battle.Simulation.Components;
using Game.Battle.Simulation.Systems;
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
            var entity = Em.CreateEntity(typeof(GroundTransform), typeof(Vitals), typeof(UnitMotion), typeof(CombatProfile), typeof(UnitStatusState));
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
    }
}
