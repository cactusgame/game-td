using System.Collections.Generic;
using Game.Maps.Domain;
using UnityEngine;

namespace Game.Maps.Unity.Views
{
    /// <summary>
    /// 编辑器和游戏共用墙体绘制范围。导航已把端点格当成阻挡，显示应覆盖该格，
    /// 自由端延伸到端点格外缘；连接节点仍使用原位置，不改写墙网 ID 或源地图。
    /// </summary>
    public static class MapWallPresentation
    {
        public readonly struct Piece
        {
            public readonly Vector2 Center;
            public readonly float Width;
            public readonly float Height;
            public readonly float Angle;
            public readonly float GroundY;
            public readonly Vector2 Ground;
            public Piece(Vector2 center, float width, float height, float angle, Vector2 ground)
            {
                Center = center;
                Width = width;
                Height = height;
                Angle = angle;
                GroundY = ground.y;
                Ground = ground;
            }
        }

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
            start = ExtendFreeEnd(layout, a, -direction);
            end = ExtendFreeEnd(layout, b, direction);
            return true;
        }

        public static IEnumerable<Piece> BuildPieces(MapLayout layout, WallEdge edge)
        {
            if (!TryGetSpan(layout, edge, out var start, out var end))
                yield break;
            Vector2 direction = (end - start).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            // 反向绘制同一条墙时，墙身仍显示在地面线的上侧。
            if (normal.y < 0 || Mathf.Abs(normal.y) < 0.0001f && normal.x < 0)
                normal = -normal;
            float length = Vector2.Distance(start, end);
            float height = Mathf.Min(layout.cellWidth, layout.cellHeight) * 0.65f;
            int count = Mathf.Max(1, Mathf.CeilToInt(length / layout.cellWidth));
            float overlap = Mathf.Min(layout.cellWidth, layout.cellHeight) * 0.015f;
            float angle = Mathf.Atan2(direction.y, direction.x);
            // Sprite 的上方始终与选定的法线一致，避免反向画墙时图片倒置。
            if (Vector2.Dot(new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)), normal) < 0)
                angle += Mathf.PI;
            for (int i = 0; i < count; i++)
            {
                Vector2 ground = Vector2.Lerp(start, end, (i + 0.5f) / count);
                yield return new Piece(ground + normal * height * 0.45f, length / count + overlap, height, angle, ground);
            }
        }

        private static Vector2 ExtendFreeEnd(MapLayout layout, WallNode node, Vector2 direction)
        {
            var position = new Vector2(node.x, node.y);
            int connections = 0;
            foreach (var edge in layout.wallEdges)
            {
                if (edge.from == node.id || edge.to == node.id)
                    connections++;
            }
            if (connections != 1 || !layout.Geometry.TryWorldToCell(node.x, node.y, out var cell))
                return position;
            float left = layout.originX + cell.X * layout.cellWidth;
            float bottom = layout.originY + cell.Y * layout.cellHeight;
            float distanceX = direction.x > 0.0001f ? (left + layout.cellWidth - node.x) / direction.x :
                direction.x < -0.0001f ? (left - node.x) / direction.x : float.PositiveInfinity;
            float distanceY = direction.y > 0.0001f ? (bottom + layout.cellHeight - node.y) / direction.y :
                direction.y < -0.0001f ? (bottom - node.y) / direction.y : float.PositiveInfinity;
            return position + direction * Mathf.Max(0, Mathf.Min(distanceX, distanceY));
        }
    }
}
