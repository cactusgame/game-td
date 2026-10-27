using Game.Battle.Simulation.Components;
using Game.Battle.Simulation.Systems;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Tests
{
    public sealed class ContactSystemTests
    {
        private World _world;
        private Entity _hero, _monster;
        private EntityManager Em => _world.EntityManager;
        [SetUp]
        public void SetUp()
        {
            _world = new World("Normal attack contact tests");
            Em.CreateEntity(typeof(ContactStatistics));
            _hero = Spawn(true, 0);
            _monster = Spawn(false, 0.5f);
        }
        [TearDown]
        public void TearDown() => _world.Dispose();
        private Entity Spawn(bool hero, float x)
        {
            var entity = Em.CreateEntity(typeof(GroundTransform), typeof(UnitMotion), typeof(Vitals), typeof(CombatProfile), typeof(CombatTargetState));
            Em.SetComponentData(entity, new GroundTransform { Position = new float2(x, 0), Previous = new float2(x, 0) });
            Em.SetComponentData(entity, new UnitMotion { Radius = 0.5f });
            Em.SetComponentData(entity, new CombatProfile { AttackInterval = 1 });
            Em.SetComponentData(entity, new Vitals { Health = 100, MaxHealth = 100, Attack = hero ? 15 : 9, Defense = hero ? 3 : 2, LastAttackTime = -100, LastHitTime = -100 });
            if (hero) Em.AddComponent<HeroTag>(entity);
            else { Em.AddComponent<MonsterTag>(entity); Em.AddComponent<HeroContact>(entity); }
            return entity;
        }
        private void Step(double now)
        {
            _world.SetTime(new TimeData(now, 0.02f));
            _world.GetOrCreateSystem<CircleContactSystem>().Update(_world.Unmanaged);
        }
        private int Hp(Entity entity) => Em.GetComponentData<Vitals>(entity).Health;
        private void Move(float x) => Em.SetComponentData(_monster, new GroundTransform { Position = new float2(x, 0), Previous = new float2(x, 0) });

        [Test]
        public void ContinuousOverlap_HeroAttacksFirst_EnemyStartsAfterOneInterval()
        {
            Step(1);
            Assert.That(Hp(_hero), Is.EqualTo(100));
            Assert.That(Hp(_monster), Is.EqualTo(87));
            Assert.That(Em.GetComponentData<Vitals>(_monster).LastAttackTime, Is.EqualTo(-100));
            Step(1.5);
            Assert.That(Hp(_monster), Is.EqualTo(87));
            Step(2);
            Assert.That(Hp(_hero), Is.EqualTo(94));
            Assert.That(Hp(_monster), Is.EqualTo(74));
            Assert.That(Em.GetComponentData<GroundTransform>(_monster).FacingX, Is.EqualTo(-1));
            Step(2);
            Assert.That(Hp(_hero), Is.EqualTo(94));
            Step(3);
            Assert.That(Hp(_hero), Is.EqualTo(88));
            Assert.That(Hp(_monster), Is.EqualTo(61));
        }

        [Test]
        public void Reentry_DoesNotRefreshHeroCooldown_AndEnemyReengagementStillWaits()
        {
            Step(1);
            Move(3); Step(1.1);
            Move(0.5f); Step(1.2);
            Assert.That(Hp(_monster), Is.EqualTo(87));
            Step(2);
            Assert.That(Hp(_monster), Is.EqualTo(74));
            Assert.That(Hp(_hero), Is.EqualTo(100));
            Step(2.2);
            Assert.That(Hp(_hero), Is.EqualTo(94));
        }

        [Test]
        public void HeroLethalAttack_PreventsSameStepEnemyRetaliation()
        {
            var hero = Em.GetComponentData<Vitals>(_hero); hero.Health = 5; Em.SetComponentData(_hero, hero);
            var monster = Em.GetComponentData<Vitals>(_monster); monster.Health = 5; Em.SetComponentData(_monster, monster);
            Step(1);
            Assert.That(Hp(_hero), Is.EqualTo(5));
            Assert.That(Hp(_monster), Is.Zero);
            Assert.That(Em.GetComponentData<Vitals>(_monster).LastAttackTime, Is.EqualTo(-100));
        }

        [Test]
        public void BlockedDamage_StillConsumesAttackIntervalAndPublishesAttack()
        {
            foreach (var entity in new[] { _hero, _monster })
            {
                var vital = Em.GetComponentData<Vitals>(entity); vital.Defense = 100; Em.SetComponentData(entity, vital);
            }
            Step(1);
            Assert.That(Em.GetComponentData<Vitals>(_hero).LastAttackTime, Is.EqualTo(1));
            Assert.That(Em.GetComponentData<Vitals>(_monster).LastAttackTime, Is.EqualTo(-100));
            Step(1.5);
            Assert.That(Em.GetComponentData<Vitals>(_hero).LastAttackTime, Is.EqualTo(1));
            Step(2);
            Assert.That(Em.GetComponentData<Vitals>(_hero).LastAttackTime, Is.EqualTo(2));
            Assert.That(Em.GetComponentData<Vitals>(_monster).LastAttackTime, Is.EqualTo(2));
            Assert.That(Em.GetComponentData<Vitals>(_hero).LastHitTime, Is.EqualTo(-100));
            Assert.That(Em.GetComponentData<Vitals>(_monster).LastHitTime, Is.EqualTo(-100));
        }

        [Test]
        public void HeroTarget_RemainsWhileValid_AndSwitchingDoesNotRefreshCooldown()
        {
            Step(1);
            var closer = Spawn(false, 0.1f);
            Step(2);
            Assert.That(Hp(_monster), Is.EqualTo(74));
            Assert.That(Hp(closer), Is.EqualTo(100));
            var dead = Em.GetComponentData<Vitals>(_monster); dead.Health = 0; Em.SetComponentData(_monster, dead);
            Step(2.1);
            Assert.That(Hp(closer), Is.EqualTo(100));
            Step(3);
            Assert.That(Hp(closer), Is.EqualTo(87));
        }

        [Test]
        public void DeadHero_CannotDealDamage()
        {
            var hero = Em.GetComponentData<Vitals>(_hero); hero.Health = 0; Em.SetComponentData(_hero, hero);
            Step(1);
            Assert.That(Hp(_monster), Is.EqualTo(100));
        }

        [Test]
        public void MultipleExtremeHits_CannotOverflowHeroDamage()
        {
            var monster = Em.GetComponentData<Vitals>(_monster); monster.Attack = int.MaxValue; Em.SetComponentData(_monster, monster);
            Em.Instantiate(_monster);
            Step(1);
            Assert.That(Hp(_hero), Is.EqualTo(100));
            Step(2);
            Assert.That(Hp(_hero), Is.Zero);
        }
    }
}
