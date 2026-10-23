using System;
using Game.Battle.Contracts.Commands;
using Game.Battle.Domain.Definitions;
using Game.Battle.Domain.ValueObjects;
using Game.Battle.Simulation.Bridges;
using Game.Infrastructure.Configuration;
using Game.MapEngine.Domain;
using NUnit.Framework;

namespace Game.Battle.Tests
{
    public sealed class DomainBoundaryTests
    {
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Coordinates_NonfiniteValue_RejectsAtTheBoundary(float value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MoveIntent(value, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MoveIntent(0, value));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MapPoint(value, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MapPoint(0, value));
        }

        [Test]
        public void MapPoint_EqualCoordinates_HasValueEquality()
        {
            var first = new MapPoint(2, 3);
            var second = new MapPoint(2, 3);
            Assert.That(first == second, Is.True);
            Assert.That(first.Equals((object)second), Is.True);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first != new MapPoint(2, 4), Is.True);
        }

        [Test]
        public void UnitKind_UnknownValue_RejectsInsteadOfSelectingRunner()
        {
            var unknown = (UnitKind)255;
            Assert.Throws<ArgumentOutOfRangeException>(() => new UnitDefinition(unknown, 10, 1, 0, 1, 0.4f, 1));
            var definition = JsonBattleDefinitionSource.Parse(System.IO.File.ReadAllText("Assets/Content/Configuration/Resources/GameConfig/battle.json"));
            Assert.Throws<ArgumentOutOfRangeException>(() => definition.GetUnit(unknown));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(0)]
        [TestCase(-1)]
        public void Step_InvalidTime_LeavesSimulationTimeUnchanged(float step)
        {
            using var simulation = new EcsBattleSimulation();
            var definition = JsonBattleDefinitionSource.Parse(System.IO.File.ReadAllText("Assets/Content/Configuration/Resources/GameConfig/battle.json"));
            simulation.Reset(definition);
            Assert.Throws<ArgumentOutOfRangeException>(() => simulation.Step(step, default));
            Assert.That(simulation.ElapsedTime, Is.Zero);
            simulation.Step(definition.FixedStep, default);
            Assert.That(simulation.ElapsedTime, Is.GreaterThan(0));
        }
    }
}
