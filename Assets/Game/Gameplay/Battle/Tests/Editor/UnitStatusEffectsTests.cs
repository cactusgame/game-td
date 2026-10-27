using System;
using System.Collections.Generic;
using Game.Battle.Contracts.Commands;
using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.Definitions;
using Game.Battle.Domain.ValueObjects;
using Game.Battle.Simulation.Bridges;
using Game.MapEngine.Domain;
using NUnit.Framework;

namespace Game.Battle.Tests
{
    /// <summary>从公开效果入口到真实固定步与快照，覆盖各阵营和控制/伤害生命周期。</summary>
    public sealed class UnitStatusEffectsTests
    {
        private static BattleDefinition Definition()
        {
            var hero = new UnitDefinition(UnitKind.Hero, 100, 0, 0, 2, 0.2f, 1,
                skills: new[] { new HeroAreaSkillDefinition("test", true, 30, 10, 2) });
            return new BattleDefinition(hero,
                new UnitDefinition(UnitKind.Goblin, 100, 0, 0, 2, 0.2f, 1),
                new UnitDefinition(UnitKind.Runner, 100, 0, 0, 2, 0.2f, 1),
                1, 0, 40, 40, 0.02f, 7, heroSpawn: new MapPoint(-8, -8),
                enemySpawns: new[] { new MapPoint(6, 0) }, soldiers: new[]
                {
                    new UnitDefinition(UnitKind.Soldier, 100, 0, 0, 2, 0.2f, 1),
                    new UnitDefinition(UnitKind.Archer, 100, 0, 0, 2, 0.2f, 1)
                }, heroRespawnSeconds: 1);
        }

        private static EcsBattleSimulation Create()
        {
            var simulation = new EcsBattleSimulation();
            simulation.Reset(Definition());
            Assert.That(simulation.TrySpawnSoldier(UnitKind.Soldier, new MapPoint(0, 0), new MapPoint(3, 0), out _), Is.True);
            Assert.That(simulation.TrySpawnSoldier(UnitKind.Archer, new MapPoint(0, 3), new MapPoint(3, 3), out _), Is.True);
            return simulation;
        }

        private static UnitSnapshot Read(EcsBattleSimulation simulation, UnitKind kind)
        {
            var units = new List<UnitSnapshot>();
            simulation.CopyUnits(units);
            return units.Find(unit => unit.Kind == kind);
        }

        [TestCase(UnitKind.Hero, UnitStatusKind.Stun)]
        [TestCase(UnitKind.Soldier, UnitStatusKind.Stun)]
        [TestCase(UnitKind.Archer, UnitStatusKind.Stun)]
        [TestCase(UnitKind.Goblin, UnitStatusKind.Stun)]
        [TestCase(UnitKind.Hero, UnitStatusKind.Root)]
        [TestCase(UnitKind.Soldier, UnitStatusKind.Root)]
        [TestCase(UnitKind.Archer, UnitStatusKind.Root)]
        [TestCase(UnitKind.Goblin, UnitStatusKind.Root)]
        public void Control_BlocksAllMovementSources_ResumesAtExactExpiry(UnitKind kind, UnitStatusKind effect)
        {
            using var simulation = Create();
            var before = Read(simulation, kind);
            Assert.That(simulation.TryApplyStatus(new ApplyUnitStatusCommand(before.Id, effect, 1)), Is.True);
            simulation.Step(0.5f, new MoveIntent(1, 0));
            var held = Read(simulation, kind);
            Assert.That((held.X, held.Y), Is.EqualTo((before.X, before.Y)));
            Assert.That(held.CanMove, Is.False);
            Assert.That(Read(simulation, kind).Status.StunRemainingSeconds, Is.EqualTo(held.Status.StunRemainingSeconds), "查询不能推进模拟时间");
            simulation.Step(0.5f, new MoveIntent(1, 0));
            var released = Read(simulation, kind);
            Assert.That(released.CanMove, Is.True);
            Assert.That((released.X, released.Y), Is.Not.EqualTo((held.X, held.Y)));
        }

        [TestCase(UnitStatusKind.Stun, false)]
        [TestCase(UnitStatusKind.Silence, false)]
        [TestCase(UnitStatusKind.Root, true)]
        public void Casting_ChecksSharedPermissions_BlockedCastDoesNotConsumeCooldown(UnitStatusKind effect, bool casts)
        {
            using var simulation = Create();
            simulation.TryApplyStatus(new ApplyUnitStatusCommand(1, effect, 1));
            simulation.Execute(PlayerOperation.SkillOne);
            Assert.That(Read(simulation, UnitKind.Goblin).Health, Is.EqualTo(casts ? 90 : 100));
            Assert.That(Read(simulation, UnitKind.Hero).SkillOneCooldown.IsCoolingDown, Is.EqualTo(casts));
            if (casts) return;
            Assert.That(Read(simulation, UnitKind.Hero).LastAttackTime, Is.EqualTo(-100));
            simulation.Step(1, default);
            simulation.Execute(PlayerOperation.SkillOne);
            Assert.That(Read(simulation, UnitKind.Goblin).Health, Is.EqualTo(90));
        }

        [Test]
        public void OverlappingControls_ExpireIndependently_AndShorterReapplicationDoesNotShorten()
        {
            using var simulation = Create();
            simulation.TryApplyStatus(new ApplyUnitStatusCommand(1, UnitStatusKind.Stun, 1));
            simulation.TryApplyStatus(new ApplyUnitStatusCommand(1, UnitStatusKind.Root, 2));
            simulation.TryApplyStatus(new ApplyUnitStatusCommand(1, UnitStatusKind.Silence, 3));
            simulation.TryApplyStatus(new ApplyUnitStatusCommand(1, UnitStatusKind.Silence, 0.5));
            simulation.Step(1, default);
            var hero = Read(simulation, UnitKind.Hero);
            Assert.That(hero.CanAttack, Is.True);
            Assert.That(hero.CanMove, Is.False);
            Assert.That(hero.CanCast, Is.False);
            simulation.Step(1, default);
            hero = Read(simulation, UnitKind.Hero);
            Assert.That(hero.CanMove, Is.True);
            Assert.That(hero.CanCast, Is.False);
            simulation.Step(1, default);
            Assert.That(Read(simulation, UnitKind.Hero).CanCast, Is.True);
        }

        [TestCase(UnitKind.Hero)]
        [TestCase(UnitKind.Soldier)]
        [TestCase(UnitKind.Archer)]
        [TestCase(UnitKind.Goblin)]
        public void Poison_AllFactions_CatchesUpThroughExpiryAndDoesNotTickTwice(UnitKind kind)
        {
            using var simulation = Create();
            ulong id = Read(simulation, kind).Id;
            Assert.That(simulation.TryApplyStatus(new ApplyUnitStatusCommand(id, UnitStatusKind.Poison, 3, 7, 1)), Is.True);
            simulation.Step(0.5f, default);
            Assert.That(Read(simulation, kind).Health, Is.EqualTo(100));
            simulation.Step(2, default);
            Assert.That(Read(simulation, kind).Health, Is.EqualTo(86));
            Assert.That(Read(simulation, kind).LastHitTime, Is.EqualTo(2));
            simulation.Step(1, default);
            Assert.That(Read(simulation, kind).Health, Is.EqualTo(79));
            Assert.That(Read(simulation, kind).Status.IsPoisoned, Is.False);
            simulation.Step(1, default);
            Assert.That(Read(simulation, kind).Health, Is.EqualTo(79));
        }

        [Test]
        public void Poison_Reapplication_ReplacesDamageAndRestartsCadence()
        {
            using var simulation = Create();
            simulation.TryApplyStatus(new ApplyUnitStatusCommand(1, UnitStatusKind.Poison, 4, 20, 1));
            simulation.Step(0.5f, default);
            simulation.TryApplyStatus(new ApplyUnitStatusCommand(1, UnitStatusKind.Poison, 2, 3, 0.5));
            simulation.Step(0.5f, default);
            Assert.That(Read(simulation, UnitKind.Hero).Health, Is.EqualTo(97));
            simulation.Step(1.5f, default);
            Assert.That(Read(simulation, UnitKind.Hero).Health, Is.EqualTo(88));
        }

        [TestCase(0.3, 0.1, 3)]
        [TestCase(0.6, 0.2, 3)]
        [TestCase(0.9, 0.3, 3)]
        [TestCase(0.09, 0.1, 0)]
        public void Poison_FractionalPeriods_DoNotLoseFinalTickAtExpiry(double duration, double interval, int ticks)
        {
            using var simulation = Create();
            simulation.TryApplyStatus(new ApplyUnitStatusCommand(1, UnitStatusKind.Poison, duration, 7, interval));
            simulation.Step(1, default);
            Assert.That(Read(simulation, UnitKind.Hero).Health, Is.EqualTo(100 - ticks * 7));
            Assert.That(Read(simulation, UnitKind.Hero).Status.IsPoisoned, Is.False);
        }

        [Test]
        public void Poison_MultipleStepsAndOneCatchupStep_ProduceSameFractionalPeriodDamage()
        {
            using var stepped = Create();
            using var catchup = Create();
            stepped.TryApplyStatus(new ApplyUnitStatusCommand(1, UnitStatusKind.Poison, 0.3, 7, 0.1));
            catchup.TryApplyStatus(new ApplyUnitStatusCommand(1, UnitStatusKind.Poison, 0.3, 7, 0.1));
            for (int i = 0; i < 3; i++) stepped.Step(0.125f, default);
            catchup.Step(0.375f, default);
            Assert.That(Read(stepped, UnitKind.Hero).Health, Is.EqualTo(79));
            Assert.That(Read(catchup, UnitKind.Hero).Health, Is.EqualTo(79));
        }

        [Test]
        public void PoisonDeath_ClearsEffects_AndRespawnDoesNotInheritThem()
        {
            using var simulation = Create();
            simulation.TryApplyStatus(new ApplyUnitStatusCommand(1, UnitStatusKind.Stun, 10));
            simulation.TryApplyStatus(new ApplyUnitStatusCommand(1, UnitStatusKind.Root, 10));
            simulation.TryApplyStatus(new ApplyUnitStatusCommand(1, UnitStatusKind.Silence, 10));
            simulation.TryApplyStatus(new ApplyUnitStatusCommand(1, UnitStatusKind.Poison, 10, 100, 1));
            simulation.Step(1, default);
            var hero = Read(simulation, UnitKind.Hero);
            Assert.That(hero.IsGhost, Is.True);
            Assert.That(hero.Status.IsStunned || hero.Status.IsPoisoned, Is.False);
            Assert.That(simulation.TryApplyStatus(new ApplyUnitStatusCommand(1, UnitStatusKind.Stun, 2)), Is.False);
            simulation.Step(1, new MoveIntent(1, 0));
            hero = Read(simulation, UnitKind.Hero);
            Assert.That(hero.Health, Is.EqualTo(100));
            Assert.That(hero.IsInvulnerable, Is.True);
            Assert.That(hero.CanMove && hero.CanCast && hero.CanAttack, Is.True);
            simulation.Step(3, default);
            Assert.That(Read(simulation, UnitKind.Hero).Health, Is.EqualTo(100));
        }

        [Test]
        public void StatusInput_InvalidOrMissingTarget_IsRejectedWithoutMutation()
        {
            using var simulation = Create();
            Assert.That(simulation.TryApplyStatus(default), Is.False);
            Assert.That(simulation.TryApplyStatus(new ApplyUnitStatusCommand(999, UnitStatusKind.Stun, 1)), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => new ApplyUnitStatusCommand(1, UnitStatusKind.Stun, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ApplyUnitStatusCommand(1, UnitStatusKind.Poison, 1, 1, 0));
            Assert.That(Read(simulation, UnitKind.Hero).CanMove, Is.True);
        }

        [Test]
        public void RecallDuringRoot_DoesNotTeleport_AndResumesCommandAfterExpiry()
        {
            using var simulation = Create();
            var soldier = Read(simulation, UnitKind.Soldier);
            simulation.TryApplyStatus(new ApplyUnitStatusCommand(soldier.Id, UnitStatusKind.Root, 1));
            simulation.Execute(PlayerOperation.CallAllMelee, instantDeployment: true);
            var held = Read(simulation, UnitKind.Soldier);
            Assert.That((held.X, held.Y), Is.EqualTo((soldier.X, soldier.Y)));
            Assert.That(held.Command, Is.EqualTo(SoldierCommand.FollowHero));
            simulation.Step(0.5f, default);
            Assert.That(Read(simulation, UnitKind.Soldier).Behavior, Is.EqualTo(UnitBehavior.Idle));
            simulation.Step(0.5f, default);
            var released = Read(simulation, UnitKind.Soldier);
            Assert.That(released.Command, Is.EqualTo(SoldierCommand.FollowHero));
            Assert.That(released.Behavior, Is.EqualTo(UnitBehavior.Regrouping));
            Assert.That((released.X, released.Y), Is.Not.EqualTo((soldier.X, soldier.Y)));
        }

        [Test]
        public void PoisonDeath_IsResolvedBeforeSoldierCanAttackInSameStep()
        {
            using var simulation = new EcsBattleSimulation();
            var definition = Definition();
            simulation.Reset(new BattleDefinition(definition.Hero, definition.Goblin, definition.Runner,
                1, 0, 40, 40, 0.02f, 7, heroSpawn: new MapPoint(-8, -8),
                enemySpawns: new[] { new MapPoint(0.54f, 0) }, soldiers: new[]
                {
                    new UnitDefinition(UnitKind.Soldier, 100, 10, 0, 2, 0.2f, 1)
                }));
            Assert.That(simulation.TrySpawnSoldier(UnitKind.Soldier, new MapPoint(0, 0), new MapPoint(0, 0), out ulong id), Is.True);
            simulation.TryApplyStatus(new ApplyUnitStatusCommand(id, UnitStatusKind.Poison, 1, 100, 0.125));
            simulation.Step(0.125f, default);
            Assert.That(Read(simulation, UnitKind.Soldier).Health, Is.Zero);
            Assert.That(Read(simulation, UnitKind.Goblin).Health, Is.EqualTo(100));
            Assert.That(Read(simulation, UnitKind.Soldier).LastAttackTime, Is.EqualTo(-100));
        }
    }
}
