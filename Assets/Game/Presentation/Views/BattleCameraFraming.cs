using UnityEngine;

namespace Game.Presentation.Views
{
    /// <summary>固定世界视野的跟随相机几何；地图变大不会自动把画面拉远。</summary>
    public static class BattleCameraFraming
    {
        /// <summary>地图坐标不一定以零为中心；仅限制位置，不改变世界视野。</summary>
        public static Vector2 CenterOnMap(Vector2 target, Rect mapBounds, float visibleHeight, float aspect, float edgeMargin, float topEdgeMargin)
        {
            Vector2 offset = Center(target - mapBounds.center, mapBounds.width, mapBounds.height,
                visibleHeight, aspect, edgeMargin, topEdgeMargin);
            if (mapBounds.width <= visibleHeight * aspect)
                offset.x = 0;
            if (mapBounds.height <= visibleHeight)
                offset.y = 0;
            return mapBounds.center + offset;
        }

        public static Vector2 Center(Vector2 target, float mapWidth, float mapHeight, float visibleHeight, float aspect, float edgeMargin, float topEdgeMargin = -1)
        {
            float halfHeight = visibleHeight * 0.5f;
            float halfWidth = halfHeight * Mathf.Max(0.01f, aspect);
            float limitX = Mathf.Max(0, mapWidth * 0.5f + edgeMargin - halfWidth);
            float bottomLimit = -mapHeight * 0.5f - edgeMargin + halfHeight;
            float topLimit = mapHeight * 0.5f + Mathf.Max(edgeMargin, topEdgeMargin) - halfHeight;
            // 某个方向的地图小于视野时，该方向居中，不使用反向的 Clamp 上下界。
            return new Vector2(Mathf.Clamp(target.x, -limitX, limitX), bottomLimit > topLimit ? 0 : Mathf.Clamp(target.y, bottomLimit, topLimit));
        }
    }
}
