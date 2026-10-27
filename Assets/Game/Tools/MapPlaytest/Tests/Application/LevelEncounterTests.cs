using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Battle.Contracts.Commands;
using Game.Battle.Contracts.Ports;
using Game.Battle.Domain.ValueObjects;
using Game.Battle.Simulation.Bridges;
using Game.Encounter.Domain.Aggregates.LevelEncounter;
using Game.Encounter.Domain.Definitions;
using Game.Infrastructure.Configuration;
using Game.Infrastructure.Maps.Serialization;
using Game.Maps.Domain;
using Game.MapEngine.Domain;
using Game.Tools.MapPlaytest.Application.Adapters;
using Game.Tools.MapPlaytest.Application.Sessions;
using NUnit.Framework;

namespace Game.Tools.MapPlaytest.Tests
{
    public sealed class LevelEncounterTests
    {
        [Test]
        public void ConstructionTime_BlocksAllScheduling_AndConfirmationStartsTheWaitClockOnce()
        {
            var wave = Wave(Group(count: 1, delay: 1));
            wave.waitSeconds = 3;
            var encounter = new LevelEncounter(Definition(wave));
            encounter.StartDueWaves(100);
            Assert.That(encounter.IsConstructionTime, Is.True);
            Assert.That(encounter.CanStartNextWave(100), Is.False);
            Assert.That(encounter.TryStartNextWave(100, out _), Is.False);
            Assert.That(encounter.TryGetDueSpawn(100, out _), Is.False);
            Assert.That(encounter.TryCompleteConstruction(100), Is.True);
            Assert.That(encounter.TryCompleteConstruction(101), Is.False);
            encounter.StartDueWaves(102.99);
            Assert.That(encounter.StartedWaves, Is.Zero);
            encounter.StartDueWaves(103);
            Assert.That(encounter.StartedWaves, Is.EqualTo(1));
            Assert.That(encounter.TryGetDueSpawn(103.99, out _), Is.False);
            Assert.That(encounter.TryGetDueSpawn(104, out _), Is.True);
        }

        [Test]
        public void Runtime_ConstructionTimeAllowsSimulation_ButNoEnemiesOrRewards_AndRestartRestoresIt()
        {
            using var session = Session(new LevelLayout { waves = new List<WaveLayout> { Wave(Group(count: 1)) } });
            session.Restart();
            for (int i = 0; i < 300; i++)
                session.Advance(0.02f);
            Assert.That(session.SimulationTime, Is.GreaterThan(5));
            Assert.That(session.IsConstructionTime, Is.True);
            Assert.That(session.SpawnedWaves, Is.Zero);
            Assert.That(session.Units.Any(unit => UnitKinds.IsEnemy(unit.Kind)), Is.False);
            int gold = session.Gold;
            Assert.That(session.TryCompleteConstruction(), Is.True);
            Assert.That(session.TryCompleteConstruction(), Is.False);
            Assert.That(session.Gold, Is.EqualTo(gold));
            Assert.That(session.Units.Count(unit => UnitKinds.IsEnemy(unit.Kind)), Is.EqualTo(1));
            session.Restart();
            Assert.That(session.IsConstructionTime, Is.True);
            Assert.That(session.Units.Any(unit => UnitKinds.IsEnemy(unit.Kind)), Is.False);
        }

        [Test]
        public void Preview_UsesEarliestGroupsAtEachEntry_MergesTiesAndAdvancesWithWave()
        {
            var first = Wave(Group(count: 3, delay: 2), Group(count: 4, delay: 2),
                Group("enemy.fallen", 5, 2), Group(count: 99, delay: 3));
            var second = Wave(Group("enemy.fallen", 6));
            second.allowEarlyStart = true;
            using var session = Session(new LevelLayout { waves = new List<WaveLayout> { first, second } });
            session.Restart();
            var previews = session.GetWavePreviews();
            Assert.That(previews.Count, Is.EqualTo(2));
            Assert.That(previews.Single(item => item.EnemyId == "enemy.skeleton").Count, Is.EqualTo(7));
            Assert.That(previews.Single(item => item.EnemyId == "enemy.fallen").Count, Is.EqualTo(5));
            Assert.That(previews[0].WaveIndex, Is.Zero);
            Assert.That(previews[0].EntryId, Is.EqualTo("entry"));
            session.TryCompleteConstruction();
            Assert.That(session.GetWavePreviews().Single().WaveIndex, Is.EqualTo(1));
            Assert.That(session.GetWavePreviews().Single().Count, Is.EqualTo(6));
            session.TryStartNextWave();
            Assert.That(session.GetWavePreviews(), Is.Empty);
        }

        [Test]
        public void RewardAccount_RepeatedReceiptDoesNotCreditTwice_AndFailureCanRetry()
        {
            var account = new Game.Economy.Domain.Aggregates.ResourceAccount.ResourceAccount(int.MaxValue - 1);
            Assert.Throws<ArgumentOutOfRangeException>(() => account.CreditOnce("reward", 2));
            Assert.That(account.CreditOnce("reward", 1), Is.True);
            Assert.That(account.CreditOnce("reward", 1), Is.False);
            Assert.That(account.Gold, Is.EqualTo(int.MaxValue));
        }
        private static SpawnGroupLayout Group(string enemy = "enemy.skeleton", int count = 2, double delay = 0, double interval = 1) =>
            new SpawnGroupLayout { entryId = "entry", enemyId = enemy, count = count, delaySeconds = delay, intervalSeconds = interval };

        private static WaveLayout Wave(params SpawnGroupLayout[] groups) => new WaveLayout
        { waitSeconds = 0, groups = new List<SpawnGroupLayout>(groups) };

        private static LevelDefinition Definition(params WaveLayout[] waves) => new LevelDefinition(
            new LevelLayout { waves = new List<WaveLayout>(waves) }, new[] { "entry" }, new[] { "enemy.skeleton", "enemy.fallen" });

        [Test]
        public void Gold_ExplicitZeroOverridesLevelAndDefault_AndDefinitionIsIsolated()
        {
            var level = new LevelLayout { waves = new List<WaveLayout> { Wave(Group()), Wave(Group()) } };
            level.goldOverrides.Add(new EnemyGoldOverride { enemyId = "enemy.skeleton", gold = 4 });
            level.waves[1].goldOverrides.Add(new EnemyGoldOverride { enemyId = "enemy.skeleton", gold = 0 });
            var definition = new LevelDefinition(level, new[] { "entry" }, new[] { "enemy.skeleton", "enemy.fallen" });
            level.goldOverrides[0].gold = 999;
            definition.CreateWorkingCopy().waves[1].goldOverrides[0].gold = 999;
            Assert.That(definition.ResolveKillGold(0, "enemy.skeleton", 5, out var source), Is.EqualTo(4));
            Assert.That(source, Is.EqualTo("关卡覆盖"));
            Assert.That(definition.ResolveKillGold(1, "enemy.skeleton", 5, out source), Is.Zero);
            Assert.That(source, Is.EqualTo("波次覆盖"));
            Assert.That(definition.ResolveKillGold(1, "enemy.fallen", 7, out source), Is.EqualTo(7));
            Assert.That(source, Is.EqualTo("怪物默认"));
        }

        [Test]
        public void SpawnGroups_IndependentDelaysAndIntervals_DoNotCompleteDuringAGap()
        {
            var encounter = new LevelEncounter(Definition(Wave(Group(count: 2, delay: 1, interval: 2), Group("enemy.fallen", 1, 2))));
            encounter.TryCompleteConstruction(0);
            Assert.That(encounter.TryGetDueSpawn(0.99, out _), Is.False);
            Assert.That(encounter.TryGetDueSpawn(1, out var first), Is.True);
            encounter.CommitSpawn(first, 10, 4);
            Assert.That(encounter.ResolveDefeatedEnemy(10), Is.EqualTo(4));
            Assert.That(encounter.ResolveDefeatedEnemy(10), Is.Zero);
            Assert.That(encounter.CompleteClearedWaves(1), Is.Zero);
            Assert.That(encounter.CompletedWaves, Is.Zero);
            Assert.That(encounter.TryGetDueSpawn(2, out var fast), Is.True);
            Assert.That(fast.EnemyId, Is.EqualTo("enemy.fallen"));
            encounter.CommitSpawn(fast, 11, 5);
            Assert.That(encounter.TryGetDueSpawn(2.99, out _), Is.False);
            Assert.That(encounter.TryGetDueSpawn(3, out var last), Is.True);
            encounter.CommitSpawn(last, 12, 4);
            Assert.That(encounter.TryGetDueSpawn(3, out _), Is.False);
            encounter.ResolveDefeatedEnemy(11); encounter.ResolveDefeatedEnemy(12);
            encounter.CompleteClearedWaves(3);
            encounter.Evaluate(false);
            Assert.That(encounter.Outcome, Is.EqualTo("Victory"));
        }

        [Test]
        public void EarlyStart_OverlappingWavesKeepBirthRewards_AndPayOnlyOnce()
        {
            var first = Wave(Group(count: 1)); first.completionGold = 30;
            var second = Wave(Group(count: 1)); second.startMode = WaveStartMode.Manual;
            second.allowEarlyStart = true; second.earlyStartGold = 10; second.completionGold = 20;
            var encounter = new LevelEncounter(Definition(first, second));
            encounter.TryCompleteConstruction(0);
            encounter.TryGetDueSpawn(0, out var order); encounter.CommitSpawn(order, 1, 4);
            Assert.That(encounter.TryStartNextWave(0, out int bonus), Is.True);
            Assert.That(bonus, Is.EqualTo(10));
            Assert.That(encounter.TryStartNextWave(0, out _), Is.False);
            encounter.TryGetDueSpawn(0, out order); encounter.CommitSpawn(order, 2, 0);
            Assert.That(encounter.ResolveDefeatedEnemy(1), Is.EqualTo(4));
            Assert.That(encounter.CompleteClearedWaves(1), Is.EqualTo(30));
            Assert.That(encounter.ResolveDefeatedEnemy(2), Is.Zero);
            Assert.That(encounter.CompleteClearedWaves(2), Is.EqualTo(20));
            Assert.That(encounter.CompleteClearedWaves(3), Is.Zero);
            encounter.Evaluate(true);
            Assert.That(encounter.Outcome, Is.EqualTo("Defeat"));
        }

        [Test]
        public void AutomaticWave_AfterCompletion_WaitsForClearThenDelay()
        {
            var second = Wave(Group(count: 1)); second.waitSeconds = 2;
            var encounter = new LevelEncounter(Definition(Wave(Group(count: 1)), second));
            encounter.TryCompleteConstruction(0);
            encounter.TryGetDueSpawn(0, out var order); encounter.CommitSpawn(order, 1, 0);
            encounter.StartDueWaves(100);
            Assert.That(encounter.StartedWaves, Is.EqualTo(1));
            Assert.That(encounter.CanStartNextWave(100), Is.False);
            encounter.ResolveDefeatedEnemy(1); encounter.CompleteClearedWaves(100);
            encounter.StartDueWaves(101.99);
            Assert.That(encounter.StartedWaves, Is.EqualTo(1));
            encounter.StartDueWaves(102);
            Assert.That(encounter.StartedWaves, Is.EqualTo(2));
        }

        [Test]
        public void AutomaticWave_AfterPreviousStart_CanOverlapWithoutManualEarlyStart()
        {
            var second = Wave(Group()); second.startMode = WaveStartMode.AfterPreviousStarted; second.waitSeconds = 2;
            var encounter = new LevelEncounter(Definition(Wave(Group()), second));
            encounter.TryCompleteConstruction(0); encounter.StartDueWaves(1.99);
            Assert.That(encounter.StartedWaves, Is.EqualTo(1));
            encounter.StartDueWaves(2);
            Assert.That(encounter.StartedWaves, Is.EqualTo(2));
            Assert.That(encounter.CompletedWaves, Is.Zero);
        }

        [Test]
        public void Validation_RejectsMissingReferencesDuplicateOverridesAndNonfiniteTime()
        {
            var group = Group(); group.delaySeconds = double.NaN;
            Assert.Throws<ArgumentException>(() => Definition(Wave(group)));
            group.delaySeconds = 0; group.entryId = "deleted-entry";
            Assert.Throws<ArgumentException>(() => Definition(Wave(group)));
            group.entryId = "entry"; group.enemyId = "missing";
            Assert.Throws<ArgumentException>(() => Definition(Wave(group)));
            var wave = Wave(Group());
            wave.goldOverrides.Add(new EnemyGoldOverride { enemyId = "enemy.skeleton", gold = 0 });
            wave.goldOverrides.Add(new EnemyGoldOverride { enemyId = "enemy.skeleton", gold = 1 });
            Assert.Throws<ArgumentException>(() => Definition(wave));
        }

        private static MapLayout Map()
        {
            var map = new MapLayout();
            map.markers.Add(new MapMarker { id = "hero", name = "英雄", kind = MarkerKind.HeroSpawn, x = 8.5f, y = 5.5f });
            map.markers.Add(new MapMarker { id = "entry", name = "西口", kind = MarkerKind.EnemyEntry, x = -9.5f, y = 0.5f });
            map.facilities.Add(new InitialFacility { id = "castle", definitionId = "hall", x = 12, y = 7 });
            map.facilities.Add(new InitialFacility { id = "tower", definitionId = "tower", x = 9, y = 8 });
            return map;
        }

        private static MapPlaySession Session(LevelLayout level, int start = 0, Input input = null, EcsBattleSimulation simulation = null)
        {
            var rules = JsonBattleDefinitionSource.Parse(File.ReadAllText("Assets/Content/Configuration/Resources/GameConfig/battle.json"));
            var catalog = new MapContentCatalog(new[]
            {
                new FacilityDefinition { id = "hall", width = 2, height = 2, maxHealth = 500 },
                new FacilityDefinition { id = "tower", attack = 10000, attackRange = 30, attackInterval = 0.02f }
            });
            var map = Map();
            var definition = LevelDefinitionFactory.Create(level, map, rules);
            return new MapPlaySession(new MapDefinition(map, catalog), catalog, rules, input ?? new Input(), () => simulation ?? new EcsBattleSimulation(), levelDefinition: definition, startWaveIndex: start);
        }

        [Test]
        public void Runtime_KillsCreditEffectiveGoldAndClearRewards_RestartResetsReceipts()
        {
            var first = Wave(Group(count: 2, interval: 0)); first.completionGold = 10;
            var second = Wave(Group(count: 1, delay: 0.1)); second.completionGold = 20;
            second.goldOverrides.Add(new EnemyGoldOverride { enemyId = "enemy.skeleton", gold = 0 });
            var level = new LevelLayout { initialGold = 100, waves = new List<WaveLayout> { first, second } };
            level.goldOverrides.Add(new EnemyGoldOverride { enemyId = "enemy.skeleton", gold = 4 });
            using var session = Session(level);
            session.Restart();
            Assert.That(session.TryCompleteConstruction(), Is.True);
            Assert.That(session.Units.Count(unit => UnitKinds.IsEnemy(unit.Kind)), Is.EqualTo(2));
            for (int i = 0; i < 100 && session.Outcome == "Running"; i++) session.Advance(0.02f);
            Assert.That(session.Outcome, Is.EqualTo("Victory"));
            Assert.That(session.Gold, Is.EqualTo(138));
            session.Advance(1);
            Assert.That(session.Gold, Is.EqualTo(138));
            session.Restart();
            Assert.That(session.Gold, Is.EqualTo(100));
            Assert.That(session.SpawnedWaves, Is.Zero);
            Assert.That(session.IsConstructionTime, Is.True);
            Assert.That(session.CompletedWaves, Is.Zero);
        }

        [Test]
        public void Runtime_StartAtSelectedWave_SkipsEarlierEnemiesAndIncome()
        {
            var first = Wave(Group(count: 99)); first.completionGold = 999;
            var second = Wave(Group(count: 1)); second.startMode = WaveStartMode.Manual; second.completionGold = 20;
            using var session = Session(new LevelLayout { initialGold = 100, waves = new List<WaveLayout> { first, second } }, 1);
            session.Restart();
            Assert.That(session.Units.Count(unit => UnitKinds.IsEnemy(unit.Kind)), Is.Zero);
            Assert.That(session.TryStartNextWave(), Is.True);
            for (int i = 0; i < 100 && session.Outcome == "Running"; i++) session.Advance(0.02f);
            Assert.That(session.Outcome, Is.EqualTo("Victory"));
            Assert.That(session.Gold, Is.EqualTo(125));
        }

        [Test]
        public void Runtime_SimulationClockHonorsDelayedGroups()
        {
            var first = Wave(Group(count: 2, delay: 1, interval: 0.5));
            using var session = Session(new LevelLayout { waves = new List<WaveLayout> { first } });
            session.Restart();
            Assert.That(session.TryCompleteConstruction(), Is.True);
            for (int i = 0; i < 49; i++) session.Advance(0.02f);
            Assert.That(session.Units.Count(unit => UnitKinds.IsEnemy(unit.Kind)), Is.Zero);
            session.Advance(0.02f);
            Assert.That(session.Units.Count(unit => UnitKinds.IsEnemy(unit.Kind)), Is.EqualTo(1));
            for (int i = 0; i < 25; i++) session.Advance(0.02f);
            Assert.That(session.Units.Count(unit => UnitKinds.IsEnemy(unit.Kind)), Is.EqualTo(2));
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ReleaseWithR_TeleportsOnlyBeforeFirstWave(bool confirmed, bool waveStarted)
        {
            var wave = Wave(Group(count: 1, delay: 100));
            wave.waitSeconds = waveStarted ? 0 : 3;
            var input = new Input();
            var simulation = new EcsBattleSimulation();
            using var session = Session(new LevelLayout { waves = new List<WaveLayout> { wave } }, input: input, simulation: simulation);
            session.Restart();
            if (confirmed) session.TryCompleteConstruction();
            Assert.That(simulation.TrySpawnSoldier(UnitKind.Soldier, new MapPoint(2.5f, 5.5f), new MapPoint(2.5f, 5.5f), out ulong id), Is.True);
            simulation.Execute(PlayerOperation.CallAll);
            input.Next = PlayerOperation.NearbyTap;
            session.Advance(0.02f);
            var unit = session.Units.Single(item => item.Id == id);
            Assert.That(unit.IsSelected, Is.False);
            Assert.That(unit.X, waveStarted ? Is.LessThan(3) : Is.GreaterThan(6));
        }

        private sealed class Input : IHeroInput, IPlayerOperationInput
        {
            public PlayerOperation? Next;
            public MoveIntent Read() => default;
            public void ResetOperations() => Next = null;
            public void ReadOperations(List<PlayerOperation> destination)
            {
                if (Next.HasValue) destination.Add(Next.Value);
                Next = null;
            }
        }
    }
}
