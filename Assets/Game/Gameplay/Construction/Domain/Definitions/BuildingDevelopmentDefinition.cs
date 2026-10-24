using System;

namespace Game.Construction.Domain.Definitions
{
    /// <summary>建设与发展的只读规则；不保存余额、当前等级或当前生命。</summary>
    public sealed class BuildingDevelopmentDefinition
    {
        public string Id { get; }
        public int BuildCost { get; }
        public float BuildSeconds { get; }
        public bool CanDemolish { get; }
        public float RefundRatio { get; }
        public BuildingCategory Category { get; }
        public ConstructionPlacementKind PlacementKind { get; }
        public bool CanPlayerBuild => Category == BuildingCategory.PlayerBuildable;
        public BuildingDevelopmentDefinition(string id, int buildCost, float buildSeconds, bool canDemolish, float refundRatio,
            BuildingCategory category = BuildingCategory.PlayerBuildable, ConstructionPlacementKind placementKind = ConstructionPlacementKind.Building)
        {
            if (string.IsNullOrWhiteSpace(id) || buildCost < 0 || float.IsNaN(buildSeconds) || float.IsInfinity(buildSeconds) || buildSeconds < 0 ||
                float.IsNaN(refundRatio) || refundRatio < 0 || refundRatio > 1 ||
                !Enum.IsDefined(typeof(BuildingCategory), category) || !Enum.IsDefined(typeof(ConstructionPlacementKind), placementKind))
                throw new ArgumentException("Invalid building development rule.");
            Id = id;
            BuildCost = buildCost;
            BuildSeconds = buildSeconds;
            CanDemolish = canDemolish;
            RefundRatio = refundRatio;
            Category = category;
            PlacementKind = placementKind;
        }
    }
}
