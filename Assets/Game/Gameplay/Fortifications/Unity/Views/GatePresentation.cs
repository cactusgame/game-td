using System;
using System.Collections.Generic;
using System.Linq;
using Game.Fortifications.Domain.Definitions;
using Game.Fortifications.Domain.Rules;
using Game.Fortifications.Domain.ValueObjects;
using Game.Fortifications.Unity.Assets;
using Game.MapEngine.Unity.Views;
using UnityEngine;

namespace Game.Fortifications.Unity.Views
{
    /// <summary>门柱、门楣、内壁与铁栅共用墙的局部坐标、材质和深度裁切；没有固定方向的门图。</summary>
    public static class GatePresentation
    {
        public static IEnumerable<SceneRenderEntry> Build(WallNetworkSnapshot network, WallEdgeSnapshot edge,
            WallGateSnapshot gate, WallDefinition definition, WallVisualStyle style)
        {
            if (style.Masonry == null || !WallGateRules.ValidateGate(network, edge, gate, definition, out _) ||
                !WallGateRules.TryGetSpan(network, edge, gate, out var span))
                yield break;
            var geometry = new WallRenderGeometry(definition, style);
            var origin = new Vector2(span.Origin.X, span.Origin.Y);
            var direction = new Vector2(span.Direction.X, span.Direction.Y);
            var normal = new Vector2(-direction.y, direction.x);
            var paving = style.Paving != null ? style.Paving : style.Masonry;

            // 门柱与普通墙身共用纹理原点和横截面，门顶女墙继续沿墙延伸。
            foreach (var entry in RaisedWallPresentation.BuildSection(gate.Id, origin, direction, normal,
                span.Start, span.OpeningStart, geometry, style.Masonry, paving))
                yield return entry;
            foreach (var entry in RaisedWallPresentation.BuildSection(gate.Id, origin, direction, normal,
                span.OpeningEnd, span.End, geometry, style.Masonry, paving))
                yield return entry;

            var footprint = RaisedWallPresentation.Rectangle(origin + direction * span.Start,
                origin + direction * span.End, normal * definition.Width * 0.5f);
            var builder = new WallSurfaceBuilder(gate.Id, "gate/" + gate.Id, geometry, footprint);
            var opening = RaisedWallPresentation.Rectangle(origin + direction * span.OpeningStart,
                origin + direction * span.OpeningEnd, normal * definition.Width * 0.5f);
            float openingHeight = definition.Height * 0.72f;
            RaisedWallPresentation.Prism(builder, opening, openingHeight, definition.Height, origin, direction,
                geometry, style.Masonry, paving, 1, true);
            // 门洞底面的朝下表面、两侧贯穿墙厚的内壁，不用黑色贴图假装一个洞。
            builder.Deck(opening, openingHeight, style.Masonry, new Color(0.48f, 0.46f, 0.42f), 0);
            Jamb(builder, origin + direction * span.OpeningStart, normal, openingHeight, geometry, style.Masonry);
            Jamb(builder, origin + direction * span.OpeningEnd, -normal, openingHeight, geometry, style.Masonry);
            RaisedWallPresentation.AddRail(builder, origin, direction, normal, span.OpeningStart, span.OpeningEnd, 1, geometry, style.Masonry, paving);
            RaisedWallPresentation.AddRail(builder, origin, direction, normal, span.OpeningStart, span.OpeningEnd, -1, geometry, style.Masonry, paving);

            // 摧毁后保留门框，移除铁栅；通行由运行规则裁决。
            if (gate.IsDestroyed)
            {
                foreach (var entry in builder.Finish())
                    yield return entry;
                yield break;
            }

            // 铁栅为有厚度的深色金属构件；开门时整体提升，材质可单独替换。
            var iron = style.Iron != null ? style.Iron : style.Masonry;
            float rise = gate.IsOpen ? openingHeight : 0;
            float barWidth = Mathf.Min(0.035f, span.TileLength * 0.04f);
            float thickness = Mathf.Min(0.012f, definition.Width * 0.02f);
            // 铁栅安装在朝向观察者的外口；放在墙厚中心会被近侧门楣遮住，只剩悬空短齿。
            Vector2 front = normal.y >= 0 ? -normal : normal;
            Vector2 ironOrigin = origin + front * (definition.Width * 0.5f - thickness);
            int count = Mathf.Clamp(Mathf.CeilToInt((span.OpeningEnd - span.OpeningStart) / 0.2f), 2, 96);
            for (int i = 0; i <= count; i++)
            {
                float distance = Mathf.Lerp(span.OpeningStart + barWidth, span.OpeningEnd - barWidth, i / (float)count);
                var bar = RaisedWallPresentation.Rectangle(ironOrigin + direction * (distance - barWidth * 0.5f),
                    ironOrigin + direction * (distance + barWidth * 0.5f), normal * thickness);
                IronPrism(builder, bar, rise, openingHeight + rise, geometry, iron);
            }
            foreach (float ratio in new[] { 0.25f, 0.75f })
            {
                var crossbar = RaisedWallPresentation.Rectangle(ironOrigin + direction * span.OpeningStart,
                    ironOrigin + direction * span.OpeningEnd, normal * thickness);
                float bottom = openingHeight * ratio + rise;
                IronPrism(builder, crossbar, bottom, bottom + barWidth, geometry, iron);
            }
            foreach (var entry in builder.Finish())
                yield return entry;
        }

        private static void Jamb(WallSurfaceBuilder builder, Vector2 center, Vector2 normal, float height,
            WallRenderGeometry geometry, Sprite masonry)
        {
            builder.Facade(center - normal * geometry.Width * 0.5f, center + normal * geometry.Width * 0.5f,
                0, height, 0, geometry.Width / geometry.TextureLength, masonry, new Color(0.6f, 0.58f, 0.54f), 1);
        }

        private static void IronPrism(WallSurfaceBuilder builder, Vector2[] polygon, float bottom, float top,
            WallRenderGeometry geometry, Sprite iron)
        {
            for (int i = 0; i < polygon.Length; i++)
                builder.Facade(polygon[i], polygon[(i + 1) % polygon.Length], bottom, top, 0, 0.2f,
                    iron, new Color(0.045f, 0.06f, 0.075f), 4);
            builder.Deck(polygon, top, iron, new Color(0.12f, 0.14f, 0.16f), 4);
        }

        /// <summary>命中真实投影体的凸包，包含已打开的门洞；倾斜门无需用正面图矩形猜测。</summary>
        public static bool HitTest(WallGateSpan span, WallDefinition definition, Vector2 point)
        {
            var direction = new Vector2(span.Direction.X, span.Direction.Y);
            var normal = new Vector2(-direction.y, direction.x) * definition.Width * 0.5f;
            var a = span.At(span.Start);
            var b = span.At(span.End);
            var footprint = RaisedWallPresentation.Rectangle(new Vector2(a.X, a.Y), new Vector2(b.X, b.Y), normal);
            var points = footprint.Concat(footprint.Select(value => value + Vector2.up * definition.Height * 1.5f))
                .OrderBy(value => value.x).ThenBy(value => value.y).ToArray();
            var hull = new List<Vector2>();
            foreach (var value in points)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], value - hull[hull.Count - 1]) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(value);
            }
            int lower = hull.Count;
            for (int i = points.Length - 2; i >= 0; i--)
            {
                var value = points[i];
                while (hull.Count > lower && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], value - hull[hull.Count - 1]) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(value);
            }
            for (int i = 0; i < hull.Count - 1; i++)
                if (Cross(hull[i + 1] - hull[i], point - hull[i]) < -0.00001f)
                    return false;
            return hull.Count >= 4;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
