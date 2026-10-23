using UnityEngine;

namespace Game.MapEngine.Unity.Views
{
    /// <summary>编辑画布和运行视图共用的绘制条目；Id 仅关联来源，不拥有来源的可变状态。</summary>
    public sealed class SceneRenderEntry
    {
        public string Id;
        public Sprite Sprite;
        public Vector2 Center;
        public float Width;
        public float Height;
        public float Angle;
        public float GroundY;
        public float GroundX;
        public string SortKey;
        public int DepthLayer;
        public RaisedSurfaceShape Wall;
    }
}
