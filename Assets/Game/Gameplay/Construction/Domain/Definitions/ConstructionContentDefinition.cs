using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Game.Construction.Domain.Definitions
{
    public sealed class ConstructionContentDefinition
    {
        public IReadOnlyDictionary<string, BuildingDevelopmentDefinition> Buildings { get; }
        public IReadOnlyDictionary<string, UpgradeBranchDefinition> Upgrades { get; }
        public IReadOnlyDictionary<string, BarracksDefinition> Barracks { get; }
        public ConstructionContentDefinition(IEnumerable<BuildingDevelopmentDefinition> buildings, IEnumerable<UpgradeBranchDefinition> upgrades, IEnumerable<BarracksDefinition> barracks = null)
        {
            var buildingIndex = new Dictionary<string, BuildingDevelopmentDefinition>();
            foreach (var building in buildings) buildingIndex.Add(building.Id, building);
            var upgradeIndex = new Dictionary<string, UpgradeBranchDefinition>();
            foreach (var upgrade in upgrades) upgradeIndex.Add(upgrade.Id, upgrade);
            foreach (var upgrade in upgradeIndex.Values)
            {
                if (!buildingIndex.ContainsKey(upgrade.BuildingId)) throw new ArgumentException("Unknown upgrade building: " + upgrade.Id);
                var seen = new HashSet<string> { upgrade.Id };
                var current = upgrade;
                while (!string.IsNullOrEmpty(current.PrerequisiteId))
                {
                    if (!upgradeIndex.TryGetValue(current.PrerequisiteId, out current) ||
                        !seen.Add(current.Id) || current.BuildingId != upgrade.BuildingId)
                        throw new ArgumentException("Invalid upgrade prerequisite: " + upgrade.Id);
                }
            }
            Buildings = new ReadOnlyDictionary<string, BuildingDevelopmentDefinition>(buildingIndex);
            Upgrades = new ReadOnlyDictionary<string, UpgradeBranchDefinition>(upgradeIndex);
            var barracksIndex = new Dictionary<string, BarracksDefinition>();
            foreach (var definition in barracks ?? Array.Empty<BarracksDefinition>())
            {
                if (!buildingIndex.ContainsKey(definition.BuildingId)) throw new ArgumentException("Unknown barracks building.");
                barracksIndex.Add(definition.BuildingId, definition);
            }
            Barracks = new ReadOnlyDictionary<string, BarracksDefinition>(barracksIndex);
        }
    }
}
