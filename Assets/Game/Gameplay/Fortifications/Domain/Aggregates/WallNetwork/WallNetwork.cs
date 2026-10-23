using System;
using System.Collections.Generic;
using System.Linq;
using Game.Fortifications.Domain.ValueObjects;

namespace Game.Fortifications.Domain.Aggregates.WallNetwork
{
    /// <summary>
    /// 运行墙网的唯一状态拥有者。低频连接和门操作使用普通对象；不保存 ECS 血量。
    /// 应用层在候选墙网与空间/战斗同步成功后发布候选，不通过渲染或通知提交业务。
    /// </summary>
    public sealed class WallNetwork
    {
        private WallNetworkSnapshot _snapshot;
        public WallNetworkSnapshot Snapshot => _snapshot;

        public WallNetwork(WallNetworkSnapshot initial)
        {
            var connections = new HashSet<(string, string)>();
            foreach (var edge in initial.Edges)
            {
                var key = string.CompareOrdinal(edge.From, edge.To) < 0
                    ? (edge.From, edge.To)
                    : (edge.To, edge.From);
                if (!connections.Add(key) || !Rules.WallConnectionRules.TryGetSpan(initial, edge, out _, out _))
                    throw new ArgumentException("墙网包含重复连接或零长度墙段。", nameof(initial));
            }
            _snapshot = new WallNetworkSnapshot(initial.Nodes, initial.Edges);
        }

        public WallNetwork CreateCandidate() => new WallNetwork(_snapshot);

        public bool TryConnect(WallEdgeSnapshot edge, out string reason)
        {
            if (_snapshot.FindNode(edge.From) == null || _snapshot.FindNode(edge.To) == null)
            {
                reason = "连接节点不存在";
                return false;
            }
            if (_snapshot.Edges.Any(existing => existing.Id == edge.Id ||
                existing.From == edge.From && existing.To == edge.To || existing.From == edge.To && existing.To == edge.From))
            {
                reason = "墙段身份或连接重复";
                return false;
            }
            if (!Rules.WallConnectionRules.TryGetSpan(_snapshot, edge, out _, out _))
            {
                reason = "墙段长度不能为零";
                return false;
            }
            var next = new WallNetworkSnapshot(_snapshot.Nodes, _snapshot.Edges.Concat(new[] { edge }));
            if (!Rules.WallGateRules.Validate(next, edge, 0.5f, out reason))
                return false;
            _snapshot = next;
            return true;
        }

        public bool TrySetGateOpen(string edgeId, bool isOpen, out string reason)
        {
            var edge = _snapshot.FindEdge(edgeId);
            if (edge == null || !edge.HasGate)
            {
                reason = "该墙段没有城门";
                return false;
            }
            var changed = new WallEdgeSnapshot(edge.Id, edge.From, edge.To, edge.Gate.WithOpen(isOpen));
            _snapshot = new WallNetworkSnapshot(_snapshot.Nodes, _snapshot.Edges.Select(item => item.Id == edgeId ? changed : item));
            reason = null;
            return true;
        }

        public bool TryRemoveEdge(string edgeId)
        {
            if (_snapshot.FindEdge(edgeId) == null)
                return false;
            var edges = _snapshot.Edges.Where(edge => edge.Id != edgeId).ToArray();
            var nodes = _snapshot.Nodes.Where(node => edges.Any(edge => edge.From == node.Id || edge.To == node.Id));
            _snapshot = new WallNetworkSnapshot(nodes, edges);
            return true;
        }
    }
}
