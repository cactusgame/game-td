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
    public sealed class HeroRespawnTests
    {
        [Test]
        public void GhostCannotAttackCastSkillsOrCallSoldiers()
        {
            var hero = new UnitDefinition(UnitKind.Hero, 1, 7, 0, 4, 0.3f, 1,
                skills: new[] { new HeroAreaSkillDefinition("burst", true, 8, 30, 3) });
            var enemy = new UnitDefinition(UnitKind.Goblin, 1000, 10, 0, 0.01f, 0.3f, 1);
            var runner = new UnitDefinition(UnitKind.Runner, 100, 0, 0, 1, 0.3f, 1);
            using var simulation = new EcsBattleSimulation();
            simulation.Reset(new BattleDefinition(hero, enemy, runner, 1, 0, 40, 40, 0.02f, 7,
                heroSpawn: new MapPoint(2, 3), enemySpawns: new[] { new MapPoint(2, 3) },
                soldiers: new[] { new UnitDefinition(UnitKind.Soldier, 100, 0, 0, 2, 0.2f, 1) }));
            Assert.That(simulation.TrySpawnSoldier(UnitKind.Soldier, new MapPoint(2, 5), new MapPoint(2, 5), out _), Is.True);
            simulation.Step(0.02f, default);
            var units = new List<UnitSnapshot>();
            simulation.CopyUnits(units);
            int health = units.Find(u => u.Kind == UnitKind.Goblin).Health;
            simulation.Execute(PlayerOperation.SkillOne);
            simulation.Execute(PlayerOperation.CallAll);
            simulation.Execute(PlayerOperation.NearbyTap);
            simulation.Step(0.02f, new MoveIntent(1, 0));
            simulation.CopyUnits(units);
            Assert.That(units.Find(u => u.Kind == UnitKind.Hero).IsGhost, Is.True);
            Assert.That(units.Find(u => u.Kind == UnitKind.Goblin).Health, Is.EqualTo(health));
            Assert.That(simulation.PlayerControls.SelectedCount, Is.Zero);
            Assert.That(simulation.PlayerControls.SkillOneRemaining, Is.Zero);
        }

        [TestCase(0.5f)]
        [TestCase(10f)]
        public void Respawn_WaitsConfiguredTimeAndRestoresSameHeroAtGhostPositionRepeatedly(float seconds)
        {
            using var world = new World("Hero respawn behavior");
            var em = world.EntityManager;
            var hero = em.CreateEntity(typeof(HeroTag), typeof(Vitals), typeof(GroundTransform), typeof(HeroRespawnState), typeof(UnitStatusState));
            var system = world.GetOrCreateSystemManaged<HeroRespawnSystem>();
            em.SetComponentData(hero, new HeroRespawnState { DelaySeconds = seconds, InvulnerabilitySeconds = 2 });
            double now = 1;
            for (int life = 0; life < 4; life++)
            {
                var deathPosition = new float2(life + 2, -life - 3);
                em.SetComponentData(hero, new GroundTransform { Position = deathPosition, Previous = deathPosition, FacingX = -1 });
                em.SetComponentData(hero, new Vitals { Health = 0, MaxHealth = 100, LastHitTime = (float)now });
                world.SetTime(new TimeData(now, 0.02f));
                system.Update();
                double readyAt = em.GetComponentData<HeroRespawnState>(hero).ReadyAt;
                Assert.That(readyAt, Is.EqualTo(now + seconds));
                world.SetTime(new TimeData(readyAt - 0.001, 0.02f));
                system.Update();
                Assert.That(em.GetComponentData<Vitals>(hero).Health, Is.Zero);
                Assert.That(em.GetComponentData<HeroRespawnState>(hero).ReadyAt, Is.EqualTo(readyAt), "死亡状态不能重复重置倒计时");
                var ghostPosition = deathPosition + new float2(3, 2);
                em.SetComponentData(hero, new GroundTransform { Position = ghostPosition, Previous = ghostPosition });
                world.SetTime(new TimeData(readyAt, 0.02f));
                system.Update();
                Assert.That(em.GetComponentData<Vitals>(hero).Health, Is.EqualTo(100));
                Assert.That(em.GetComponentData<GroundTransform>(hero).Position, Is.EqualTo(ghostPosition));
                Assert.That(em.GetComponentData<GroundTransform>(hero).Previous, Is.EqualTo(ghostPosition));
                Assert.That(em.GetComponentData<HeroRespawnState>(hero).DeathPosition, Is.EqualTo(deathPosition));
                Assert.That(em.GetComponentData<HeroRespawnState>(hero).DeathSequence, Is.EqualTo(life + 1));
                Assert.That(em.GetComponentData<UnitStatusState>(hero).IsInvulnerableAt(readyAt + 1.999), Is.True);
                Assert.That(em.GetComponentData<UnitStatusState>(hero).IsInvulnerableAt(readyAt + 2), Is.False);
                Assert.That(em.GetComponentData<HeroRespawnState>(hero).IsWaiting, Is.False);
                now = readyAt + 1;
            }
        }

        [Test]
        public void FixedStep_GhostMovesWithoutMovingCorpseAndRevivesWithProtection()
        {
            var hero = new UnitDefinition(UnitKind.Hero, 1, 0, 0, 4, 0.3f, 1);
            var enemy = new UnitDefinition(UnitKind.Goblin, 100, 10, 0, 0.01f, 0.3f, 1);
            var runner = new UnitDefinition(UnitKind.Runner, 100, 0, 0, 1, 0.3f, 1);
            var definition = new BattleDefinition(hero, enemy, runner, 1, 0, 40, 40, 0.02f, 7,
                heroSpawn: new MapPoint(2, 3), enemySpawns: new[] { new MapPoint(2, 3) }, heroRespawnSeconds: 0.2f);
            using var simulation = new EcsBattleSimulation();
            simulation.Reset(definition);
            var units = new List<UnitSnapshot>();
            UnitSnapshot ReadHero()
            {
                simulation.CopyUnits(units);
                return units.Find(u => u.Kind == UnitKind.Hero);
            }
            simulation.Step(0.02f, default);
            var dead = ReadHero();
            Assert.That(dead.Health, Is.Zero);
            Assert.That(dead.RespawnRemainingSeconds, Is.EqualTo(0.2).Within(0.0001));
            // 不推进模拟时间时，读取与渲染不会消耗倒计时。
            Assert.That(ReadHero().RespawnRemainingSeconds, Is.EqualTo(dead.RespawnRemainingSeconds));
            simulation.Step(0.02f, new MoveIntent(1, 0));
            Assert.That(ReadHero().X, Is.GreaterThan(dead.X));
            Assert.That(ReadHero().DeathX, Is.EqualTo(dead.X));
            Assert.That(ReadHero().IsGhost, Is.True);
            Assert.That(ReadHero().Y, Is.EqualTo(dead.Y));
            Assert.That(ReadHero().RespawnRemainingSeconds, Is.LessThan(dead.RespawnRemainingSeconds));
            for (int i = 0; i < 12 && ReadHero().Health <= 0; i++) simulation.Step(0.02f, new MoveIntent(1, 0));
            var revived = ReadHero();
            Assert.That(revived.Health, Is.EqualTo(hero.MaxHealth));
            Assert.That(revived.Id, Is.EqualTo(dead.Id));
            Assert.That(revived.X, Is.GreaterThan(dead.X));
            Assert.That(revived.IsGhost, Is.False);
            Assert.That(revived.InvulnerabilityRemainingSeconds, Is.EqualTo(2).Within(0.001));
            Assert.That(revived.Y, Is.EqualTo(dead.Y));
            Assert.That(revived.RespawnRemainingSeconds, Is.Zero);
            simulation.Reset(definition);
            Assert.That(ReadHero().Health, Is.EqualTo(hero.MaxHealth));
            Assert.That(ReadHero().RespawnRemainingSeconds, Is.Zero);
        }
    }
}
