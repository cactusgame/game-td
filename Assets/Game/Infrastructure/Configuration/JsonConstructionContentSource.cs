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
                b.id, b.buildCost, b.buildSeconds, b.canDemolish, b.refundRatio, ParseCategory(b.category), ParsePlacement(b.placementKind))), data.upgrades.Select(u => new UpgradeBranchDefinition(
                u.id, u.buildingId, u.prerequisiteId, u.exclusiveGroup, u.cost, u.seconds, u.maxHealthBonus, u.attackBonus, u.rangeBonus, u.capacityBonus, u.name)),
                (data.barracks ?? Array.Empty<Barracks>()).Select(b => new BarracksDefinition(b.id, b.unitId, b.capacity, b.retrainSeconds, b.rallyDistance)));
        }
        private static BuildingCategory ParseCategory(string value)
        {
            if (!Enum.TryParse(value, out BuildingCategory category) || !Enum.IsDefined(typeof(BuildingCategory), category) || category.ToString() != value)
                throw new FormatException("建筑必须声明合法分类：" + value);
            return category;
        }
        private static ConstructionPlacementKind ParsePlacement(string value)
        {
            if (!Enum.TryParse(value, out ConstructionPlacementKind kind) || !Enum.IsDefined(typeof(ConstructionPlacementKind), kind) || kind.ToString() != value)
                throw new FormatException("建筑放置方式无效：" + value);
            return kind;
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
            public string category, placementKind;
        }
        [Serializable] private sealed class Upgrade
        {
            public string id, buildingId, prerequisiteId, exclusiveGroup, name;
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
