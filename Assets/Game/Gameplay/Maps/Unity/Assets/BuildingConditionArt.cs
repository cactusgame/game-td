using UnityEngine;

namespace Game.Maps.Unity.Assets
{
    /// <summary>建筑的共享完整、残血和废墟素材。每列三态共用画幅和脚底锚点。</summary>
    public sealed class BuildingConditionArt : ScriptableObject
    {
        public MapVisualCatalog.FacilityVisual[] facilities;
        public Sprite damagedMasonry;
        public Sprite destroyedMasonry;
    }
}
