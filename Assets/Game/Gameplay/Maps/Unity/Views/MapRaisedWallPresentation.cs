using System.Collections.Generic;
using Game.Maps.Domain;
using Game.Maps.Unity.Assets;
using UnityEngine;

namespace Game.Maps.Unity.Views
{
    /// <summary>
    /// 共用地面接口生成墙身、走道和女墙。墙与节点使用同一条连接弦，
    /// 表面按地面深度裁切；渲染片段不产生新的墙段身份。
    /// </summary>
    public static class MapRaisedWallPresentation
    {
        public sealed class Shape
        {
            public MapSurfaceGeometry.Patch Mesh;
            public Vector2[] GroundOutline;
            public readonly List<Vector2[]> Facades = new List<Vector2[]>();
        }

        public static IEnumerable<MapSceneSprites.Entry> Build(MapLayout layout, WallEdge edge, MapVisualCatalog visuals)
        {
            Sprite masonry = visuals.wallMasonry != null ? visuals.wallMasonry : visuals.WallBodySprite;
            Sprite paving = visuals.wallWalkway != null ? visuals.wallWalkway : masonry;
            if (masonry == null || !MapWallPresentation.TryGetSpan(layout, edge, out var start, out var end))
                yield break;
            string startId = edge.from;
            string endId = edge.to;
            if (start.x > end.x || start.x == end.x && start.y > end.y)
            {
                Vector2 swap = start;
                start = end;
                end = swap;
                string swapId = startId;
                startId = endId;
                endId = swapId;
            }
            var geometry = new MapWallGeometry(layout, visuals);
            Vector2 direction = (end - start).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            float length = Vector2.Distance(start, end);
            float first = geometry.PortDistance(geometry.RadiusAt(layout, layout.wallNodes.Find(node => node.id == startId)));
            float last = length - geometry.PortDistance(geometry.RadiusAt(layout, layout.wallNodes.Find(node => node.id == endId)));
            if (last <= first + 0.0001f)
                yield break;
            if (edge.hasGate && WallGateRules.Validate(layout, edge, out _))
            {
                var center = WallGateRules.Center(layout, edge);
                float distance = Vector2.Dot(new Vector2(center.X, center.Y) - start, direction);
                float halfFrame = edge.gate.width * (0.5f - visuals.gateSideInset) - geometry.Width * 0.125f;
                foreach (var entry in BuildSection(edge.id, start, direction, normal, first, distance - halfFrame, geometry, masonry, paving))
                    yield return entry;
                foreach (var entry in BuildSection(edge.id, start, direction, normal, distance + halfFrame, last, geometry, masonry, paving))
                    yield return entry;
                yield break;
            }
            foreach (var entry in BuildSection(edge.id, start, direction, normal, first, last, geometry, masonry, paving))
                yield return entry;
        }

        private static IEnumerable<MapSceneSprites.Entry> BuildSection(string id, Vector2 start, Vector2 direction,
            Vector2 normal, float first, float last, MapWallGeometry geometry, Sprite masonry, Sprite paving)
        {
            if (last <= first)
                yield break;
            Vector2 a = start + direction * first;
            Vector2 b = start + direction * last;
            var footprint = Rectangle(a, b, normal * geometry.Width * 0.5f);
            var builder = new MapWallSurfaceBuilder(id, "wall/" + id + "/" + first.ToString("R", System.Globalization.CultureInfo.InvariantCulture), geometry, footprint);

            // 端面属于敌台连接口，只绘制墙的长边；不能在走道相接处留下竖直封口。
            Side(builder, footprint[0], footprint[1], 0, geometry.DeckHeight, start, direction, geometry, masonry, 0);
            Side(builder, footprint[2], footprint[3], 0, geometry.DeckHeight, start, direction, geometry, masonry, 0);
            builder.Deck(footprint, geometry.DeckHeight, paving, new Color(0.94f, 0.92f, 0.86f), 1);
            AddRail(builder, start, direction, normal, first, last, 1, geometry, masonry, paving);
            AddRail(builder, start, direction, normal, first, last, -1, geometry, masonry, paving);
            foreach (var entry in builder.Finish())
                yield return entry;
        }

        private static void AddRail(MapWallSurfaceBuilder builder, Vector2 start, Vector2 direction, Vector2 normal,
            float first, float last, float sign, MapWallGeometry geometry, Sprite masonry, Sprite paving)
        {
            Vector2 offset = normal * sign * (geometry.Width - geometry.RailWidth) * 0.5f;
            var basePolygon = Rectangle(start + direction * first + offset, start + direction * last + offset, normal * geometry.RailWidth * 0.5f);
            Prism(builder, basePolygon, geometry.DeckHeight, geometry.DeckHeight + geometry.RailHeight,
                start, direction, geometry, masonry, paving, 2, false);
            // 垛口按固定世界间距截取，不随整条墙的分片数量缩放。
            int firstTooth = Mathf.FloorToInt(first / geometry.MerlonSpacing);
            int lastTooth = Mathf.FloorToInt(last / geometry.MerlonSpacing);
            for (int i = firstTooth; i <= lastTooth; i++)
            {
                float a = Mathf.Max(first, i * geometry.MerlonSpacing);
                float b = Mathf.Min(last, i * geometry.MerlonSpacing + geometry.MerlonLength);
                if (b <= a + 0.0001f)
                    continue;
                var polygon = Rectangle(start + direction * a + offset, start + direction * b + offset, normal * geometry.RailWidth * 0.5f);
                Prism(builder, polygon, geometry.DeckHeight + geometry.RailHeight,
                    geometry.DeckHeight + geometry.RailHeight + geometry.MerlonHeight,
                    start, direction, geometry, masonry, paving, 3, true);
            }
        }

        internal static void Prism(MapWallSurfaceBuilder builder, Vector2[] polygon, float bottom, float top,
            Vector2 textureOrigin, Vector2 direction, MapWallGeometry geometry, Sprite masonry, Sprite paving, int layer, bool ends)
        {
            for (int i = 0; i < polygon.Length; i++)
            {
                if (!ends && (i == 1 || i == 3))
                    continue;
                Side(builder, polygon[i], polygon[(i + 1) % polygon.Length], bottom, top,
                    textureOrigin, direction, geometry, masonry, layer);
            }
            builder.Deck(polygon, top, paving, new Color(1, 0.98f, 0.92f), layer);
        }

        private static void Side(MapWallSurfaceBuilder builder, Vector2 a, Vector2 b, float bottom, float top,
            Vector2 origin, Vector2 direction, MapWallGeometry geometry, Sprite masonry, int layer)
        {
            float u0 = Vector2.Dot(a - origin, direction) / geometry.TextureLength;
            float u1 = Vector2.Dot(b - origin, direction) / geometry.TextureLength;
            if (Mathf.Abs(u1 - u0) < 0.00001f)
                u1 = u0 + Vector2.Distance(a, b) / geometry.TextureLength;
            Vector2 outward = new Vector2(b.y - a.y, a.x - b.x).normalized;
            float light = 0.78f + Vector2.Dot(outward, new Vector2(-0.4f, -0.6f).normalized) * 0.12f;
            builder.Facade(a, b, bottom, top, u0, u1, masonry, new Color(light, light * 0.97f, light * 0.92f), layer);
        }

        internal static Vector2[] Rectangle(Vector2 start, Vector2 end, Vector2 halfWidth)
        {
            return new[] { start - halfWidth, end - halfWidth, end + halfWidth, start + halfWidth };
        }

        public static Vector2 Project(Vector2 ground, float height) => ground + Vector2.up * height;
    }
}
