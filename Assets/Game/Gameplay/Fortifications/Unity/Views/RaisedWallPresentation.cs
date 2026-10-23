using System.Linq;
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
    /// 共用地面接口生成墙身、走道和女墙。墙与节点使用同一条连接弦，
    /// 表面按地面深度裁切；渲染片段不产生新的墙段身份。
    /// </summary>
    public static class RaisedWallPresentation
    {
        public static IEnumerable<SceneRenderEntry> Build(WallNetworkSnapshot network, WallEdgeSnapshot edge, WallDefinition definition, WallVisualStyle visuals)
        {
            Sprite masonry = visuals.Masonry;
            Sprite paving = visuals.Paving != null ? visuals.Paving : masonry;
            if (masonry == null || !WallPresentation.TryGetSpan(network, edge, out var start, out var end))
                yield break;
            string startId = edge.From;
            string endId = edge.To;
            if (start.x > end.x || start.x == end.x && start.y > end.y)
            {
                Vector2 swap = start;
                start = end;
                end = swap;
                string swapId = startId;
                startId = endId;
                endId = swapId;
            }
            var geometry = new WallRenderGeometry(definition, visuals);
            Vector2 direction = (end - start).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            float length = Vector2.Distance(start, end);
            float first = geometry.PortDistance(geometry.RadiusAt(network, network.FindNode(startId)));
            float last = length - geometry.PortDistance(geometry.RadiusAt(network, network.FindNode(endId)));
            if (last <= first + 0.0001f)
                yield break;
            float cursor = first;
            if (WallGateRules.Validate(network, edge, definition, out _))
            {
                foreach (var gate in edge.Gates.OrderBy(item => item.StartTile))
                {
                    WallGateRules.TryGetSpan(network, edge, gate, out var span);
                    foreach (var entry in BuildSection(edge.Id, start, direction, normal, cursor, span.Start, geometry, masonry, paving))
                        yield return entry;
                    cursor = span.End;
                }
            }
            foreach (var entry in BuildSection(edge.Id, start, direction, normal, cursor, last, geometry, masonry, paving))
                yield return entry;
        }

        /// <summary>
        /// 为一条墙边的指定区间生成表现：墙身立面 → 墙顶铺地 → 两侧女墙和垛口。
        /// Build 已扣除连接底座占用的端部；有城门时，门两侧分别调用本方法。
        /// 输出只包含可重建的渲染片段，不创建新的墙边，也不修改地图或导航状态。
        /// </summary>
        /// <param name="id">原墙边的稳定 ID；本段生成的所有片段仍归属于该墙边。</param>
        /// <param name="start">整条墙边规范方向后的地面起点，同时作为墙面纹理和垛口排列的共同基准。</param> 规则是：优先从左向右；如果是纵墙，则从下向上。
        /// <param name="direction">沿墙的单位向量。</param>
        /// <param name="normal">地面内垂直于墙的单位向量，用于向两侧扩出墙宽。</param>
        /// <param name="first">本段起点距 start 的距离，使用地面世界单位。</param>
        /// <param name="last">本段终点距 start 的距离；它是终点距离，不是本段长度。</param>
        /// <param name="geometry">墙身、连接底座接口、女墙和垛口共用的表现尺寸。</param>
        /// <param name="masonry">立面石砖素材。</param>
        /// <param name="paving">平台及女墙、垛口顶面使用的铺地素材。</param>
        internal static IEnumerable<SceneRenderEntry> BuildSection(string id, Vector2 start, Vector2 direction,
            Vector2 normal, float first, float last, WallRenderGeometry geometry, Sprite masonry, Sprite paving)
        {
            if (last <= first)
                yield break;

            // 将沿墙的一维距离还原为地面端点，再向法线两侧各扩展半个墙宽。
            // footprint 是完整墙顶平台的矩形轮廓，与墙的朝向无关，不包含立面向上的高度。
            Vector2 a = start + direction * first;
            Vector2 b = start + direction * last;
            var footprint = Rectangle(a, b, normal * geometry.Width * 0.5f);

            // 同一墙边可能被城门分成两段，用区间起点区分排序键，领域身份仍保留为 id。
            // builder 收集表面，并在加入表面时按地面深度、纹理重复边界进行裁切。
            var builder = new WallSurfaceBuilder(id, "wall/" + id + "/" + first.ToString("R", System.Globalization.CultureInfo.InvariantCulture), geometry, footprint);

            // 两条长边从地面升到墙顶高度，使用立面石砖；两端接连接底座或门框，不生成封口面。
            // 两次 Side 都保留整条墙的 start 作为纹理原点，避免城门两侧各自重置砖纹。
            Side(builder, footprint[0], footprint[1], 0, geometry.DeckHeight, start, direction, geometry, masonry, 0);
            Side(builder, footprint[2], footprint[3], 0, geometry.DeckHeight, start, direction, geometry, masonry, 0);

            // 先铺满整个墙顶，再在两侧叠加女墙；中间留下的路面净宽为 Width - 2 * RailWidth。
            // Deck 的纹理按世界地面坐标采样，使墙顶与连接底座平台采用同一套铺地坐标。
            builder.Deck(footprint, geometry.DeckHeight, paving, new Color(0.94f, 0.92f, 0.86f), 1);

            // +1、-1 分别表示法线两侧。AddRail 同时生成连续的矮女墙与固定间距的高垛口。
            AddRail(builder, start, direction, normal, first, last, 1, geometry, masonry, paving);
            AddRail(builder, start, direction, normal, first, last, -1, geometry, masonry, paving);

            // 按深度条带、表面层级和素材归并后输出，供编辑器画布与游戏 Mesh 视图共同使用。
            foreach (var entry in builder.Finish())
                yield return entry;
        }

        internal static void AddRail(WallSurfaceBuilder builder, Vector2 start, Vector2 direction, Vector2 normal,
            float first, float last, float sign, WallRenderGeometry geometry, Sprite masonry, Sprite paving)
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

        internal static void Prism(WallSurfaceBuilder builder, Vector2[] polygon, float bottom, float top,
            Vector2 textureOrigin, Vector2 direction, WallRenderGeometry geometry, Sprite masonry, Sprite paving, int layer, bool ends)
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

        private static void Side(WallSurfaceBuilder builder, Vector2 a, Vector2 b, float bottom, float top,
            Vector2 origin, Vector2 direction, WallRenderGeometry geometry, Sprite masonry, int layer)
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
