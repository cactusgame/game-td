using System;
using Game.MapEngine.Domain;

namespace Game.Maps.Domain
{
    /// <summary>门洞几何与墙段共用地面坐标。门框占地始终保留，只有门洞随开关改变通行。</summary>
    public static class WallGateRules
    {
        // 当前门型的门框 / 门洞比例。创作时整体缩放，避免碰撞开口和美术开口各自变化。
        public const float OpeningRatio = Game.Fortifications.Domain.Rules.WallGateRules.OpeningRatio;

        public static bool Validate(MapLayout layout, WallEdge edge, out string reason)
        {
            if (!edge.hasGate)
            {
                reason = null;
                return true;
            }
            if (edge.gate == null || layout.wallNodes.Find(node => node.id == edge.from) == null ||
                layout.wallNodes.Find(node => node.id == edge.to) == null || edge.from == edge.to)
            {
                reason = "城门数据或墙节点缺失";
                return false;
            }
            // 仅转换当前墙段，其他尚未完成的草稿内容不会干扰这项局部校验。
            var local = layout.Copy();
            local.wallNodes.RemoveAll(node => node.id != edge.from && node.id != edge.to);
            local.wallEdges = new System.Collections.Generic.List<WallEdge> { edge };
            if (!Mappings.MapWallSnapshotMapper.TryRead(local, out var network))
            {
                reason = "城门墙段或节点无效";
                return false;
            }
            return Game.Fortifications.Domain.Rules.WallGateRules.Validate(network, network.FindEdge(edge.id),
                Math.Min(layout.cellWidth, layout.cellHeight) * 0.5f, out reason);
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

    }
}
