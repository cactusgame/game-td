using System;
using Game.Fortifications.Domain.Aggregates.WallNetwork;
using Game.Fortifications.Domain.Definitions;
using Game.Fortifications.Domain.ValueObjects;
using Game.Maps.Domain.Mappings;
using Game.MapEngine.Domain;
using GateRules = Game.Fortifications.Domain.Rules.WallGateRules;

namespace Game.Maps.Domain
{
    /// <summary>地图初始描述与工事规则的适配；不拥有第二套门位置或尺寸规则。</summary>
    public static class WallGateRules
    {
        public static WallGate Find(MapLayout layout, string gateId, out WallEdge owner)
        {
            foreach (var edge in layout.wallEdges)
            {
                var gate = edge.gates?.Find(item => item != null && item.id == gateId);
                if (gate != null)
                {
                    owner = edge;
                    return gate;
                }
            }
            owner = null;
            return null;
        }

        public static bool Validate(MapLayout layout, WallEdge edge, out string reason, WallDefinition definition = null)
        {
            if (edge.gates == null)
            {
                reason = "城门集合缺失";
                return false;
            }
            if (edge.gates.Count == 0)
            {
                reason = null;
                return true;
            }
            if (!MapWallSnapshotMapper.TryRead(layout, out var network))
            {
                reason = "城门墙段或节点无效";
                return false;
            }
            return GateRules.Validate(network, network.FindEdge(edge.id), definition ?? WallDefinition.Default, out reason);
        }

        public static bool ValidateGate(MapLayout layout, WallEdge edge, WallGate gate, out string reason, WallDefinition definition = null)
        {
            if (gate == null || !MapWallSnapshotMapper.TryRead(layout, out var network))
            {
                reason = "城门墙段或节点无效";
                return false;
            }
            var source = network.FindEdge(edge.id);
            var item = System.Linq.Enumerable.FirstOrDefault(source.Gates, value => value.Id == gate.id);
            return GateRules.ValidateGate(network, source, item, definition ?? WallDefinition.Default, out reason);
        }

        public static bool TryGetSpan(MapLayout layout, WallEdge edge, WallGate gate, out WallGateSpan span)
        {
            span = default;
            if (gate == null || !MapWallSnapshotMapper.TryRead(layout, out var network))
                return false;
            return GateRules.TryGetSpan(network, network.FindEdge(edge.id), ToSnapshot(gate), out span);
        }

        public static MapPoint Center(MapLayout layout, WallEdge edge, WallGate gate)
        {
            if (!TryGetSpan(layout, edge, gate, out var span))
                throw new ArgumentException("城门墙段或节点无效");
            return span.Center;
        }

        public static int SnapStartTile(MapLayout layout, WallEdge edge, MapPoint center, int widthTiles)
        {
            var network = MapWallSnapshotMapper.Read(layout);
            return GateRules.SnapStartTile(network, network.FindEdge(edge.id), center, widthTiles);
        }

        public static bool TryPlace(MapLayout layout, WallEdge edge, WallGate gate, WallDefinition definition, out string reason)
        {
            if (!MapWallSnapshotMapper.TryRead(layout, out var network))
            {
                reason = "请先修复墙网的节点或引用问题";
                return false;
            }
            var walls = new WallNetwork(network);
            if (!walls.TryPlaceGate(edge.id, ToSnapshot(gate), definition, out reason))
                return false;
            MapWallSnapshotMapper.Write(walls.Snapshot, layout);
            return true;
        }

        private static WallGateSnapshot ToSnapshot(WallGate gate)
        {
            return new WallGateSnapshot(gate.id, gate.startTile, gate.widthTiles, gate.openingTiles, gate.isOpen);
        }
    }
}
