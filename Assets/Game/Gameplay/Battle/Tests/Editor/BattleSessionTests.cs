using System;
using System.Collections.Generic;
using Game.Battle.Application.Sessions;
using Game.Battle.Contracts.Commands;
using Game.Battle.Contracts.Ports;
using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.Definitions;
using Game.Battle.Domain.ValueObjects;
using Game.Battle.Simulation.Bridges;
using Game.Infrastructure.Configuration;
using NUnit.Framework;

namespace Game.Battle.Tests
{
    /// <summary>通过应用会话和正式输入/配置端口验证只读查询、补步与重启失败语义。</summary>
    public sealed class BattleSessionTests
    {
        private static BattleDefinition LoadDefinition()
        {
            return JsonBattleDefinitionSource.Parse(System.IO.File.ReadAllText("Assets/Content/Configuration/Resources/GameConfig/battle.json"));
        }

        [Test]
        public void Units_AfterRestart_RejectsExternalMutationAndReflectsNewSnapshots()
        {
            using var simulation = new EcsBattleSimulation();
            var session = new BattleSession(new DefinitionSource(LoadDefinition()), simulation, new FixedInput());
            session.Restart();
            var units = session.Units;
            var writable = units as IList<UnitSnapshot>;
            Assert.That(writable, Is.Not.Null);
            Assert.That(writable.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => writable.Clear());
            Assert.Throws<NotSupportedException>(() => writable[0] = default);
            double before = session.ElapsedTime;
            session.Advance(session.Definition.FixedStep * 2);
            Assert.That(session.ElapsedTime, Is.GreaterThan(before));
            Assert.That(units, Is.SameAs(session.Units));
            Assert.That(units.Count, Is.EqualTo(session.Definition.MonsterCount + 1));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Advance_NonfiniteFrameTime_RejectsWithoutPoisoningLaterFrames(float frameTime)
        {
            using var simulation = new EcsBattleSimulation();
            var session = new BattleSession(new DefinitionSource(LoadDefinition()), simulation, new FixedInput());
            session.Restart();
            Assert.Throws<ArgumentOutOfRangeException>(() => session.Advance(frameTime));
            Assert.That(session.ElapsedTime, Is.Zero);
            session.Advance(session.Definition.FixedStep * 2);
            Assert.That(session.ElapsedTime, Is.GreaterThan(0));
        }

        [Test]
        public void Advance_LongFrame_BoundsCatchupAndDiscardsExcessTime()
        {
            using var simulation = new EcsBattleSimulation();
            var session = new BattleSession(new DefinitionSource(LoadDefinition()), simulation, new FixedInput());
            session.Restart();
            session.Advance(10);
            double time = session.ElapsedTime;
            Assert.That(time, Is.GreaterThan(0));
            Assert.That(time, Is.LessThanOrEqualTo(session.Definition.FixedStep * 8 + 0.000001));
            session.Advance(0);
            Assert.That(session.ElapsedTime, Is.EqualTo(time));
        }

        [Test]
        public void Restart_InvalidReload_KeepsCurrentDefinitionGenerationAndSimulation()
        {
            using var simulation = new EcsBattleSimulation();
            var source = new DefinitionSource(LoadDefinition());
            var session = new BattleSession(source, simulation, new FixedInput());
            session.Restart();
            session.Advance(session.Definition.FixedStep * 2);
            var definition = session.Definition;
            int generation = session.Generation;
            double time = session.ElapsedTime;
            source.FailNextLoad = true;
            Assert.Throws<FormatException>(() => session.Restart());
            Assert.That(session.Definition, Is.SameAs(definition));
            Assert.That(session.Generation, Is.EqualTo(generation));
            Assert.That(session.ElapsedTime, Is.EqualTo(time));
        }

        private sealed class DefinitionSource : IBattleDefinitionSource
        {
            private readonly BattleDefinition _definition;
            public bool FailNextLoad { get; set; }

            public DefinitionSource(BattleDefinition definition)
            {
                _definition = definition;
            }

            public BattleDefinition Load()
            {
                if (FailNextLoad)
                {
                    throw new FormatException("Invalid configuration fixture.");
                }

                return _definition;
            }
        }

        private sealed class FixedInput : IHeroInput
        {
            public MoveIntent Read() => new MoveIntent(0, 0);
        }
    }
}
