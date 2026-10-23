using UnityEngine;

namespace Game.MapEngine.Unity.Views
{
    /// <summary>可重建的网格绘制数据，不含业务状态。</summary>
    public sealed class SurfaceMeshData
    {
        public Sprite Sprite;
        public Vector2[] Positions;
        public Vector2[] Uvs;
        public Color32[] Colors;
        public int[] Indices;
    }
}
