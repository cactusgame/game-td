using System;
using Game.Fortifications.Domain.ValueObjects;
using Game.MapEngine.Domain;

namespace Game.Fortifications.Domain.Rules
{
    /// <summary>当前门型的连接规则；不读取图片或地图编辑器。后续门型可扩展独立定义。</summary>
    public static class WallGateRules
    {
        public const float OpeningRatio = 4f / 9f;

        public static bool Validate(WallNetworkSnapshot network, WallEdgeSnapshot edge, float endMargin, out string reason)
        {
            reason = null;
            if (!edge.HasGate)
                return true;
            var gate = edge.Gate;
            if (!WallConnectionRules.TryGetSpan(network, edge, out var a, out var b))
                reason = "城门墙段或节点无效";
            else if (!Finite(gate.Position) || !Finite(gate.Width) || !Finite(gate.OpeningWidth) ||
                     gate.Position < 0 || gate.Position > 1 || gate.Width <= 0 || gate.OpeningWidth <= 0 || gate.OpeningWidth >= gate.Width)
                reason = "城门位置、总宽或门洞净宽无效";
            else if (Math.Abs(a.Y - b.Y) > 0.0001f)
                reason = "当前门型只允许连接横向墙段";
            else if (Math.Abs(gate.OpeningWidth - gate.Width * OpeningRatio) > 0.001f)
                reason = "当前门型须等比缩放门框和门洞";
            else
            {
                float length = Math.Abs(b.X - a.X);
                if (gate.Position * length - gate.Width * 0.5f < endMargin ||
                    gate.Position * length + gate.Width * 0.5f > length - endMargin)
                    reason = "城门太靠近墙端或墙段太短；门框须完整位于两个连接底座之间";
            }
            return reason == null;
        }

        public static MapPoint Center(WallNetworkSnapshot network, WallEdgeSnapshot edge)
        {
            var a = network.FindNode(edge.From).Position;
            var b = network.FindNode(edge.To).Position;
            return new MapPoint(a.X + (b.X - a.X) * edge.Gate.Position, a.Y + (b.Y - a.Y) * edge.Gate.Position);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
