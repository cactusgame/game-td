using System;
using Game.MapEngine.Domain;

namespace Game.Maps.Domain
{
    /// <summary>门洞几何与墙段共用地面坐标。门框占地始终保留，只有门洞随开关改变通行。</summary>
    public static class WallGateRules
    {
        // 当前门型的门框 / 门洞比例。创作时整体缩放，避免碰撞开口和美术开口各自变化。
        public const float OpeningRatio = 4f / 9f;

        public static bool Validate(MapLayout layout, WallEdge edge, out string reason)
        {
            reason = null;
            if (!edge.hasGate)
                return true;
            var gate = edge.gate;
            var a = layout.wallNodes.Find(node => node.id == edge.from);
            var b = layout.wallNodes.Find(node => node.id == edge.to);
            if (gate == null || a == null || b == null)
                reason = "城门数据或墙节点缺失";
            else if (!Finite(gate.position) || !Finite(gate.width) || !Finite(gate.openingWidth) ||
                     gate.position < 0 || gate.position > 1 || gate.width <= 0 || gate.openingWidth <= 0 || gate.openingWidth >= gate.width)
                reason = "城门位置、总宽或门洞净宽无效";
            else if (Math.Abs(a.y - b.y) > 0.0001f)
                reason = "当前城门素材仅支持横向墙段";
            else if (Math.Abs(gate.openingWidth - gate.width * OpeningRatio) > 0.001f)
                reason = "当前门型须等比缩放门框和门洞";
            else
            {
                float length = Math.Abs(b.x - a.x);
                float margin = Math.Min(layout.cellWidth, layout.cellHeight) * 0.5f;
                if (gate.position * length - gate.width * 0.5f < margin ||
                    gate.position * length + gate.width * 0.5f > length - margin)
                    reason = "城门太靠近墙端或墙段太短；门框须完整位于两个敌台之间";
            }
            return reason == null;
        }

        public static MapPoint Center(MapLayout layout, WallEdge edge)
        {
            var a = layout.wallNodes.Find(node => node.id == edge.from);
            var b = layout.wallNodes.Find(node => node.id == edge.to);
            return new MapPoint(a.x + (b.x - a.x) * edge.gate.position, a.y + (b.y - a.y) * edge.gate.position);
        }

        public static WallEdge FindAt(MapLayout layout, float x, float y)
        {
            WallEdge nearest = null;
            float best = float.PositiveInfinity;
            foreach (var edge in layout.wallEdges)
            {
                if (!edge.hasGate || !Validate(layout, edge, out _))
                    continue;
                var center = Center(layout, edge);
                float dx = Math.Abs(x - center.X);
                float dy = Math.Abs(y - center.Y);
                if (dx <= edge.gate.width * 0.5f && dy <= layout.cellHeight * 0.75f && dx + dy < best)
                {
                    nearest = edge;
                    best = dx + dy;
                }
            }
            return nearest;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
