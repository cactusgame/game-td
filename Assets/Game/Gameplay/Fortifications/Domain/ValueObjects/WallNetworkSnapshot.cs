using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Fortifications.Domain.ValueObjects
{
    /// <summary>编辑预放与运行墙网共同使用的隔离快照；不持有可变地图 DTO 或 Unity 对象。</summary>
    public sealed class WallNetworkSnapshot
    {
        public IReadOnlyList<WallNodeSnapshot> Nodes { get; }
        public float CellWidth { get; }
        public float CellHeight { get; }
        public IReadOnlyList<WallEdgeSnapshot> Edges { get; }

        public WallNetworkSnapshot(IEnumerable<WallNodeSnapshot> nodes, IEnumerable<WallEdgeSnapshot> edges, float cellWidth = 1, float cellHeight = 1)
        {
            if (float.IsNaN(cellWidth) || float.IsInfinity(cellWidth) || cellWidth <= 0 ||
                float.IsNaN(cellHeight) || float.IsInfinity(cellHeight) || cellHeight <= 0)
                throw new ArgumentException("Tile 尺寸必须为有限正数。");
            CellWidth = cellWidth;
            CellHeight = cellHeight;
            var nodeArray = nodes.ToArray();
            var edgeArray = edges.ToArray();
            if (nodeArray.Any(node => node == null) || edgeArray.Any(edge => edge == null))
                throw new ArgumentException("墙网不能包含空节点或空墙段。");
            if (nodeArray.Select(node => node.Id).Distinct().Count() != nodeArray.Length ||
                edgeArray.Select(edge => edge.Id).Distinct().Count() != edgeArray.Length)
                throw new ArgumentException("墙网身份重复。");
            foreach (var edge in edgeArray)
            {
                if (!nodeArray.Any(node => node.Id == edge.From) || !nodeArray.Any(node => node.Id == edge.To))
                    throw new ArgumentException("墙段引用不存在的节点。");
            }
            Nodes = Array.AsReadOnly(nodeArray);
            Edges = Array.AsReadOnly(edgeArray);
        }

        public WallNodeSnapshot FindNode(string id) => Nodes.FirstOrDefault(node => node.Id == id);
        public WallEdgeSnapshot FindEdge(string id) => Edges.FirstOrDefault(edge => edge.Id == id);
    }
}
