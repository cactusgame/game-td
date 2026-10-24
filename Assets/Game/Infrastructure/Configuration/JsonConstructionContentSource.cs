using System;
using System.Linq;
using Game.Construction.Domain.Definitions;
using UnityEngine;

namespace Game.Infrastructure.Configuration
{
    public static class JsonConstructionContentSource
    {
        public static ConstructionContentDefinition Parse(string json)
        {
            var data = JsonUtility.FromJson<Document>(json);
            if (data == null || data.schemaVersion != 1 || data.buildings == null || data.upgrades == null)
                throw new FormatException("Invalid construction content JSON.");
            return new ConstructionContentDefinition(data.buildings.Select(b => new BuildingDevelopmentDefinition(
                b.id, b.buildCost, b.buildSeconds, b.canDemolish, b.refundRatio)), data.upgrades.Select(u => new UpgradeBranchDefinition(
                u.id, u.buildingId, u.prerequisiteId, u.exclusiveGroup, u.cost, u.seconds, u.maxHealthBonus, u.attackBonus, u.rangeBonus, u.capacityBonus)),
                (data.barracks ?? Array.Empty<Barracks>()).Select(b => new BarracksDefinition(b.id, b.unitId, b.capacity, b.retrainSeconds, b.rallyDistance)));
        }
        [Serializable] private sealed class Document
        {
            public int schemaVersion;
            public Building[] buildings;
            public Upgrade[] upgrades;
            public Barracks[] barracks;
        }
        [Serializable] private sealed class Building
        {
            public string id;
            public int buildCost;
            public float buildSeconds;
            public bool canDemolish;
            public float refundRatio;
        }
        [Serializable] private sealed class Upgrade
        {
            public string id, buildingId, prerequisiteId, exclusiveGroup;
            public int cost, maxHealthBonus, attackBonus, capacityBonus;
            public float seconds, rangeBonus;
        }
        [Serializable] private sealed class Barracks
        {
            public string id, unitId;
            public int capacity;
            public float retrainSeconds, rallyDistance;
        }
    }
}
