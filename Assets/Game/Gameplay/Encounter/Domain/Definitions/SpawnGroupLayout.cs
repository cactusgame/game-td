using System;

namespace Game.Encounter.Domain.Definitions
{
    [Serializable]
    public sealed class SpawnGroupLayout
    {
        public string id = Guid.NewGuid().ToString("N");
        public string entryId;
        public string enemyId;
        public int count = 5;
        public double delaySeconds;
        public double intervalSeconds = 1;
        public string note;
        public double EndSeconds => delaySeconds + Math.Max(0, count - 1) * intervalSeconds;
        public SpawnGroupLayout Copy() => (SpawnGroupLayout)MemberwiseClone();
    }
}
