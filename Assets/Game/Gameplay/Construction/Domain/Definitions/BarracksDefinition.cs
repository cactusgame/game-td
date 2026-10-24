using System;

namespace Game.Construction.Domain.Definitions
{
    /// <summary>兵营的训练规则。单位ID引用战斗内容，当前血量不属于此定义。</summary>
    public sealed class BarracksDefinition
    {
        public string BuildingId { get; }
        public string UnitId { get; }
        public int Capacity { get; }
        public float RetrainSeconds { get; }
        public float RallyDistance { get; }
        public BarracksDefinition(string buildingId, string unitId, int capacity, float retrainSeconds, float rallyDistance)
        {
            if (string.IsNullOrWhiteSpace(buildingId) || string.IsNullOrWhiteSpace(unitId) || capacity <= 0 || capacity > 100 ||
                float.IsNaN(retrainSeconds) || float.IsInfinity(retrainSeconds) || retrainSeconds <= 0 ||
                float.IsNaN(rallyDistance) || float.IsInfinity(rallyDistance) || rallyDistance <= 0)
                throw new ArgumentException("Invalid barracks training rule.");
            BuildingId = buildingId;
            UnitId = unitId;
            Capacity = capacity;
            RetrainSeconds = retrainSeconds;
            RallyDistance = rallyDistance;
        }
    }
}
