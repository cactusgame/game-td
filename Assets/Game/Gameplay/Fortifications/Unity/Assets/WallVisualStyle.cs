using System;
using UnityEngine;

namespace Game.Fortifications.Unity.Assets
{
    /// <summary>固定制作元数据，仅决定墙面的表达，不决定实际墙宽、墙高或通行。</summary>
    [Serializable]
    public sealed class WallVisualStyle
    {
        public float railWidth = 0.075f;
        public float railHeight = 0.08f;
        public float merlonHeight = 0.16f;
        public float merlonSpacing = 0.42f;
        public float merlonLength = 0.22f;
        public float textureLength = 1.2f;
        public float textureHeight = 0.9f;
        [NonSerialized] public Sprite Masonry;
        [NonSerialized] public Sprite Paving;
        [NonSerialized] public float GateSideInset;

        public WallVisualStyle WithMaterials(Sprite masonry, Sprite paving, float gateSideInset)
        {
            var copy = (WallVisualStyle)MemberwiseClone();
            copy.Masonry = masonry;
            copy.Paving = paving;
            copy.GateSideInset = gateSideInset;
            return copy;
        }
    }
}
