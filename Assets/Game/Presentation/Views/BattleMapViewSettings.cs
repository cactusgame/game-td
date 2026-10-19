using UnityEngine;

namespace Game.Presentation.Views
{
    /// <summary>地图外观和相机视野配置，不定义寻路连通性或单位移动范围。</summary>
    [System.Serializable]
    public sealed class BattleMapViewSettings
    {
        public float VisibleWorldHeight;
        public float EdgeMargin;
        public float TopEdgeMargin;
        public float BorderThickness;
        public float GroundPatternSize;
        public Color GroundLight;
        public Color GroundDark;
        public Color BorderColor;
    }
}
