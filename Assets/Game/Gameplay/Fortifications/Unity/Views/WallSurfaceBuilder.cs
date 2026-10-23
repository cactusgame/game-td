using Game.MapEngine.Unity.Views;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Fortifications.Unity.Views
{
    /// <summary>
    /// 按地面深度与纹理重复边界裁切表面，再归并为排序片段。
    /// UV 按真实长度生成，裁切只插值，不把一整张砖墙反复压缩到渲染片段里。
    /// </summary>
    internal sealed class WallSurfaceBuilder
    {
        private struct Point
        {
            public Vector2 Ground;
            public float Height;
            public Vector2 Material;
            public static Point Lerp(Point a, Point b, float t)
            {
                return new Point
                {
                    Ground = Vector2.Lerp(a.Ground, b.Ground, t),
                    Height = Mathf.Lerp(a.Height, b.Height, t),
                    Material = Vector2.Lerp(a.Material, b.Material, t)
                };
            }
        }

        private sealed class Batch
        {
            public Sprite Sprite;
            public int Band;
            public int Layer;
            public readonly List<Vector2> Positions = new List<Vector2>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<Color32> Colors = new List<Color32>();
            public readonly List<int> Indices = new List<int>();
            public readonly List<Vector2[]> Facades = new List<Vector2[]>();
        }

        private readonly string _id;
        private readonly string _sortKey;
        private readonly WallRenderGeometry _geometry;
        private readonly Vector2[] _footprint;
        private readonly Dictionary<(int, int, Sprite), Batch> _batches = new Dictionary<(int, int, Sprite), Batch>();

        public WallSurfaceBuilder(string id, string sortKey, WallRenderGeometry geometry, Vector2[] footprint)
        {
            _id = id;
            _sortKey = sortKey;
            _geometry = geometry;
            _footprint = footprint;
        }

        public void Facade(Vector2 a, Vector2 b, float bottom, float top, float u0, float u1, Sprite sprite, Color tint, int layer)
        {
            if (b.x - a.x <= 0.00001f || top <= bottom)
                return;
            var ground = new[] { a, b, b, a };
            var heights = new[] { bottom, bottom, top, top };
            var uv = new[]
            {
                new Vector2(u0, bottom / _geometry.TextureHeight), new Vector2(u1, bottom / _geometry.TextureHeight),
                new Vector2(u1, top / _geometry.TextureHeight), new Vector2(u0, top / _geometry.TextureHeight)
            };
            Surface(ground, heights, uv, sprite, tint, layer, true);
        }

        public void Deck(Vector2[] polygon, float height, Sprite sprite, Color tint, int layer)
        {
            var heights = new float[polygon.Length];
            var uv = new Vector2[polygon.Length];
            for (int i = 0; i < polygon.Length; i++)
            {
                heights[i] = height;
                // 墙顶与连接底座顶共用世界铺地坐标，连接处没有各自的纹理起点。
                uv[i] = polygon[i] / _geometry.TextureLength;
            }
            Surface(polygon, heights, uv, sprite, tint, layer, false);
        }

        private void Surface(Vector2[] ground, float[] heights, Vector2[] uv, Sprite sprite, Color tint, int layer, bool facade)
        {
            if (sprite == null || ground.Length < 3)
                return;
            var polygon = new List<Point>();
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            for (int i = 0; i < ground.Length; i++)
            {
                polygon.Add(new Point { Ground = ground[i], Height = heights[i], Material = uv[i] });
                minY = Mathf.Min(minY, ground[i].y);
                maxY = Mathf.Max(maxY, ground[i].y);
            }
            int firstBand = Mathf.FloorToInt(minY / _geometry.DepthStep);
            int lastBand = Mathf.FloorToInt(maxY / _geometry.DepthStep);
            for (int band = firstBand; band <= lastBand; band++)
            {
                var strip = Clip(polygon, p => p.Ground.y - band * _geometry.DepthStep);
                strip = Clip(strip, p => (band + 1) * _geometry.DepthStep - p.Ground.y);
                if (strip.Count >= 3)
                    TileSurface(strip, sprite, tint, band, layer * 2 + (facade ? 0 : 1), facade);
            }
        }

        private void TileSurface(List<Point> polygon, Sprite sprite, Color tint, int band, int layer, bool facade)
        {
            Vector2 minimum = polygon[0].Material;
            Vector2 maximum = minimum;
            foreach (var point in polygon)
            {
                minimum = Vector2.Min(minimum, point.Material);
                maximum = Vector2.Max(maximum, point.Material);
            }
            for (int y = Mathf.FloorToInt(minimum.y); y <= Mathf.FloorToInt(maximum.y); y++)
            {
                for (int x = Mathf.FloorToInt(minimum.x); x <= Mathf.FloorToInt(maximum.x); x++)
                {
                    var tile = Clip(polygon, p => p.Material.x - x);
                    tile = Clip(tile, p => x + 1 - p.Material.x);
                    tile = Clip(tile, p => p.Material.y - y);
                    tile = Clip(tile, p => y + 1 - p.Material.y);
                    Append(tile, sprite, tint, band, layer, facade);
                }
            }
        }

        private void Append(List<Point> polygon, Sprite sprite, Color tint, int band, int layer, bool facade)
        {
            if (polygon.Count < 3)
                return;
            float area = 0;
            var projected = new Vector2[polygon.Count];
            for (int i = 0; i < polygon.Count; i++)
                projected[i] = RaisedWallPresentation.Project(polygon[i].Ground, polygon[i].Height);
            for (int i = 0; i < projected.Length; i++)
            {
                Vector2 a = projected[i];
                Vector2 b = projected[(i + 1) % projected.Length];
                a -= projected[0];
                b -= projected[0];
                area += a.x * b.y - a.y * b.x;
            }
            if (Mathf.Abs(area) < 0.0000001f)
                return;
            var key = (band, layer, sprite);
            if (!_batches.TryGetValue(key, out var batch))
            {
                batch = new Batch { Sprite = sprite, Band = band, Layer = layer };
                _batches.Add(key, batch);
            }
            int offset = batch.Positions.Count;
            for (int i = 0; i < polygon.Count; i++)
            {
                batch.Positions.Add(projected[i]);
                batch.Uvs.Add(SpriteTextureCoordinates.Sample(sprite, polygon[i].Material.x, polygon[i].Material.y));
                batch.Colors.Add(tint);
            }
            for (int i = 1; i < polygon.Count - 1; i++)
            {
                batch.Indices.Add(offset);
                batch.Indices.Add(offset + (area > 0 ? i : i + 1));
                batch.Indices.Add(offset + (area > 0 ? i + 1 : i));
            }
            if (facade)
                batch.Facades.Add(projected);
        }

        private static List<Point> Clip(List<Point> polygon, Func<Point, float> signedDistance)
        {
            var output = new List<Point>();
            if (polygon.Count == 0)
                return output;
            var previous = polygon[polygon.Count - 1];
            float previousDistance = signedDistance(previous);
            foreach (var current in polygon)
            {
                float currentDistance = signedDistance(current);
                bool previousInside = previousDistance >= 0;
                bool currentInside = currentDistance >= 0;
                if (previousInside != currentInside)
                    output.Add(Point.Lerp(previous, current, previousDistance / (previousDistance - currentDistance)));
                if (currentInside)
                    output.Add(current);
                previous = current;
                previousDistance = currentDistance;
            }
            return output;
        }

        public IEnumerable<SceneRenderEntry> Finish()
        {
            var batches = new List<Batch>(_batches.Values);
            batches.Sort((a, b) =>
            {
                int depth = b.Band.CompareTo(a.Band);
                return depth != 0 ? depth : a.Layer.CompareTo(b.Layer);
            });
            foreach (var batch in batches)
            {
                var shape = new RaisedSurfaceShape
                {
                    GroundOutline = _footprint,
                    Mesh = new SurfaceMeshData
                    {
                        Sprite = batch.Sprite, Positions = batch.Positions.ToArray(), Uvs = batch.Uvs.ToArray(),
                        Colors = batch.Colors.ToArray(), Indices = batch.Indices.ToArray()
                    }
                };
                shape.Facades.AddRange(batch.Facades);
                yield return new SceneRenderEntry
                {
                    Id = _id, SortKey = _sortKey + "/" + batch.Band + "/" + batch.Layer,
                    GroundY = (batch.Band + 0.5f) * _geometry.DepthStep,
                    GroundX = _footprint.Length > 0 ? _footprint[0].x : 0,
                    DepthLayer = batch.Layer, Wall = shape
                };
            }
        }
    }
}
