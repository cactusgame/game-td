using System;
using System.Linq;
using Game.Fortifications.Domain.Definitions;
using Game.Fortifications.Domain.ValueObjects;
using Game.MapEngine.Domain;

namespace Game.Fortifications.Domain.Rules
{
    /// <summary>任意墙向、多座门、整数 Tile 区间及连接底座净空的共同规则。</summary>
    public static class WallGateRules
    {
        public static bool TryGetSpan(WallNetworkSnapshot network, WallEdgeSnapshot edge, WallGateSnapshot gate, out WallGateSpan span)
        {
            span = default;
            if (!WallConnectionRules.TryGetSpan(network, edge, out var a, out var b))
                return false;
            if (a.X > b.X || a.X == b.X && a.Y > b.Y)
            {
                var swap = a;
                a = b;
                b = swap;
            }
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            var direction = new MapPoint(dx / length, dy / length);
            // 正方形格任意方向均为一个格长；长方形格按归一化格坐标中的单位长度换算。
            float tileLength = 1f / (float)Math.Sqrt(direction.X * direction.X / (network.CellWidth * network.CellWidth) +
                direction.Y * direction.Y / (network.CellHeight * network.CellHeight));
            float start = gate.StartTile * tileLength;
            float end = start + gate.WidthTiles * tileLength;
            float center = (start + end) * 0.5f;
            float halfOpening = gate.OpeningTiles * tileLength * 0.5f;
            span = new WallGateSpan(a, direction, start, end, center - halfOpening, center + halfOpening, tileLength);
            return true;
        }

        public static int SnapStartTile(WallNetworkSnapshot network, WallEdgeSnapshot edge, MapPoint center, int widthTiles)
        {
            var probe = new WallGateSnapshot("preview", 0, widthTiles, 1, false);
            if (!TryGetSpan(network, edge, probe, out var span))
                return 0;
            float distance = (center.X - span.Origin.X) * span.Direction.X + (center.Y - span.Origin.Y) * span.Direction.Y;
            return (int)Math.Round(distance / span.TileLength - widthTiles * 0.5f, MidpointRounding.AwayFromZero);
        }

        public static bool Validate(WallNetworkSnapshot network, WallEdgeSnapshot edge, WallDefinition definition, out string reason)
        {
            foreach (var gate in edge.Gates)
            {
                if (!ValidateGate(network, edge, gate, definition, out reason))
                    return false;
            }
            reason = null;
            return true;
        }

        public static bool ValidateGate(WallNetworkSnapshot network, WallEdgeSnapshot edge, WallGateSnapshot gate,
            WallDefinition definition, out string reason)
        {
            reason = null;
            if (gate == null || string.IsNullOrWhiteSpace(gate.Id))
                reason = "城门缺少独立 ID";
            else if (gate.StartTile < 0 || gate.WidthTiles < 3 || gate.OpeningTiles < 1 || gate.OpeningTiles > gate.WidthTiles - 2 || (gate.WidthTiles - gate.OpeningTiles) % 2 != 0)
                reason = "城门使用整数 Tile：门框至少 3 格，门洞至少 1 格，两侧门柱各至少保留 1 个整格且等宽";
            else if (!TryGetSpan(network, edge, gate, out var span))
                reason = "城门所属墙段或节点无效";
            else
            {
                var a = network.FindNode(edge.From);
                var b = network.FindNode(edge.To);
                float dx = b.Position.X - a.Position.X;
                float dy = b.Position.Y - a.Position.Y;
                float length = (float)Math.Sqrt(dx * dx + dy * dy);
                string firstId = a.Position == span.Origin ? a.Id : b.Id;
                string lastId = firstId == a.Id ? b.Id : a.Id;
                float first = WallConnectionRules.PortDistance(WallConnectionRules.JunctionRadiusAt(network, firstId, definition), definition);
                float last = length - WallConnectionRules.PortDistance(WallConnectionRules.JunctionRadiusAt(network, lastId, definition), definition);
                if (span.Start < first - 0.0001f || span.End > last + 0.0001f)
                    reason = "城门超出可用墙段或覆盖端部连接底座";
                foreach (var other in edge.Gates)
                {
                    if (other == null || ReferenceEquals(other, gate))
                        continue;
                    if (other.Id == gate.Id)
                    {
                        reason = "城门 ID 重复：" + gate.Id;
                        break;
                    }
                    if (TryGetSpan(network, edge, other, out var otherSpan) &&
                        span.Start < otherSpan.End - 0.0001f && span.End > otherSpan.Start + 0.0001f)
                    {
                        reason = "门框与城门重叠：" + other.Id;
                        break;
                    }
                }
            }
            return reason == null;
        }
    }
}
