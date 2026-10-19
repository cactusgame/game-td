using System;
using System.Collections.Generic;
using System.IO;
using Game.Battle.Contracts.Commands;
using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.Definitions;
using Game.Battle.Domain.ValueObjects;
using Game.Battle.Domain.Rules;
using Game.Battle.Simulation.Bridges;
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
        public void DefenseCannotTurnDamageIntoHealing(int attack, int defense, int expected) => Assert.That(ContactRules.Damage(attack, defense), Is.EqualTo(expected));
        [Test]
        public void LethalDamageClampsToZero() => Assert.That(ContactRules.ApplyDamage(12, 100), Is.Zero);
        [Test]
        public void SweptCircleDetectsFastCrossing() => Assert.That(ContactRules.SweptCircles(-3, 0, 3, 0, 0.5f), Is.True);
        [Test]
        public void SweptCircleDoesNotHitParallelMiss() => Assert.That(ContactRules.SweptCircles(-3, 2, 3, 2, 0.5f), Is.False);
        [Test]
        public void JsonLoadsAllThreeKindsAndConfiguredMonsterSpawns()
        {
            var definition = JsonBattleDefinitionSource.Parse(File.ReadAllText("Assets/Content/Combat/Prototype/battle.json"));
            Assert.That(definition.GoblinCount, Is.GreaterThan(0));
            Assert.That(definition.RunnerCount, Is.GreaterThan(0));
            Assert.That(definition.Hero.Kind, Is.EqualTo(UnitKind.Hero));
            Assert.That(definition.Goblin.Kind, Is.EqualTo(UnitKind.Goblin));
            Assert.That(definition.Runner.Kind, Is.EqualTo(UnitKind.Runner));
            Assert.That(definition.Runner.Speed, Is.GreaterThan(definition.Goblin.Speed));
            Assert.That(definition.Runner.Radius, Is.LessThan(definition.Goblin.Radius));
        }

        [Test]
        public void InvalidHealthAndNonfiniteSpeedAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new UnitDefinition(UnitKind.Hero, 0, 1, 0, 1, 0.4f, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new UnitDefinition(UnitKind.Hero, 1, 1, 0, float.NaN, 0.4f, 1));
        }

        [Test]
        public void UnsupportedJsonSchemaIsRejected() => Assert.Throws<FormatException>(() => JsonBattleDefinitionSource.Parse("{\"schemaVersion\":2}"));
        [Test]
        public void ArenaWithoutSafeSpawnIsRejectedInsteadOfHanging()
        {
            var hero = new UnitDefinition(UnitKind.Hero, 100, 15, 3, 4.5f, 2, 1.5f);
            var goblin = new UnitDefinition(UnitKind.Goblin, 25, 9, 2, 1.25f, 2, 1.3f);
            var runner = new UnitDefinition(UnitKind.Runner, 12, 7, 0, 3, 0.24f, 0.95f);
            Assert.Throws<ArgumentException>(() => new BattleDefinition(hero, goblin, runner, 1, 0, 6.1f, 6.1f, 1f / 60, 1));
        }

        [Test]
        public void DiagonalInputHasSameSpeedAndRestartIsReproducible()
        {
            var hero = new UnitDefinition(UnitKind.Hero, 100, 15, 3, 4.5f, 0.4f, 1.5f);
            var goblin = new UnitDefinition(UnitKind.Goblin, 25, 9, 2, 1.25f, 0.4f, 1.3f);
            var runner = new UnitDefinition(UnitKind.Runner, 12, 7, 0, 3, 0.24f, 0.95f);
            var definition = new BattleDefinition(hero, goblin, runner, 0, 0, 26, 14, 1f / 60, 123);
            using var simulation = new EcsBattleSimulation();
            var units = new List<UnitSnapshot>();
            simulation.Reset(definition);
            for (int i = 0; i < 60; i++)
                simulation.Step(1f / 60, new MoveIntent(1, 1));
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

        [Test]
        public void RandomMovementRemainsInBoundsAndResetsItsRandomStream()
        {
            var configured = JsonBattleDefinitionSource.Parse(File.ReadAllText("Assets/Content/Combat/Prototype/battle.json"));
            // 用受控数量验证行为，不将性能调试时的实际出生数量硬编码进测试期望。
            var definition = new BattleDefinition(configured.Hero, configured.Goblin, configured.Runner, 6, 4, configured.Width, configured.Height, configured.FixedStep, configured.Seed, configured.Navigation);
            using var simulation = new EcsBattleSimulation();
            var first = new List<UnitSnapshot>();
            var second = new List<UnitSnapshot>();
            simulation.Reset(definition);
            for (int i = 0; i < 600; i++)
                simulation.Step(definition.FixedStep, new MoveIntent(0, 0));
            simulation.CopyUnits(first);
            simulation.Reset(definition);
            for (int i = 0; i < 600; i++)
                simulation.Step(definition.FixedStep, new MoveIntent(0, 0));
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
}
