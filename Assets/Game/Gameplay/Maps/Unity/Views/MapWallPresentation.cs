using Game.Maps.Domain;
using UnityEngine;

namespace Game.Maps.Unity.Views
{
    /// <summary>
    /// 编辑器和游戏共用节点中心线。显示不再为了填满导航格而延长自由端。
    /// </summary>
    public static class MapWallPresentation
    {
        public static bool TryGetSpan(MapLayout layout, WallEdge edge, out Vector2 start, out Vector2 end)
        {
            start = default;
            end = default;
            var a = layout.wallNodes.Find(node => node.id == edge.from);
            var b = layout.wallNodes.Find(node => node.id == edge.to);
            if (a == null || b == null)
                return false;
            start = new Vector2(a.x, a.y);
            end = new Vector2(b.x, b.y);
            Vector2 direction = (end - start).normalized;
            if (direction.sqrMagnitude < 0.5f)
                return false;
            return true;
        }

    }
}
