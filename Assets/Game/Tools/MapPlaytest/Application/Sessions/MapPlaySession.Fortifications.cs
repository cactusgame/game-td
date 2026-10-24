using System;
using System.Collections.Generic;
using System.Linq;
using Game.Construction.Domain.Definitions;
using Game.MapEngine.Domain;
using Game.MapEngine.Domain.Geometry;
using Game.Maps.Domain;

namespace Game.Tools.MapPlaytest.Application.Sessions
{
    public sealed partial class MapPlaySession
    {
        public MapPoint SnapWallPoint(MapPoint point)
        {
            var map = Snapshot;
            var node = map.wallNodes.OrderBy(item => Distance(point, new MapPoint(item.x, item.y))).FirstOrDefault();
            if (node != null && Distance(point, new MapPoint(node.x, node.y)) < Math.Min(map.cellWidth, map.cellHeight) * 0.65f)
                return new MapPoint(node.x, node.y);
            return new MapPoint(map.originX + (float)Math.Round((point.X - map.originX) / map.cellWidth) * map.cellWidth,
                map.originY + (float)Math.Round((point.Y - map.originY) / map.cellHeight) * map.cellHeight);
        }

        public WallEdge FindWall(MapPoint point)
        {
            var map = Snapshot;
            return map.wallEdges.FirstOrDefault(edge =>
            {
                var a = map.wallNodes.Find(node => node.id == edge.from);
                var b = map.wallNodes.Find(node => node.id == edge.to);
                return SegmentGeometry.DistanceToSegment(point.X, point.Y, a.x, a.y, b.x, b.y) <= Math.Max(_catalog.Wall.Width, 0.6f);
            });
        }

        public int WallBuildCost(MapPoint from, MapPoint to)
        {
            var map = Snapshot;
            double tiles = Math.Sqrt(Math.Pow((to.X - from.X) / map.cellWidth, 2) + Math.Pow((to.Y - from.Y) / map.cellHeight, 2));
            return checked((int)Math.Ceiling(tiles - 0.0001) * (_construction?.Buildings["wall"].BuildCost ?? 0));
        }

        private bool PrepareWall(MapPoint from, MapPoint to, out MapLayout layout, out string edgeId, out string reason)
        {
            layout = Snapshot;
            edgeId = null;
            if (!CanChangeConstruction(out reason) || !CanBuildDefinition("wall", ConstructionPlacementKind.Wall, out reason)) return false;
            if (Distance(from, to) < 0.01f) { reason = "请指定不同的墙端点"; return false; }
            if (!_account.CanSpend(WallBuildCost(from, to))) { reason = "金币不足"; return false; }
            var a = EnsureWallNode(layout, from);
            var b = EnsureWallNode(layout, to);
            edgeId = MapLayout.NewId();
            layout.wallEdges.Add(new WallEdge { id = edgeId, from = a.id, to = b.id });
            return _placements.CreateCandidate().TryApplyLayout(layout, UnitsRemainClear, out reason);
        }

        public bool PreviewWall(MapPoint from, MapPoint to, out string reason) => PrepareWall(from, to, out _, out _, out reason);

        public bool TryBuildWall(MapPoint from, MapPoint to, out string reason)
        {
            RequireActive();
            if (!PrepareWall(from, to, out var layout, out var id, out reason)) return false;
            var candidate = _placements.CreateCandidate();
            if (!candidate.TryApplyLayout(layout, UnitsRemainClear, out reason) || !TryPublish(candidate, out reason)) return false;
            int cost = WallBuildCost(from, to);
            _account.TrySpend(cost);
            _wallInvestments[id] = cost;
            return true;
        }

        public bool PreviewGate(string edgeId, int start, int width, int opening, out string reason)
        {
            if (!CanChangeConstruction(out reason) || !CanBuildDefinition("gate", ConstructionPlacementKind.Gate, out reason)) return false;
            if (!_account.CanSpend(_construction?.Buildings["gate"].BuildCost ?? 0)) { reason = "金币不足"; return false; }
            var gate = new Game.Fortifications.Domain.ValueObjects.WallGateSnapshot("gate-preview", start, width, opening, false);
            return _placements.CreateCandidate().TryPlaceGate(edgeId, gate, UnitsRemainClear, out reason);
        }

        public float WallLengthTiles(string edgeId)
        {
            var map = Snapshot;
            var edge = map.wallEdges.Find(item => item.id == edgeId);
            if (edge == null) return 0;
            var a = map.wallNodes.Find(node => node.id == edge.from);
            var b = map.wallNodes.Find(node => node.id == edge.to);
            return (float)Math.Sqrt(Math.Pow((b.x - a.x) / map.cellWidth, 2) + Math.Pow((b.y - a.y) / map.cellHeight, 2));
        }

        /// <summary>售卖区间以墙的规范起点计量；门必须整座移除，余墙保留方向及门身份。</summary>
        private bool PrepareWallSale(string edgeId, float start, float end, out MapLayout layout,
            out List<KeyValuePair<string, int>> remaining, out int refund, out string reason)
        {
            layout = Snapshot;
            remaining = new List<KeyValuePair<string, int>>();
            refund = 0;
            if (!CanChangeConstruction(out reason)) return false;
            if (_construction != null && (!_construction.Buildings.TryGetValue("wall", out var wallRule) ||
                !wallRule.CanPlayerBuild || !wallRule.CanDemolish))
            { reason = "该城墙不允许玩家售卖"; return false; }
            var edge = layout.wallEdges.Find(item => item.id == edgeId);
            if (edge == null) { reason = "墙段已移除"; return false; }
            float length = WallLengthTiles(edgeId);
            if (float.IsNaN(start) || float.IsNaN(end) || start < 0 || end > length + 0.001f || end <= start ||
                Math.Abs(start - Math.Round(start)) > 0.001f || end < length - 0.001f && Math.Abs(end - Math.Round(end)) > 0.001f)
            { reason = "售卖范围无效，请沿墙选择整数区间"; return false; }
            var a = layout.wallNodes.Find(node => node.id == edge.from);
            var b = layout.wallNodes.Find(node => node.id == edge.to);
            if (a.x > b.x || a.x == b.x && a.y > b.y) { var swap = a; a = b; b = swap; }
            foreach (var gate in edge.gates)
            {
                bool overlap = start < gate.startTile + gate.widthTiles && end > gate.startTile;
                if (overlap && _construction != null && (!_construction.Buildings.TryGetValue("gate", out var gateRule) ||
                    !gateRule.CanPlayerBuild || !gateRule.CanDemolish))
                { reason = "该城门不允许玩家售卖"; return false; }
                if (overlap && (start > gate.startTile || end < gate.startTile + gate.widthTiles))
                { reason = "不能拆除半座城门，请选择完整门框区间"; return false; }
            }
            layout.wallEdges.Remove(edge);
            int investment = _wallInvestments.TryGetValue(edgeId, out int paid) ? paid : 0;
            int leftPaid = (int)Math.Floor(investment * start / length);
            int rightPaid = (int)Math.Floor(investment * (length - end) / length);
            float wallRatio = _construction?.Buildings["wall"].RefundRatio ?? 0;
            refund = (int)Math.Floor((investment - leftPaid - rightPaid) * wallRatio);
            var targetLayout = layout;
            void Retain(float lo, float hi)
            {
                if (hi - lo < 0.001f) return;
                var p = new MapPoint(a.x + (b.x - a.x) * lo / length, a.y + (b.y - a.y) * lo / length);
                var q = new MapPoint(a.x + (b.x - a.x) * hi / length, a.y + (b.y - a.y) * hi / length);
                var from = EnsureWallNode(targetLayout, p);
                var to = EnsureWallNode(targetLayout, q);
                var retained = new WallEdge { id = MapLayout.NewId(), from = from.id, to = to.id };
                foreach (var gate in edge.gates.Where(item => item.startTile >= lo && item.startTile + item.widthTiles <= hi + 0.001f))
                {
                    var copy = gate.Copy();
                    copy.startTile -= (int)Math.Round(lo);
                    retained.gates.Add(copy);
                }
                targetLayout.wallEdges.Add(retained);
            }
            Retain(0, start);
            Retain(end, length);
            var retainedNodeIds = new HashSet<string>(layout.wallEdges.SelectMany(item => new[] { item.from, item.to }));
            retainedNodeIds.UnionWith(layout.facilities.Where(item => !string.IsNullOrEmpty(item.wallNodeId)).Select(item => item.wallNodeId));
            layout.wallNodes.RemoveAll(node => !retainedNodeIds.Contains(node.id));
            foreach (var retained in layout.wallEdges.Where(item => !Snapshot.wallEdges.Any(old => old.id == item.id)))
                remaining.Add(new KeyValuePair<string, int>(retained.id, remaining.Count == 0 && start > 0 ? leftPaid : rightPaid));
            foreach (var gate in edge.gates.Where(item => start <= item.startTile && end >= item.startTile + item.widthTiles))
                if (_gateInvestments.TryGetValue(gate.id, out int gatePaid))
                    refund += (int)Math.Floor(gatePaid * (_construction?.Buildings["gate"].RefundRatio ?? 0) *
                        (double)GetGateHealth(gate.id) / _catalog.Wall.GateHealth);
            if (!_account.CanCredit(refund)) { reason = "金币账户已达上限"; return false; }
            return _placements.CreateCandidate().TryApplyLayout(layout, UnitsRemainClear, out reason);
        }

        public bool QuoteWallSale(string edgeId, float start, float end, out int refund, out string reason) =>
            PrepareWallSale(edgeId, start, end, out _, out _, out refund, out reason);

        public bool TrySellWall(string edgeId, float start, float end, out string reason)
        {
            RequireActive();
            if (!PrepareWallSale(edgeId, start, end, out var layout, out var remaining, out int refund, out reason)) return false;
            var removedGates = Snapshot.wallEdges.Find(item => item.id == edgeId).gates
                .Where(item => !layout.wallEdges.Any(wall => wall.gates.Any(gate => gate.id == item.id))).Select(item => item.id).ToArray();
            var candidate = _placements.CreateCandidate();
            if (!candidate.TryApplyLayout(layout, UnitsRemainClear, out reason) || !TryPublish(candidate, out reason)) return false;
            _wallInvestments.Remove(edgeId);
            foreach (var item in remaining) _wallInvestments[item.Key] = item.Value;
            foreach (var id in removedGates) _gateInvestments.Remove(id);
            _account.Credit(refund);
            return true;
        }

        public bool PreviewBastion(string nodeId, out string reason) => PrepareBastion(nodeId, out _, out _, out reason);

        private bool PrepareBastion(string nodeId, out MapLayout layout, out InitialFacility building, out string reason, MapLayout source = null)
        {
            layout = source ?? Snapshot;
            building = null;
            if (!CanChangeConstruction(out reason) || !CanBuildDefinition("bastion", ConstructionPlacementKind.Bastion, out reason)) return false;
            var node = layout.wallNodes.Find(item => item.id == nodeId);
            if (node == null) { reason = "请先选择墙端或连接节点"; return false; }
            if (layout.facilities.Any(item => item.wallNodeId == nodeId)) { reason = "该节点已有敌台"; return false; }
            int cost = _construction?.Buildings["bastion"].BuildCost ?? 0;
            if (!_account.CanSpend(cost)) { reason = "金币不足"; return false; }
            var size = _catalog.Find("bastion");
            if (size == null) { reason = "缺少敌台占地定义"; return false; }
            building = new InitialFacility { id = MapLayout.NewId(), definitionId = "bastion", wallNodeId = nodeId,
                x = (int)Math.Floor((node.x - layout.originX) / layout.cellWidth - size.width * 0.5f),
                y = (int)Math.Floor((node.y - layout.originY) / layout.cellHeight - size.height * 0.5f) };
            layout.facilities.Add(building);
            return _placements.CreateCandidate().TryApplyLayout(layout, UnitsRemainClear, out reason);
        }

        public bool TryBuildBastion(string nodeId, out string reason)
        {
            RequireActive();
            if (!PrepareBastion(nodeId, out var layout, out var building, out reason)) return false;
            var candidate = _placements.CreateCandidate();
            if (!candidate.TryApplyLayout(layout, UnitsRemainClear, out reason) || !TryPublish(candidate, out reason)) return false;
            int cost = _construction?.Buildings["bastion"].BuildCost ?? 0;
            _account.TrySpend(cost);
            _developments[building.id] = new Construction.Domain.Aggregates.BuildingDevelopment.BuildingDevelopment(cost);
            ReadStructures();
            return true;
        }

        public bool PreviewBastionAt(MapPoint point, out InitialFacility preview, out string reason)
        {
            preview = null;
            if (!PrepareBastionNode(point, out var map, out var nodeId, out _, out _, out reason)) return false;
            return PrepareBastion(nodeId, out _, out preview, out reason, map);
        }

        public bool TryBuildBastionAt(MapPoint point, out string reason)
        {
            RequireActive();
            if (!PrepareBastionNode(point, out var map, out var nodeId, out var replacedEdge, out var retained, out reason) ||
                !PrepareBastion(nodeId, out var layout, out var building, out reason, map)) return false;
            var candidate = _placements.CreateCandidate();
            if (!candidate.TryApplyLayout(layout, UnitsRemainClear, out reason) || !TryPublish(candidate, out reason)) return false;
            if (replacedEdge != null)
            {
                _wallInvestments.Remove(replacedEdge);
                foreach (var item in retained) _wallInvestments[item.Key] = item.Value;
            }
            int cost = _construction?.Buildings["bastion"].BuildCost ?? 0;
            _account.TrySpend(cost);
            _developments[building.id] = new Construction.Domain.Aggregates.BuildingDevelopment.BuildingDevelopment(cost);
            ReadStructures();
            return true;
        }

        private bool PrepareBastionNode(MapPoint point, out MapLayout map, out string nodeId, out string replacedEdge,
            out List<KeyValuePair<string, int>> retained, out string reason)
        {
            map = Snapshot;
            nodeId = null;
            replacedEdge = null;
            retained = new List<KeyValuePair<string, int>>();
            var closest = map.wallNodes.OrderBy(item => Distance(point, new MapPoint(item.x, item.y))).FirstOrDefault();
            if (closest != null && Distance(point, new MapPoint(closest.x, closest.y)) < Math.Min(map.cellWidth, map.cellHeight) * 0.65f)
            { nodeId = closest.id; reason = null; return true; }
            var edge = FindWall(point);
            if (edge == null || !WallGateRules.TryGetSpan(map, edge, new WallGate(), out var span))
            { reason = "请指向墙节点或墙身"; return false; }
            int split = (int)Math.Round(((point.X - span.Origin.X) * span.Direction.X + (point.Y - span.Origin.Y) * span.Direction.Y) / span.TileLength);
            float length = WallLengthTiles(edge.id);
            if (split <= 0 || split >= length || edge.gates.Any(gate => split > gate.startTile && split < gate.startTile + gate.widthTiles))
            { reason = "敌台不能截断城门或位于墙段外"; return false; }
            var a = map.wallNodes.Find(item => item.id == edge.from);
            var b = map.wallNodes.Find(item => item.id == edge.to);
            if (a.x > b.x || a.x == b.x && a.y > b.y) { var swap = a; a = b; b = swap; }
            var node = EnsureWallNode(map, span.At(split * span.TileLength));
            var left = new WallEdge { id = MapLayout.NewId(), from = a.id, to = node.id };
            var right = new WallEdge { id = MapLayout.NewId(), from = node.id, to = b.id };
            foreach (var gate in edge.gates)
            {
                var copy = gate.Copy();
                if (gate.startTile >= split) { copy.startTile -= split; right.gates.Add(copy); }
                else left.gates.Add(copy);
            }
            map.wallEdges.RemoveAll(item => item.id == edge.id);
            map.wallEdges.Add(left);
            map.wallEdges.Add(right);
            int investment = _wallInvestments.TryGetValue(edge.id, out int paid) ? paid : 0;
            int leftPaid = (int)Math.Floor(investment * split / length);
            retained.Add(new KeyValuePair<string, int>(left.id, leftPaid));
            retained.Add(new KeyValuePair<string, int>(right.id, investment - leftPaid));
            nodeId = node.id;
            replacedEdge = edge.id;
            reason = null;
            return true;
        }

        private static WallNode EnsureWallNode(MapLayout map, MapPoint point)
        {
            var node = map.wallNodes.Find(item => Distance(point, new MapPoint(item.x, item.y)) < 0.001f);
            if (node != null) return node;
            node = new WallNode { id = MapLayout.NewId(), x = point.X, y = point.Y };
            map.wallNodes.Add(node);
            return node;
        }
        private static float Distance(MapPoint a, MapPoint b) => (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    }
}
