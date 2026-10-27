using System;
using System.Collections.Generic;
using System.Linq;
using Game.Battle.Contracts.Commands;
using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.Definitions;
using Game.Battle.Domain.ValueObjects;
using Game.Battle.Simulation.Bridges;
using Game.MapEngine.Domain;
using Game.Tools.MapPlaytest.Unity.Views;
using NUnit.Framework;

namespace Game.Tools.MapPlaytest.Tests
{
    public sealed class AiDebugTests
    {
        private static EcsBattleSimulation Create()
        {
            var simulation = new EcsBattleSimulation();
            simulation.Reset(new BattleDefinition(new UnitDefinition(UnitKind.Hero, 100, 0, 0, 2, 0.2f, 1),
                new UnitDefinition(UnitKind.Goblin, 100, 0, 0, 2, 0.2f, 1),
                new UnitDefinition(UnitKind.Runner, 100, 0, 0, 2, 0.2f, 1),
                1, 0, 40, 40, 0.02f, 7, heroSpawn: new MapPoint(-8, -8), enemySpawns: new[] { new MapPoint(3, 0) },
                soldiers: new[] { new UnitDefinition(UnitKind.Soldier, 100, 0, 0, 2, 0.2f, 1) }));
            Assert.That(simulation.TrySpawnSoldier(UnitKind.Soldier, new MapPoint(0, 0), new MapPoint(1, 0), out _), Is.True);
            return simulation;
        }

        [Test]
        public void DebugRead_ReportsActualTargetPathAndRadius_WithoutAdvancingOrMutatingSimulation()
        {
            using var simulation = Create();
            simulation.Step(0.125f, default);
            var data = new List<UnitAiDebugSnapshot>(); simulation.CopyAiDebug(data);
            var soldier = data.Single(unit => unit.Kind == UnitKind.Soldier);
            Assert.That(soldier.Target, Does.Contain("Goblin"));
            Assert.That(soldier.HasTarget, Is.True);
            Assert.That(soldier.Behavior, Is.EqualTo(UnitBehavior.Chasing));
            Assert.That(soldier.Path.Count, Is.GreaterThan(0));
            Assert.That(soldier.AlertRadius, Is.EqualTo(5));
            double time = simulation.ElapsedTime;
            Assert.Throws<NotSupportedException>(() => ((IList<MapPoint>)soldier.Path)[0] = new MapPoint(9, 9));
            simulation.CopyAiDebug(data);
            Assert.That(simulation.ElapsedTime, Is.EqualTo(time));
            Assert.That(data.Single(unit => unit.UnitId == soldier.UnitId).Path[0], Is.EqualTo(soldier.Path[0]));
            var oldPath = soldier.Path.ToArray();
            simulation.Step(1, default);
            Assert.That(soldier.Path, Is.EqualTo(oldPath), "旧诊断帧不能跟着 ECS Buffer 改变");
        }

        [Test]
        public void StopReason_DistinguishesStunRootAndNoInput_WithIndependentExpiry()
        {
            using var simulation = Create();
            simulation.TryApplyStatus(new ApplyUnitStatusCommand(1, UnitStatusKind.Stun, 0.5));
            simulation.TryApplyStatus(new ApplyUnitStatusCommand(1, UnitStatusKind.Root, 1));
            var data = new List<UnitAiDebugSnapshot>();
            simulation.Step(0.25f, new MoveIntent(1, 0)); simulation.CopyAiDebug(data);
            Assert.That(data.Single(unit => unit.UnitId == 1).StopReason, Is.EqualTo(MovementStopReason.Stunned));
            simulation.Step(0.25f, new MoveIntent(1, 0)); simulation.CopyAiDebug(data);
            var rooted = data.Single(unit => unit.UnitId == 1);
            Assert.That(rooted.StopReason, Is.EqualTo(MovementStopReason.Rooted));
            Assert.That(AiDebugView.Describe(rooted), Does.Contain("停止 定身"));
            simulation.Step(0.5f, default); simulation.CopyAiDebug(data);
            Assert.That(data.Single(unit => unit.UnitId == 1).StopReason, Is.EqualTo(MovementStopReason.NoMoveInput));
        }

        [Test]
        public void DebugRead_UsesExternalWallIdentityAndCooldown_OnlyForCurrentStep()
        {
            using var simulation = Create();
            var data = new List<UnitAiDebugSnapshot>(); simulation.CopyAiDebug(data);
            ulong enemy = data.Single(unit => unit.Kind == UnitKind.Goblin).UnitId;
            Assert.That(simulation.TryAttackExternalTarget(enemy, new MapPoint(2, 0), 0, damage => true, "wall:test-wall"), Is.True);
            simulation.CopyAiDebug(data);
            var attacking = data.Single(unit => unit.UnitId == enemy);
            Assert.That(attacking.Target, Is.EqualTo("wall:test-wall"));
            Assert.That(attacking.TargetPosition, Is.EqualTo(new MapPoint(2, 0)));
            Assert.That(attacking.AttackCooldownSeconds, Is.EqualTo(1));
            simulation.Step(0.125f, default); simulation.CopyAiDebug(data);
            Assert.That(data.Single(unit => unit.UnitId == enemy).Target, Is.Not.EqualTo("wall:test-wall"));
        }
    }
}
