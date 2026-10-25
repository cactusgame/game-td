using System.Collections.Generic;
using Game.Battle.Contracts.Ports;
using Game.Battle.Domain.Definitions;
using Game.Battle.Domain.ValueObjects;
using Game.Encounter.Domain.Aggregates.LevelEncounter;
using Game.Encounter.Domain.Definitions;
using Game.MapEngine.Domain;

namespace Game.Tools.MapPlaytest.Application.Sessions
{
    public sealed partial class MapPlaySession
    {
        private readonly LevelDefinition _levelDefinition;
        private readonly int _startWaveIndex;
        private readonly Dictionary<string, MapPoint> _levelEntries = new Dictionary<string, MapPoint>();
        private LevelEncounter _levelEncounter;

        public int TotalWaves => _levelDefinition?.WaveCount ?? 2;
        public int CompletedWaves => _levelEncounter?.CompletedWaves ?? 0;
        public bool CanStartNextWave => _levelEncounter?.CanStartNextWave(SimulationTime) ?? false;
        private bool IsEncounterRunning => _levelEncounter?.IsRunning ?? _encounter.IsRunning;

        /// <summary>输入提交开波意图，关卡推进状态，经济账户提交奖励，最后生成本时点应出生的敌人。</summary>
        public bool TryStartNextWave()
        {
            RequireActive();
            if (_levelEncounter == null || !_levelEncounter.TryStartNextWave(SimulationTime, out int reward))
                return false;
            CreditLevelReward("early/" + _levelEncounter.StartedWaves, reward);
            SpawnDueWave();
            return true;
        }

        private void SpawnLevelEnemies(LevelEncounter encounter, BattleDefinition definition,
            ILevelEnemySimulation simulation, double time)
        {
            encounter.StartDueWaves(time);
            while (encounter.TryGetDueSpawn(time, out var order))
            {
                var unit = order.EnemyId == definition.Goblin.Id ? definition.Goblin : definition.Runner;
                int gold = _levelDefinition.ResolveKillGold(order.WaveIndex, order.EnemyId, unit.KillGold, out _);
                ulong id = simulation.SpawnEnemy(unit.Kind, _levelEntries[order.EntryId]);
                encounter.CommitSpawn(order, id, gold);
            }
        }

        /// <summary>只消费战斗已提交的死亡快照。先结算收入和清波，再判胜负，主堡被毁优先失败。</summary>
        private void SettleLevelRewards(bool castleDestroyed)
        {
            foreach (var unit in Units)
            {
                if (unit.Health > 0 || !UnitKinds.IsEnemy(unit.Kind))
                    continue;
                int reward = _levelEncounter.ResolveDefeatedEnemy(unit.Id);
                if (reward > 0)
                    CreditLevelReward("enemy/" + unit.Id, reward);
            }
            int completionReward = _levelEncounter.CompleteClearedWaves(SimulationTime);
            if (completionReward > 0)
                CreditLevelReward("clear/" + _levelEncounter.CompletedWaves, completionReward);
            _levelEncounter.Evaluate(castleDestroyed);
        }

        private void CreditLevelReward(string rewardId, int amount)
        {
            if (amount > 0)
                _account.CreditOnce(rewardId, amount);
        }
    }
}
