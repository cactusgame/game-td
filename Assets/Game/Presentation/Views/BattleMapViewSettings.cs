using UnityEngine;

namespace Game.Presentation.Views
{
    /// <summary>地图外观和相机视野配置，不定义寻路连通性或单位移动范围。</summary>
    [CreateAssetMenu(menuName = "Game/Presentation/Battle Map View Settings")]
    public sealed class BattleMapViewSettings : ScriptableObject
    {
        [Tooltip("单屏可见的世界高度；16:9 下高度 18 对应宽度 32。")]
        [Min(1)] public float VisibleWorldHeight = 18;
        [Tooltip("相机靠近地图边缘时，允许显示边界外的留白。")]
        [Min(0)] public float EdgeMargin = 0.75f;
        [Tooltip("地图上沿留白，给站在边界上的角色身体与血条保留可见空间。")]
        [Min(0)] public float TopEdgeMargin = 2;
        [Min(0.01f)] public float BorderThickness = 0.3f;
        [Tooltip("仅用于地面视觉棋盘格，与寻路格长无关。")]
        [Min(0.1f)] public float GroundPatternSize = 1;
        public Color GroundLight = new Color(0.31f, 0.41f, 0.25f);
        public Color GroundDark = new Color(0.30f, 0.40f, 0.245f);
        public Color BorderColor = new Color(0.93f, 0.73f, 0.32f);
    }
}
