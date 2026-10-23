using System;
using Game.Encounter.Domain.Definitions;

namespace Game.Encounter.Domain.Aggregates.DefenseEncounter
{
    /// <summary>拥有波次提交进度和终局状态；不保存怪物或建筑的第二份生命。</summary>
    public sealed class DefenseEncounter
    {
        private readonly DefenseEncounterDefinition _definition;
        public DefenseEncounter(DefenseEncounterDefinition definition = null)
        {
            _definition = definition ?? new DefenseEncounterDefinition();
        }
        public int SpawnedWaves { get; private set; }
        public string Outcome { get; private set; } = "Running";
        public bool IsRunning => Outcome == "Running";
        public int DueCount(double simulationTime)
        {
            if (!IsRunning || SpawnedWaves >= 2)
                return 0;
            if (SpawnedWaves == 0)
                return _definition.FirstWaveCount;
            return simulationTime + 0.000001 >= _definition.WaveInterval ? _definition.SecondWaveCount : 0;
        }

        public void CommitWave()
        {
            if (!IsRunning || SpawnedWaves >= 2)
                throw new InvalidOperationException("No pending wave.");
            SpawnedWaves++;
        }

        public void Evaluate(bool castleDestroyed, int aliveEnemies)
        {
            if (!IsRunning)
                return;
            // 同步致死时防守失败优先；首波清空不能提前胜利。
            if (castleDestroyed)
                Outcome = "Defeat";
            else if (SpawnedWaves == 2 && aliveEnemies == 0)
                Outcome = "Victory";
        }
    }
}
