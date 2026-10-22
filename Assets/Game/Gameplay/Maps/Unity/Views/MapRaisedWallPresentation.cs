using Game.Maps.Unity.Assets;
using System.Collections.Generic;
using Game.Maps.Domain;
using UnityEngine;

namespace Game.Maps.Unity.Views
{
    /// <summary>
    /// 所有方向共用有厚度的墙体。地面轮廓可以旋转，高度投影始终竖直向上。
    /// 墙顶、立面与垛口由同一几何生成，不按方向更换整幅墙图片。
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
            var material = visuals.wallMasonry != null ? visuals.wallMasonry : visuals.WallBodySprite;
            if (material == null)
                yield break;
            if (!MapWallPresentation.TryGetSpan(layout, edge, out var start, out var end))
                yield break;
            // 固定方向，反向画同一条墙不会改变垛口节奏与遮挡顺序。
            if (start.x > end.x || start.x == end.x && start.y > end.y)
            {
                Vector2 swap = start;
                start = end;
                end = swap;
            }
            float unit = Mathf.Min(layout.cellWidth, layout.cellHeight);
            float length = Vector2.Distance(start, end);
            Vector2 direction = (end - start) / length;
            // 墙身接到柱子的外缘，而不是穿到柱子中心并盖住柱顶。
            if (visuals.wallCorner != null)
            {
                float inset = Mathf.Min(unit * 0.23f, length * 0.2f);
                start += direction * inset;
                end -= direction * inset;
                length = Vector2.Distance(start, end);
            }
            Vector2 normal = new Vector2(-direction.y, direction.x);
            float width = unit * 0.52f;
            float height = unit * 0.5f;
            int count = Mathf.Max(1, Mathf.CeilToInt(length / (unit * 0.42f)));
            for (int i = 0; i < count; i++)
            {
                Vector2 a = Vector2.Lerp(start, end, (float)i / count);
                Vector2 b = Vector2.Lerp(start, end, (float)(i + 1) / count);
                var builder = new Builder(material);
                var footprint = Rectangle(a, b, normal * width * 0.5f);
                builder.Box(footprint, 0, height, new Color(0.93f, 0.89f, 0.8f), i == 0, i == count - 1);

                // 后侧女墙先绘制；垛口也独立竖直抬升，不沿墙线倾斜。
                float farSign = normal.y >= 0 ? 1 : -1;
                AddParapet(builder, a, b, normal, direction, width, height, unit, farSign);
                AddParapet(builder, a, b, normal, direction, width, height, unit, -farSign);
                Vector2 ground = (a + b) * 0.5f;
                yield return new MapSceneSprites.Entry
                {
                    Id = edge.id,
                    SortKey = "wall/" + edge.id,
                    GroundX = ground.x,
                    GroundY = ground.y,
                    Wall = builder.Finish(footprint)
                };
            }
        }

        public static Vector2 Project(Vector2 ground, float height)
        {
            return ground + Vector2.up * height;
        }

        private static void AddParapet(Builder builder, Vector2 a, Vector2 b, Vector2 normal,
            Vector2 direction, float width, float height, float unit, float sign)
        {
            float railWidth = unit * 0.09f;
            Vector2 offset = normal * sign * (width - railWidth) * 0.5f;
            builder.Box(Rectangle(a + offset, b + offset, normal * railWidth * 0.5f),
                height, height + unit * 0.075f, new Color(1, 0.97f, 0.88f), false, false, false);
            Vector2 center = (a + b) * 0.5f + offset;
            Vector2 halfLength = direction * Vector2.Distance(a, b) * 0.25f;
            builder.Box(Rectangle(center - halfLength, center + halfLength, normal * railWidth * 0.5f),
                height + unit * 0.075f, height + unit * 0.22f, new Color(1, 0.99f, 0.92f), true, true, false);
        }

        private static Vector2[] Rectangle(Vector2 start, Vector2 end, Vector2 halfWidth)
        {
            return new[] { start - halfWidth, end - halfWidth, end + halfWidth, start + halfWidth };
        }

        private sealed class Builder
        {
            private readonly Sprite _sprite;
            private readonly List<Vector2> _vertices = new List<Vector2>();
            private readonly List<Vector2> _uvs = new List<Vector2>();
            private readonly List<Color32> _colors = new List<Color32>();
            private readonly List<int> _indices = new List<int>();
            private readonly Shape _shape = new Shape();

            public Builder(Sprite sprite) => _sprite = sprite;

            public void Box(Vector2[] ground, float bottom, float top, Color tint, bool startCap, bool endCap, bool masonry = true)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (i == 1 && !endCap || i == 3 && !startCap)
                        continue;
                    Vector2 a = ground[i];
                    Vector2 b = ground[(i + 1) % 4];
                    // 只绘制朝向观察者的立面。正纵向长边退化时，墙顶仍保留完整厚度。
                    if (b.x - a.x <= 0.00001f)
                        continue;
                    var face = new[] { Project(a, bottom), Project(b, bottom), Project(b, top), Project(a, top) };
                    _shape.Facades.Add(face);
                    Quad(face, tint * new Color(0.85f, 0.82f, 0.78f, 1), false, masonry);
                }
                var cap = new Vector2[4];
                for (int i = 0; i < 4; i++)
                    cap[i] = Project(ground[i], top);
                Quad(cap, tint, true, masonry);
            }

            private void Quad(Vector2[] points, Color tint, bool top, bool masonry)
            {
                int offset = _vertices.Count;
                for (int i = 0; i < 4; i++)
                {
                    _vertices.Add(points[i]);
                    // 墙身按石砌层次采样；小垛口只取单块石材内部，不把整张砖墙缩在一块砖上。
                    float u0 = masonry ? 0.08f : 0.28f;
                    float u1 = masonry ? 0.48f : 0.43f;
                    float v0 = masonry ? 0.16f : 0.65f;
                    float v1 = masonry ? (top ? 0.36f : 0.86f) : 0.78f;
                    Vector2 uv = new Vector2(i == 0 || i == 3 ? u0 : u1, i < 2 ? v0 : v1);
                    _uvs.Add(MapSurfaceGeometry.TextureUv(_sprite, uv.x, uv.y));
                    _colors.Add(tint);
                }
                _indices.AddRange(new[] { offset, offset + 1, offset + 2, offset, offset + 2, offset + 3 });
            }

            public Shape Finish(Vector2[] footprint)
            {
                _shape.GroundOutline = footprint;
                _shape.Mesh = new MapSurfaceGeometry.Patch
                {
                    Sprite = _sprite,
                    Positions = _vertices.ToArray(),
                    Uvs = _uvs.ToArray(),
                    Colors = _colors.ToArray(),
                    Indices = _indices.ToArray()
                };
                return _shape;
            }
        }
    }
}
