using System;
using System.Collections.Generic;

namespace Game.Encounter.Domain.Definitions
{
    public enum WaveStartMode { Manual, AfterPreviousStarted, AfterPreviousCompleted }

    [Serializable]
    public sealed class WaveLayout
    {
        public string id = Guid.NewGuid().ToString("N");
        public string name = "新波次";
        public WaveStartMode startMode = WaveStartMode.AfterPreviousCompleted;
        public double waitSeconds = 5;
        public int completionGold;
        public bool allowEarlyStart;
        public int earlyStartGold;
        public List<EnemyGoldOverride> goldOverrides = new List<EnemyGoldOverride>();
        public List<SpawnGroupLayout> groups = new List<SpawnGroupLayout>();

        public WaveLayout Copy()
        {
            var copy = (WaveLayout)MemberwiseClone();
            copy.goldOverrides = goldOverrides?.ConvertAll(item => item?.Copy());
            copy.groups = groups?.ConvertAll(item => item?.Copy());
            return copy;
        }
    }
}
