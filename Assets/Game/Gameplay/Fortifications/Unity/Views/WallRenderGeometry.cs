using Game.MapEngine.Unity.Views;
using System;
using System.Collections.Generic;
using Game.Fortifications.Domain.ValueObjects;
using Game.Fortifications.Domain.Rules;
using Game.Fortifications.Unity.Assets;
using UnityEngine;

namespace Game.Fortifications.Unity.Views
{
    /// <summary>
    /// 读取工事尺寸与连接结果，生成 Unity 绘制轮廓。节点始终是地面底座中心。
    /// 这里只决定圆弧细分和裁切，不拥有拓扑、物理尺寸或连接底座设施状态。
    /// </summary>
    public sealed class WallRenderGeometry
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

        public readonly Game.Fortifications.Domain.Definitions.WallDefinition Definition;

        public WallRenderGeometry(Game.Fortifications.Domain.Definitions.WallDefinition definition, WallVisualStyle dimensions)
        {
            Definition = definition;
            Width = definition.Width;
            DeckHeight = definition.Height;
            NodeRadius = definition.JunctionRadius;
            RailWidth = Positive(dimensions.railWidth);
            RailHeight = Positive(dimensions.railHeight);
            MerlonHeight = Positive(dimensions.merlonHeight);
            MerlonSpacing = Positive(dimensions.merlonSpacing);
            MerlonLength = Positive(dimensions.merlonLength);
            TextureLength = Positive(dimensions.textureLength);
            TextureHeight = Positive(dimensions.textureHeight);
            DepthStep = 0.1f;
            if (RailWidth * 2 >= Width || MerlonLength >= MerlonSpacing)
                throw new InvalidOperationException("女墙须留出顶面，垛口须保留间隔。");
        }

        private static float Positive(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
                throw new InvalidOperationException("城墙制作尺寸必须为有限正数。");
            return value;
        }

        public float RadiusAt(WallNetworkSnapshot network, WallNodeSnapshot node)
        {
            return WallConnectionRules.JunctionRadiusAt(network, node.Id, Definition);
        }

        public float PortDistance(float radius)
        {
            return WallConnectionRules.PortDistance(radius, Definition);
        }

        public List<Vector2> DirectionsAt(WallNetworkSnapshot network, WallNodeSnapshot node)
        {
            var result = new List<Vector2>();
            foreach (var direction in WallConnectionRules.DirectionsAt(network, node.Id))
                result.Add(new Vector2(direction.X, direction.Y));
            return result;
        }

        /// <summary>圆形连接底座在每个墙口切成弦；墙矩形与连接底座共享这条弦，不靠图片重叠掩盖接缝。</summary>
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
