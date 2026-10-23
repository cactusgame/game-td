using System;
using System.Collections.Generic;
using Game.Fortifications.Domain.Definitions;
using Game.Fortifications.Domain.ValueObjects;
using Game.MapEngine.Domain;

namespace Game.Fortifications.Domain.Rules
{
    /// <summary>墙网连接几何，供编辑预放与运行时查询共同使用；不生成渲染三角形。</summary>
    public static class WallConnectionRules
    {
        public static bool TryGetSpan(WallNetworkSnapshot network, WallEdgeSnapshot edge, out MapPoint start, out MapPoint end)
        {
            start = default;
            end = default;
            var a = network.FindNode(edge.From);
            var b = network.FindNode(edge.To);
            if (a == null || b == null)
                return false;
            start = a.Position;
            end = b.Position;
            return Distance(start, end) > 0.000001f;
        }

        public static IReadOnlyList<MapPoint> DirectionsAt(WallNetworkSnapshot network, string nodeId)
        {
            var result = new List<MapPoint>();
            var node = network.FindNode(nodeId);
            if (node == null)
                return result.AsReadOnly();
            foreach (var edge in network.Edges)
            {
                string otherId = edge.From == nodeId ? edge.To : edge.To == nodeId ? edge.From : null;
                var other = network.FindNode(otherId);
                if (other == null)
                    continue;
                float distance = Distance(node.Position, other.Position);
                if (distance > 0.000001f)
                    result.Add(new MapPoint((other.Position.X - node.Position.X) / distance, (other.Position.Y - node.Position.Y) / distance));
            }
            result.Sort((a, b) => Math.Atan2(a.Y, a.X).CompareTo(Math.Atan2(b.Y, b.X)));
            return result.AsReadOnly();
        }

        /// <summary>连接底座用于封合墙口，不代表敌台设施，不生成驻军或升级资格。</summary>
        public static float JunctionRadiusAt(WallNetworkSnapshot network, string nodeId, WallDefinition definition)
        {
            var node = network.FindNode(nodeId);
            if (node == null)
                throw new ArgumentException("墙节点不存在。", nameof(nodeId));
            float available = float.PositiveInfinity;
            foreach (var edge in network.Edges)
            {
                string otherId = edge.From == nodeId ? edge.To : edge.To == nodeId ? edge.From : null;
                var other = network.FindNode(otherId);
                if (other != null)
                    available = Math.Min(available, Distance(node.Position, other.Position) * 0.4f);
            }
            float required = definition.JunctionRadius;
            var directions = DirectionsAt(network, nodeId);
            for (int i = 0; i < directions.Count; i++)
            {
                for (int j = i + 1; j < directions.Count; j++)
                {
                    float dot = directions[i].X * directions[j].X + directions[i].Y * directions[j].Y;
                    float sine = (float)Math.Sqrt(Math.Max(0, (1 - dot) * 0.5f));
                    if (sine > 0.001f)
                        required = Math.Max(required, definition.Width * 0.5f / sine + definition.JunctionClearance);
                }
            }
            return Math.Max(definition.Width * 0.51f, Math.Min(required, available));
        }

        public static float PortDistance(float radius, WallDefinition definition)
        {
            return (float)Math.Sqrt(Math.Max(0, radius * radius - definition.Width * definition.Width * 0.25f));
        }

        private static float Distance(MapPoint a, MapPoint b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
