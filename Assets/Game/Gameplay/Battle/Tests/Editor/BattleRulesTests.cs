using System;
using System.Collections.Generic;
using System.IO;
using Game.Battle.Contracts;
using Game.Battle.Domain;
using Game.Battle.Simulation;
using Game.Infrastructure.Configuration;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Tests
{
    // 阅读测试可了解规则期望：纯公式、JSON 校验，以及完整模拟中的移动/重启行为。
    public sealed class BattleRulesTests
    {
        [TestCase(15, 2, 13)]
        [TestCase(5, 9, 0)]
        [TestCase(0, 0, 0)]
        public void DefenseCannotTurnDamageIntoHealing(int attack, int defense, int expected)
            => Assert.That(ContactRules.Damage(attack, defense), Is.EqualTo(expected));

        [Test] public void LethalDamageClampsToZero()
            => Assert.That(ContactRules.ApplyDamage(12, 100), Is.Zero);

        [Test] public void SweptCircleDetectsFastCrossing()
            => Assert.That(ContactRules.SweptCircles(-3, 0, 3, 0, 0.5f), Is.True);

        [Test] public void SweptCircleDoesNotHitParallelMiss()
            => Assert.That(ContactRules.SweptCircles(-3, 2, 3, 2, 0.5f), Is.False);

        [Test] public void JsonLoadsAllThreeKindsAndTenMonsters()
        {
            var definition = JsonBattleDefinitionSource.Parse(File.ReadAllText("Assets/Content/Combat/Prototype/battle.json"));
            Assert.That(definition.MonsterCount, Is.EqualTo(10));
            Assert.That(definition.Runner.Speed, Is.GreaterThan(definition.Goblin.Speed));
            Assert.That(definition.Runner.Radius, Is.LessThan(definition.Goblin.Radius));
        }

        [Test] public void InvalidHealthAndNonfiniteSpeedAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new UnitDefinition(UnitKind.Hero, 0, 1, 0, 1, 0.4f, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new UnitDefinition(UnitKind.Hero, 1, 1, 0, float.NaN, 0.4f, 1));
        }

        [Test] public void UnsupportedJsonSchemaIsRejected()
            => Assert.Throws<FormatException>(() => JsonBattleDefinitionSource.Parse("{\"schemaVersion\":2}"));

        [Test] public void ArenaWithoutSafeSpawnIsRejectedInsteadOfHanging()
        {
            var hero = new UnitDefinition(UnitKind.Hero, 100, 15, 3, 4.5f, 2, 1.5f);
            var goblin = new UnitDefinition(UnitKind.Goblin, 25, 9, 2, 1.25f, 2, 1.3f);
            var runner = new UnitDefinition(UnitKind.Runner, 12, 7, 0, 3, 0.24f, 0.95f);
            Assert.Throws<ArgumentException>(() => new BattleDefinition(hero, goblin, runner, 1, 0, 6.1f, 6.1f, 1f / 60, 1));
        }

        [Test] public void DiagonalInputHasSameSpeedAndRestartIsReproducible()
        {
            var hero = new UnitDefinition(UnitKind.Hero, 100, 15, 3, 4.5f, 0.4f, 1.5f);
            var goblin = new UnitDefinition(UnitKind.Goblin, 25, 9, 2, 1.25f, 0.4f, 1.3f);
            var runner = new UnitDefinition(UnitKind.Runner, 12, 7, 0, 3, 0.24f, 0.95f);
            var definition = new BattleDefinition(hero, goblin, runner, 0, 0, 26, 14, 1f / 60, 123);
            using var simulation = new EcsBattleSimulation();
            var units = new List<UnitSnapshot>();
            simulation.Reset(definition);
            for (int i = 0; i < 60; i++) simulation.Step(1f / 60, new MoveIntent(1, 1));
            simulation.CopyUnits(units);
            Assert.That(math.length(new float2(units[0].X, units[0].Y)), Is.EqualTo(4.5f).Within(0.001));
            simulation.Reset(definition);
            simulation.CopyUnits(units);
            Assert.That(units[0].X, Is.Zero);
            Assert.That(units[0].Health, Is.EqualTo(100));
            Assert.That(units[0].LastAttackTime, Is.EqualTo(-100));
            Assert.That(units[0].LastHitTime, Is.EqualTo(-100));
            Assert.That(simulation.ElapsedTime, Is.Zero);
        }

        [Test] public void RandomMovementRemainsInBoundsAndResetsItsRandomStream()
        {
            var definition = JsonBattleDefinitionSource.Parse(File.ReadAllText("Assets/Content/Combat/Prototype/battle.json"));
            using var simulation = new EcsBattleSimulation();
            var first = new List<UnitSnapshot>(); var second = new List<UnitSnapshot>();
            simulation.Reset(definition);
            for (int i = 0; i < 600; i++) simulation.Step(definition.FixedStep, new MoveIntent(0, 0));
            simulation.CopyUnits(first);
            simulation.Reset(definition);
            for (int i = 0; i < 600; i++) simulation.Step(definition.FixedStep, new MoveIntent(0, 0));
            simulation.CopyUnits(second);
            Assert.That(first.Count, Is.EqualTo(11));
            for (int i = 0; i < first.Count; i++)
            {
                float radius = definition.GetUnit(first[i].Kind).Radius;
                Assert.That(math.abs(first[i].X), Is.LessThanOrEqualTo(definition.Width / 2 - radius + 0.001f));
                Assert.That(math.abs(first[i].Y), Is.LessThanOrEqualTo(definition.Height / 2 - radius + 0.001f));
                Assert.That(first[i].X, Is.EqualTo(second[i].X));
                Assert.That(first[i].Y, Is.EqualTo(second[i].Y));
                Assert.That(first[i].Health, Is.EqualTo(second[i].Health));
            }
        }
    }

    // 使用独立测试 World 验证真实 ECS 组件读写；不依赖场景 GameObject 或 Sprite。
    // SetUp 建立数据与系统，TearDown 释放原生资源，避免测试状态进入正式对局。
    public sealed class ContactSystemTests
    {
        private World _world;
        private BattleSystemGroup _group;
        private Entity _hero, _monster;
        [SetUp] public void SetUp()
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
            em.SetComponentData(_hero, new UnitMotion { Radius = 0.5f });
            em.SetComponentData(_monster, new UnitMotion { Radius = 0.5f });
            em.SetComponentData(_hero, new Vitals { Health = 100, MaxHealth = 100, Attack = 15, Defense = 3 });
            em.SetComponentData(_monster, new Vitals { Health = 25, MaxHealth = 25, Attack = 9, Defense = 2 });
            MoveMonster(0.5f);
        }
        [TearDown] public void TearDown() => _world.Dispose();
        private void Step() { _world.SetTime(new TimeData(1, 1f / 60)); _group.Update(); }
        private void MoveMonster(float x) => _world.EntityManager.SetComponentData(_monster,
            new GroundTransform { Position = new float2(x, 0), Previous = new float2(x, 0) });
        private int Hp(Entity entity) => _world.EntityManager.GetComponentData<Vitals>(entity).Health;

        [Test] public void OverlapDamagesOnceAndSeparationAllowsAnotherHit()
        {
            Step(); Assert.That(Hp(_hero), Is.EqualTo(94)); Assert.That(Hp(_monster), Is.EqualTo(12));
            Step(); Assert.That(Hp(_hero), Is.EqualTo(94)); Assert.That(Hp(_monster), Is.EqualTo(12));
            MoveMonster(3); Step(); MoveMonster(0.5f); Step();
            Assert.That(Hp(_hero), Is.EqualTo(88)); Assert.That(Hp(_monster), Is.Zero);
            Step(); Assert.That(Hp(_hero), Is.EqualTo(88));
        }

        [Test] public void LethalExchangeIsSimultaneous()
        {
            var hero = _world.EntityManager.GetComponentData<Vitals>(_hero); hero.Health = 5;
            var monster = _world.EntityManager.GetComponentData<Vitals>(_monster); monster.Health = 5;
            _world.EntityManager.SetComponentData(_hero, hero); _world.EntityManager.SetComponentData(_monster, monster);
            Step(); Assert.That(Hp(_hero), Is.Zero); Assert.That(Hp(_monster), Is.Zero);
        }

        [Test] public void BlockedDamageStillPublishesAttackWithoutHurtAndOverlapDoesNotRetrigger()
        {
            var em = _world.EntityManager;
            var hero = em.GetComponentData<Vitals>(_hero);
            var monster = em.GetComponentData<Vitals>(_monster);
            hero.Defense = monster.Defense = 100;
            hero.LastHitTime = monster.LastHitTime = -100;
            em.SetComponentData(_hero, hero); em.SetComponentData(_monster, monster);
            Step();
            Assert.That(em.GetComponentData<Vitals>(_hero).LastAttackTime, Is.EqualTo(1));
            Assert.That(em.GetComponentData<Vitals>(_monster).LastAttackTime, Is.EqualTo(1));
            Assert.That(em.GetComponentData<Vitals>(_hero).LastHitTime, Is.EqualTo(-100));
            Assert.That(em.GetComponentData<Vitals>(_monster).LastHitTime, Is.EqualTo(-100));
            _world.SetTime(new TimeData(2, 1f / 60)); _group.Update();
            Assert.That(em.GetComponentData<Vitals>(_hero).LastAttackTime, Is.EqualTo(1));
            Assert.That(em.GetComponentData<Vitals>(_monster).LastAttackTime, Is.EqualTo(1));
        }

        [Test] public void DeadHeroCannotDealDamage()
        {
            var hero = _world.EntityManager.GetComponentData<Vitals>(_hero); hero.Health = 0;
            _world.EntityManager.SetComponentData(_hero, hero);
            Step(); Assert.That(Hp(_monster), Is.EqualTo(25));
        }

        [Test] public void MultipleExtremeHitsCannotOverflowHeroDamage()
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
