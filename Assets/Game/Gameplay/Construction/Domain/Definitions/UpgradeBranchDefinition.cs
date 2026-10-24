using System;

namespace Game.Construction.Domain.Definitions
{
    public sealed class UpgradeBranchDefinition
    {
        public string Id { get; }
        public string BuildingId { get; }
        public string PrerequisiteId { get; }
        public string ExclusiveGroup { get; }
        public int Cost { get; }
        public float Seconds { get; }
        public int MaxHealthBonus { get; }
        public int AttackBonus { get; }
        public float RangeBonus { get; }
        public int CapacityBonus { get; }
        public UpgradeBranchDefinition(string id, string buildingId, string prerequisiteId, string exclusiveGroup,
            int cost, float seconds, int maxHealthBonus, int attackBonus, float rangeBonus, int capacityBonus = 0)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(buildingId) || cost < 0 ||
                float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0 || maxHealthBonus < 0 || attackBonus < 0 ||
                float.IsNaN(rangeBonus) || float.IsInfinity(rangeBonus) || rangeBonus < 0 || capacityBonus < 0 || capacityBonus > 100)
                throw new ArgumentException("Invalid upgrade definition.");
            Id = id; BuildingId = buildingId; PrerequisiteId = prerequisiteId; ExclusiveGroup = exclusiveGroup;
            Cost = cost; Seconds = seconds; MaxHealthBonus = maxHealthBonus; AttackBonus = attackBonus; RangeBonus = rangeBonus;
            CapacityBonus = capacityBonus;
        }
    }
}
