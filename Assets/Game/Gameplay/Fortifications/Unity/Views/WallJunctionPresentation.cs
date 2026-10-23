using Game.Fortifications.Domain.Definitions;
using Game.MapEngine.Unity.Views;
using System.Collections.Generic;
using Game.Fortifications.Domain.ValueObjects;
using Game.Fortifications.Domain.Rules;
using Game.Fortifications.Unity.Assets;
using UnityEngine;

namespace Game.Fortifications.Unity.Views
{
    /// <summary>
    /// 端部、转角与分叉共用的几何连接底座。底座中心就是节点，连接口按邻接方向切割，
    /// 墙身和连接底座不再混用两种透视、锚点与高度来源。
    /// </summary>
    public static class WallJunctionPresentation
    {
        public static IEnumerable<SceneRenderEntry> Build(WallNetworkSnapshot network, WallNodeSnapshot node, WallDefinition definition, WallVisualStyle visuals)
        {
            Sprite masonry = visuals.Masonry;
            Sprite paving = visuals.Paving != null ? visuals.Paving : masonry;
            if (masonry == null)
                yield break;
            var geometry = new WallRenderGeometry(definition, visuals);
            var directions = geometry.DirectionsAt(network, node);
            if (directions.Count == 0)
                yield break;
            Vector2 center = new Vector2(node.Position.X, node.Position.Y);
            float radius = geometry.RadiusAt(network, node);
            float portDistance = geometry.PortDistance(radius);
            var outline = geometry.NodeOutline(center, radius, directions);
            if (outline.Length < 3)
                yield break;
            var builder = new WallSurfaceBuilder(node.Id, "node/" + node.Id, geometry, outline);

            Shell(builder, outline, 0, geometry.DeckHeight, center, radius, portDistance, directions, geometry, masonry, 0);
            builder.Deck(outline, geometry.DeckHeight, paving, new Color(0.94f, 0.92f, 0.86f), 1);

            // 女墙只沿连接底座外缘生成，墙体连接口不封死。连接端的内缘精确收至墙体走道宽度。
            var inside = new Vector2[outline.Length];
            for (int i = 0; i < outline.Length; i++)
                inside[i] = InnerPoint(outline[i], center, radius, portDistance, directions, geometry.RailWidth);
            for (int i = 0; i < outline.Length; i++)
            {
                int next = (i + 1) % outline.Length;
                if (IsPort(outline[i], outline[next], center, portDistance, directions))
                    continue;
                var rail = new[] { outline[i], outline[next], inside[next], inside[i] };
                Shell(builder, rail, geometry.DeckHeight, geometry.DeckHeight + geometry.RailHeight,
                    center, radius, portDistance, directions, geometry, masonry, 2);
                builder.Deck(rail, geometry.DeckHeight + geometry.RailHeight, paving, new Color(1, 0.98f, 0.92f), 2);
            }

            float circumference = Mathf.PI * 2 * radius;
            for (float distance = 0; distance < circumference; distance += geometry.MerlonSpacing)
            {
                float a = distance / radius;
                float b = Mathf.Min(distance + geometry.MerlonLength, circumference) / radius;
                var tooth = new List<Vector2>
                {
                    At(center, radius, a), At(center, radius, b),
                    At(center, radius - geometry.RailWidth, b), At(center, radius - geometry.RailWidth, a)
                };
                foreach (var direction in directions)
                    tooth = WallRenderGeometry.Clip(tooth, center, direction, portDistance);
                if (tooth.Count < 3)
                    continue;
                var polygon = tooth.ToArray();
                float bottom = geometry.DeckHeight + geometry.RailHeight;
                Shell(builder, polygon, bottom, bottom + geometry.MerlonHeight,
                    center, radius, portDistance, directions, geometry, masonry, 3);
                builder.Deck(polygon, bottom + geometry.MerlonHeight, paving, new Color(1, 0.98f, 0.92f), 3);
            }
            foreach (var entry in builder.Finish())
                yield return entry;
        }

        private static void Shell(WallSurfaceBuilder builder, Vector2[] polygon, float bottom, float top,
            Vector2 center, float radius, float portDistance, List<Vector2> directions, WallRenderGeometry geometry, Sprite sprite, int layer)
        {
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Length];
                if (IsPort(a, b, center, portDistance, directions))
                    continue;
                float angleA = Mathf.Repeat(Mathf.Atan2(a.y - center.y, a.x - center.x), Mathf.PI * 2);
                float angleB = angleA + Mathf.DeltaAngle(angleA * Mathf.Rad2Deg,
                    Mathf.Atan2(b.y - center.y, b.x - center.x) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                Vector2 outward = new Vector2(b.y - a.y, a.x - b.x).normalized;
                float light = 0.78f + Vector2.Dot(outward, new Vector2(-0.4f, -0.6f).normalized) * 0.12f;
                builder.Facade(a, b, bottom, top, angleA * radius / geometry.TextureLength,
                    angleB * radius / geometry.TextureLength, sprite, new Color(light, light * 0.97f, light * 0.92f), layer);
            }
        }

        private static Vector2 InnerPoint(Vector2 point, Vector2 center, float radius, float portDistance,
            List<Vector2> directions, float railWidth)
        {
            foreach (var direction in directions)
            {
                if (Mathf.Abs(Vector2.Dot(point - center, direction) - portDistance) < 0.0001f)
                    return Vector2.MoveTowards(point, center + direction * portDistance, railWidth);
            }
            return center + (point - center) * Mathf.Max(0, radius - railWidth) / radius;
        }

        private static bool IsPort(Vector2 a, Vector2 b, Vector2 center, float distance, List<Vector2> directions)
        {
            foreach (var direction in directions)
            {
                if (Mathf.Abs(Vector2.Dot(a - center, direction) - distance) < 0.0001f &&
                    Mathf.Abs(Vector2.Dot(b - center, direction) - distance) < 0.0001f)
                    return true;
            }
            return false;
        }

        private static Vector2 At(Vector2 center, float radius, float angle)
        {
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
    }
}
