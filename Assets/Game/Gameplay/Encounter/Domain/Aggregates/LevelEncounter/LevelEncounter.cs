using System;
using System.Collections.Generic;
using Game.Encounter.Domain.Definitions;

namespace Game.Encounter.Domain.Aggregates.LevelEncounter
{
    /// <summary>波次进度唯一拥有者。生成成功后才提交名额；死亡与清波奖励只结算一次。</summary>
    public sealed class LevelEncounter
    {
        private readonly LevelLayout _layout;
        private readonly int _startWaveIndex;
        private readonly WaveProgress[] _waves;
        private readonly Dictionary<ulong, EnemyReceipt> _enemies = new Dictionary<ulong, EnemyReceipt>();
        private readonly HashSet<ulong> _spawnedUnitIds = new HashSet<ulong>();
        public int StartedWaves { get; private set; }
        public int CompletedWaves { get; private set; }
        public int WaveCount => _waves.Length;
        public string Outcome { get; private set; } = "Running";
        public bool IsRunning => Outcome == "Running";
        public bool IsConstructionTime { get; private set; } = true;
        private double _constructionCompletedAt;

        /// <summary>确认建设后才启用波次调度。首波等待从确认时刻计算，手动首波直接启动。</summary>
        public bool TryCompleteConstruction(double time)
        {
            if (!IsRunning || !IsConstructionTime)
                return false;

            IsConstructionTime = false;
            _constructionCompletedAt = time;
            if (_layout.waves[_startWaveIndex].startMode == WaveStartMode.Manual)
                StartWave(_startWaveIndex, time);
            StartDueWaves(time);
            return true;
        }

        private sealed class WaveProgress
        {
            public bool Started, Completed;
            public double StartedAt, CompletedAt;
            public int Alive;
            public int[] Spawned;
        }
        private readonly struct EnemyReceipt
        {
            public readonly int WaveIndex, Gold;
            public EnemyReceipt(int waveIndex, int gold) { WaveIndex = waveIndex; Gold = gold; }
        }

        public LevelEncounter(LevelDefinition definition, int startWaveIndex = 0)
        {
            _layout = definition.CreateWorkingCopy();
            _startWaveIndex = startWaveIndex;
            if (startWaveIndex < 0 || startWaveIndex >= _layout.waves.Count)
                throw new ArgumentOutOfRangeException(nameof(startWaveIndex));
            _waves = new WaveProgress[_layout.waves.Count];
            for (int i = 0; i < _waves.Length; i++)
            {
                bool skipped = i < startWaveIndex;
                _waves[i] = new WaveProgress { Started = skipped, Completed = skipped, Spawned = new int[_layout.waves[i].groups.Count] };
                if (skipped) { StartedWaves++; CompletedWaves++; }
            }
        }

        public bool CanStartNextWave(double time)
        {
            if (!IsRunning || IsConstructionTime || StartedWaves >= WaveCount) return false;
            int index = StartedWaves;
            return index == _startWaveIndex || _waves[index - 1].Completed || _layout.waves[index].allowEarlyStart;
        }

        public bool TryStartNextWave(double time, out int earlyGold)
        {
            earlyGold = 0;
            if (!CanStartNextWave(time)) return false;
            int index = StartedWaves;
            var wave = _layout.waves[index];
            // 首波与从指定波开始的试玩不发提前开波奖励；奖励只针对尚未到自动启动点的后续波。
            if (index > _startWaveIndex && wave.allowEarlyStart && !IsAutomaticallyDue(index, time))
                earlyGold = wave.earlyStartGold;
            StartWave(index, time);
            return true;
        }

        public void StartDueWaves(double time)
        {
            while (IsRunning && !IsConstructionTime && StartedWaves < WaveCount && IsAutomaticallyDue(StartedWaves, time))
                StartWave(StartedWaves, time);
        }

        private bool IsAutomaticallyDue(int index, double time)
        {
            var wave = _layout.waves[index];
            if (wave.startMode == WaveStartMode.Manual) return false;
            if (index == _startWaveIndex) return time + 0.000001 >= _constructionCompletedAt + wave.waitSeconds;
            var previous = _waves[index - 1];
            if (wave.startMode == WaveStartMode.AfterPreviousStarted)
                return previous.Started && time + 0.000001 >= previous.StartedAt + wave.waitSeconds;
            return previous.Completed && time + 0.000001 >= previous.CompletedAt + wave.waitSeconds;
        }

        private void StartWave(int index, double time)
        {
            _waves[index].Started = true;
            _waves[index].StartedAt = time;
            StartedWaves++;
        }

        public bool TryGetDueSpawn(double time, out EnemySpawnOrder order)
        {
            for (int w = 0; IsRunning && w < StartedWaves; w++)
            {
                var progress = _waves[w];
                if (progress.Completed) continue;
                for (int g = 0; g < progress.Spawned.Length; g++)
                {
                    var group = _layout.waves[w].groups[g];
                    int sequence = progress.Spawned[g];
                    if (sequence < group.count && time + 0.000001 >= progress.StartedAt + group.delaySeconds + sequence * group.intervalSeconds)
                    {
                        order = new EnemySpawnOrder(w, g, sequence, group.entryId, group.enemyId);
                        return true;
                    }
                }
            }
            order = default;
            return false;
        }

        public void CommitSpawn(EnemySpawnOrder order, ulong unitId, int killGold)
        {
            if (order.WaveIndex < 0 || order.WaveIndex >= _waves.Length || order.GroupIndex < 0 ||
                order.GroupIndex >= _waves[order.WaveIndex].Spawned.Length)
                throw new InvalidOperationException("出怪组不存在");
            var progress = _waves[order.WaveIndex];
            var group = _layout.waves[order.WaveIndex].groups[order.GroupIndex];
            if (!IsRunning || !progress.Started || progress.Completed || unitId == 0 || killGold < 0 ||
                progress.Spawned[order.GroupIndex] != order.Sequence || order.Sequence >= group.count ||
                order.EntryId != group.entryId || order.EnemyId != group.enemyId || _spawnedUnitIds.Contains(unitId))
                throw new InvalidOperationException("无效或重复的出怪提交");
            _enemies.Add(unitId, new EnemyReceipt(order.WaveIndex, killGold));
            _spawnedUnitIds.Add(unitId);
            progress.Spawned[order.GroupIndex]++;
            progress.Alive++;
        }

        public int ResolveDefeatedEnemy(ulong unitId)
        {
            if (!IsRunning || !_enemies.TryGetValue(unitId, out var receipt)) return 0;
            _enemies.Remove(unitId);
            _waves[receipt.WaveIndex].Alive--;
            return receipt.Gold;
        }

        public int CompleteClearedWaves(double time)
        {
            int gold = 0;
            for (int w = 0; IsRunning && w < StartedWaves; w++)
            {
                var progress = _waves[w];
                if (progress.Completed || progress.Alive != 0) continue;
                bool allSpawned = true;
                for (int g = 0; g < progress.Spawned.Length; g++)
                    allSpawned &= progress.Spawned[g] == _layout.waves[w].groups[g].count;
                if (!allSpawned) continue;
                gold = checked(gold + _layout.waves[w].completionGold);
                progress.Completed = true;
                progress.CompletedAt = time;
                CompletedWaves++;
            }
            return gold;
        }

        public void Evaluate(bool castleDestroyed)
        {
            if (!IsRunning) return;
            if (castleDestroyed) Outcome = "Defeat";
            else if (CompletedWaves == WaveCount) Outcome = "Victory";
        }
    }
}
