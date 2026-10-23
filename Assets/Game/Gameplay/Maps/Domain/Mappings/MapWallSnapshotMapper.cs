using System;
using System.Linq;
using Game.Fortifications.Domain.ValueObjects;
using Game.MapEngine.Domain;

namespace Game.Maps.Domain.Mappings
{
    /// <summary>地图中的预放描述与工事快照的边界转换。转换不会改写源文件或源文档。</summary>
    public static class MapWallSnapshotMapper
    {
        /// <summary>编辑草稿可有未修复引用；表现跳过无效墙网，诊断仍由内容校验器报告。</summary>
        public static bool TryRead(MapLayout layout, out WallNetworkSnapshot network)
        {
            network = null;
            if (layout.wallNodes == null || layout.wallEdges == null ||
                layout.wallNodes.Any(node => node == null) || layout.wallEdges.Any(edge => edge == null))
                return false;
            try
            {
                network = Read(layout);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public static WallNetworkSnapshot Read(MapLayout layout)
        {
            return new WallNetworkSnapshot(
                layout.wallNodes.Select(node => new WallNodeSnapshot(node.id, new MapPoint(node.x, node.y))),
                layout.wallEdges.Select(edge => new WallEdgeSnapshot(edge.id, edge.from, edge.to,
                    edge.gates?.Select(gate => gate == null ? null : new WallGateSnapshot(gate.id, gate.startTile, gate.widthTiles, gate.openingTiles, gate.isOpen, gate.isDestroyed)))),
                layout.cellWidth, layout.cellHeight);
        }

        public static void Write(WallNetworkSnapshot network, MapLayout target)
        {
            target.wallNodes = network.Nodes.Select(node => new WallNode { id = node.Id, x = node.Position.X, y = node.Position.Y }).ToList();
            target.wallEdges = network.Edges.Select(edge => new WallEdge
            {
                id = edge.Id, from = edge.From, to = edge.To,
                gates = edge.Gates.Select(gate => new WallGate
                {
                    id = gate.Id, startTile = gate.StartTile, widthTiles = gate.WidthTiles,
                    openingTiles = gate.OpeningTiles, isOpen = gate.IsOpen, isDestroyed = gate.IsDestroyed
                }).ToList()
            }).ToList();
        }
    }
}
