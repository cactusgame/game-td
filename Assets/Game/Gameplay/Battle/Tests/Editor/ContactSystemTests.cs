using System;
using System.Collections.Generic;
using System.IO;
using Game.Battle.Simulation.Components;
using Game.Battle.Simulation.Systems;
using Game.Infrastructure.Configuration;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Tests
{
    // 使用独立测试 World 验证真实 ECS 组件读写；不依赖场景 GameObject 或 Sprite。
    // SetUp 建立数据与系统，TearDown 释放原生资源，避免测试状态进入正式对局。
    public sealed class ContactSystemTests
    {
        private World _world;
        private BattleSystemGroup _group;
        private Entity _hero, _monster;
        [SetUp]
        public void SetUp()
        {
            _world = new World("Contact tests");
            _group = _world.GetOrCreateSystemManaged<BattleSystemGroup>();
            _group.AddSystemToUpdateList(_world.GetOrCreateSystem<MovementSystem>());
            _group.AddSystemToUpdateList(_world.GetOrCreateSystem<CircleContactSystem>());
            // Fixture units omit UnitIdentity/WanderState, so the movement query matches none.
            _group.SortSystems();
            var em = _world.EntityManager;
            em.CreateEntity(typeof(ContactStatistics), typeof(BattleSettings));
            _hero = em.CreateEntity(typeof(HeroTag), typeof(GroundTransform), typeof(UnitMotion), typeof(Vitals));
            _monster = em.CreateEntity(typeof(MonsterTag), typeof(GroundTransform), typeof(UnitMotion), typeof(Vitals), typeof(HeroContact));
            em.SetComponentData(_hero, new UnitMotion{Radius = 0.5f});
            em.SetComponentData(_monster, new UnitMotion{Radius = 0.5f});
            em.SetComponentData(_hero, new Vitals{Health = 100, MaxHealth = 100, Attack = 15, Defense = 3});
            em.SetComponentData(_monster, new Vitals{Health = 25, MaxHealth = 25, Attack = 9, Defense = 2});
            MoveMonster(0.5f);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();
        private void Step()
        {
            _world.SetTime(new TimeData(1, 1f / 60));
            _group.Update();
        }

        private void MoveMonster(float x) => _world.EntityManager.SetComponentData(_monster, new GroundTransform{Position = new float2(x, 0), Previous = new float2(x, 0)});
        private int Hp(Entity entity) => _world.EntityManager.GetComponentData<Vitals>(entity).Health;
        [Test]
        public void OverlapDamagesOnceAndSeparationAllowsAnotherHit()
        {
            Step();
            Assert.That(Hp(_hero), Is.EqualTo(94));
            Assert.That(Hp(_monster), Is.EqualTo(12));
            Step();
            Assert.That(Hp(_hero), Is.EqualTo(94));
            Assert.That(Hp(_monster), Is.EqualTo(12));
            MoveMonster(3);
            Step();
            MoveMonster(0.5f);
            Step();
            Assert.That(Hp(_hero), Is.EqualTo(88));
            Assert.That(Hp(_monster), Is.Zero);
            Step();
            Assert.That(Hp(_hero), Is.EqualTo(88));
        }

        [Test]
        public void HeroExchangePublishesAttackHurtAndFacingForBothLivingUnits()
        {
            Step();
            var hero = _world.EntityManager.GetComponentData<Vitals>(_hero);
            var monster = _world.EntityManager.GetComponentData<Vitals>(_monster);
            Assert.That(hero.Health, Is.GreaterThan(0));
            Assert.That(monster.Health, Is.GreaterThan(0));
            Assert.That(hero.LastAttackTime, Is.EqualTo(1));
            Assert.That(hero.LastHitTime, Is.EqualTo(1));
            Assert.That(monster.LastAttackTime, Is.EqualTo(1));
            Assert.That(monster.LastHitTime, Is.EqualTo(1));
            Assert.That(_world.EntityManager.GetComponentData<GroundTransform>(_monster).FacingX, Is.EqualTo(-1));
        }

        [Test]
        public void LethalExchangeIsSimultaneous()
        {
            var hero = _world.EntityManager.GetComponentData<Vitals>(_hero);
            hero.Health = 5;
            var monster = _world.EntityManager.GetComponentData<Vitals>(_monster);
            monster.Health = 5;
            _world.EntityManager.SetComponentData(_hero, hero);
            _world.EntityManager.SetComponentData(_monster, monster);
            Step();
            Assert.That(Hp(_hero), Is.Zero);
            Assert.That(Hp(_monster), Is.Zero);
        }

        [Test]
        public void BlockedDamageStillPublishesAttackWithoutHurtAndOverlapDoesNotRetrigger()
        {
            var em = _world.EntityManager;
            var hero = em.GetComponentData<Vitals>(_hero);
            var monster = em.GetComponentData<Vitals>(_monster);
            hero.Defense = monster.Defense = 100;
            hero.LastHitTime = monster.LastHitTime = -100;
            em.SetComponentData(_hero, hero);
            em.SetComponentData(_monster, monster);
            Step();
            Assert.That(em.GetComponentData<Vitals>(_hero).LastAttackTime, Is.EqualTo(1));
            Assert.That(em.GetComponentData<Vitals>(_monster).LastAttackTime, Is.EqualTo(1));
            Assert.That(em.GetComponentData<Vitals>(_hero).LastHitTime, Is.EqualTo(-100));
            Assert.That(em.GetComponentData<Vitals>(_monster).LastHitTime, Is.EqualTo(-100));
            _world.SetTime(new TimeData(2, 1f / 60));
            _group.Update();
            Assert.That(em.GetComponentData<Vitals>(_hero).LastAttackTime, Is.EqualTo(1));
            Assert.That(em.GetComponentData<Vitals>(_monster).LastAttackTime, Is.EqualTo(1));
        }

        [Test]
        public void DeadHeroCannotDealDamage()
        {
            var hero = _world.EntityManager.GetComponentData<Vitals>(_hero);
            hero.Health = 0;
            _world.EntityManager.SetComponentData(_hero, hero);
            Step();
            Assert.That(Hp(_monster), Is.EqualTo(25));
        }

        [Test]
        public void MultipleExtremeHitsCannotOverflowHeroDamage()
        {
            var monster = _world.EntityManager.GetComponentData<Vitals>(_monster);
            monster.Attack = int.MaxValue;
            _world.EntityManager.SetComponentData(_monster, monster);
            _world.EntityManager.Instantiate(_monster);
            Step();
            Assert.That(Hp(_hero), Is.Zero);
        }
    }
}
