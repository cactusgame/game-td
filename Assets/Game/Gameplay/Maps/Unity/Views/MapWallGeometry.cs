using System;
using System.Collections.Generic;
using Game.Maps.Domain;
using Game.Maps.Unity.Assets;
using UnityEngine;

namespace Game.Maps.Unity.Views
{
    /// <summary>
    /// 城墙表现的共同尺寸和连接接口。节点始终是地面底座中心；端部不按导航格延长。
    /// 这些是本轮制作尺寸，不写入地图，也不替代尚未调整的移动阻挡模型。
    /// </summary>
    public sealed class MapWallGeometry
    {
        public readonly float Width;
        public readonly float DeckHeight;
        public readonly float NodeRadius;
        public readonly float RailWidth;
        public readonly float RailHeight;
        public readonly float MerlonHeight;
        public readonly float MerlonSpacing;
        public readonly float MerlonLength;
        public readonly float TextureLength;
        public readonly float TextureHeight;
        public readonly float DepthStep;

        public MapWallGeometry(MapLayout layout, MapVisualCatalog visuals)
        {
            float unit = Mathf.Min(layout.cellWidth, layout.cellHeight);
            var dimensions = visuals.wallDimensions ?? new MapVisualCatalog.WallDimensions();
            Width = Positive(dimensions.width) * unit;
            DeckHeight = Positive(dimensions.deckHeight) * unit;
            NodeRadius = Positive(dimensions.nodeRadius) * unit;
            RailWidth = Positive(dimensions.railWidth) * unit;
            RailHeight = Positive(dimensions.railHeight) * unit;
            MerlonHeight = Positive(dimensions.merlonHeight) * unit;
            MerlonSpacing = Positive(dimensions.merlonSpacing) * unit;
            MerlonLength = Positive(dimensions.merlonLength) * unit;
            TextureLength = Positive(dimensions.textureLength) * unit;
            TextureHeight = Positive(dimensions.textureHeight) * unit;
            DepthStep = unit * 0.1f;
            if (NodeRadius <= Width * 0.5f || RailWidth * 2 >= Width || MerlonLength >= MerlonSpacing)
                throw new InvalidOperationException("城墙制作尺寸无效：敌台半径须大于墙半宽，女墙须留出走道，垛口须保留间隔。");
        }

        private static float Positive(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
                throw new InvalidOperationException("城墙制作尺寸必须为有限正数。");
            return value;
        }

        public float RadiusAt(MapLayout layout, WallNode node)
        {
            float availableRadius = float.PositiveInfinity;
            foreach (var edge in layout.wallEdges)
            {
                string otherId = edge.from == node.id ? edge.to : edge.to == node.id ? edge.from : null;
                var other = layout.wallNodes.Find(value => value.id == otherId);
                if (other != null)
                    availableRadius = Mathf.Min(availableRadius, Vector2.Distance(new Vector2(node.x, node.y), new Vector2(other.x, other.y)) * 0.4f);
            }
            float requiredRadius = NodeRadius;
            var directions = DirectionsAt(layout, node);
            for (int i = 0; i < directions.Count; i++)
            {
                for (int j = i + 1; j < directions.Count; j++)
                {
                    float sine = Mathf.Sqrt(Mathf.Max(0, (1 - Vector2.Dot(directions[i], directions[j])) * 0.5f));
                    if (sine > 0.001f)
                        requiredRadius = Mathf.Max(requiredRadius, Width * 0.5f / sine + RailWidth * 0.25f);
                }
            }
            return Mathf.Max(Width * 0.51f, Mathf.Min(requiredRadius, availableRadius));
        }

        public float PortDistance(float radius)
        {
            return Mathf.Sqrt(Mathf.Max(0, radius * radius - Width * Width * 0.25f));
        }

        public List<Vector2> DirectionsAt(MapLayout layout, WallNode node)
        {
            var result = new List<Vector2>();
            foreach (var edge in layout.wallEdges)
            {
                string otherId = edge.from == node.id ? edge.to : edge.to == node.id ? edge.from : null;
                var other = layout.wallNodes.Find(value => value.id == otherId);
                if (other == null)
                    continue;
                Vector2 direction = new Vector2(other.x - node.x, other.y - node.y);
                if (direction.sqrMagnitude > 0.000001f)
                    result.Add(direction.normalized);
            }
            result.Sort((left, right) => Mathf.Atan2(left.y, left.x).CompareTo(Mathf.Atan2(right.y, right.x)));
            return result;
        }

        /// <summary>圆形敌台在每个墙口切成弦；墙矩形与敌台共享这条弦，不靠图片重叠掩盖接缝。</summary>
        public Vector2[] NodeOutline(Vector2 center, float radius, List<Vector2> directions)
        {
            var angles = new List<float>();
            for (int i = 0; i < 32; i++)
                angles.Add(i * Mathf.PI * 2 / 32);
            float halfOpening = Mathf.Asin(Mathf.Clamp01(Width * 0.5f / radius));
            foreach (var direction in directions)
            {
                float angle = Mathf.Atan2(direction.y, direction.x);
                angles.Add(Mathf.Repeat(angle - halfOpening, Mathf.PI * 2));
                angles.Add(Mathf.Repeat(angle + halfOpening, Mathf.PI * 2));
            }
            angles.Sort();
            var outline = new List<Vector2>();
            foreach (float angle in angles)
            {
                var point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (outline.Count == 0 || Vector2.Distance(outline[outline.Count - 1], point) > 0.00001f)
                    outline.Add(point);
            }
            foreach (var direction in directions)
                outline = Clip(outline, center, direction, PortDistance(radius));
            return outline.ToArray();
        }

        public static List<Vector2> Clip(List<Vector2> polygon, Vector2 center, Vector2 normal, float distance)
        {
            var output = new List<Vector2>();
            if (polygon.Count == 0)
                return output;
            var previous = polygon[polygon.Count - 1];
            float previousDistance = Vector2.Dot(previous - center, normal) - distance;
            foreach (var current in polygon)
            {
                float currentDistance = Vector2.Dot(current - center, normal) - distance;
                bool previousInside = previousDistance <= 0.000001f;
                bool currentInside = currentDistance <= 0.000001f;
                if (previousInside != currentInside)
                    output.Add(Vector2.Lerp(previous, current, previousDistance / (previousDistance - currentDistance)));
                if (currentInside)
                    output.Add(current);
                previous = current;
                previousDistance = currentDistance;
            }
            return output;
        }
    }
}
