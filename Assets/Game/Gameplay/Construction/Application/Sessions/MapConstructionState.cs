using System.Linq;
using Game.Fortifications.Domain.ValueObjects;
using Game.Maps.Domain;
using Game.Maps.Domain.Mappings;
using Game.Fortifications.Domain.Aggregates.WallNetwork;
using Game.MapEngine.Domain.Geometry;
using System;
using Game.MapEngine.Domain;

namespace Game.Construction.Application.Sessions
{
    /// <summary>
    /// 首版运行建设协调：持有地图/设施布置与独立墙网聚合，组织空间候选。
    /// 墙网是墙与门的唯一可变状态来源；应用会话在战斗接受候选后发布，不修改编辑文档。
    /// </summary>
    public sealed class MapConstructionState
    {
        private MapLayout _state;
        private WallNetwork _walls;
        private readonly MapContentCatalog _catalog;
        public int Version { get; private set; }
        public MapLayout Snapshot
        {
            get
            {
                var snapshot = _state.Copy();
                MapWallSnapshotMapper.Write(_walls.Snapshot, snapshot);
                return snapshot;
            }
        }
        public GridMap Navigation { get; private set; }

        public MapConstructionState(MapDefinition definition, MapContentCatalog catalog)
        {
            _catalog = catalog;
            var initial = definition.CreateWorkingCopy();
            Navigation = MapSpatialRules.CreateNavigationMap(initial, catalog);
            OwnState(initial);
        }

        private MapConstructionState(MapConstructionState source)
        {
            _catalog = source._catalog;
            _state = source._state.Copy();
            _walls = source._walls.CreateCandidate();
            Navigation = source.Navigation;
            Version = source.Version;
        }

        /// <summary>创建未提交的布置候选；应用会话在模拟接受新空间后才发布它。</summary>
        public MapConstructionState CreateCandidate() => new MapConstructionState(this);

        public bool TryBuild(string definitionId, int x, int y, Func<GridMap, bool> unitsRemainClear, out string reason)
        {
            var candidate = new InitialFacility { id = MapLayout.NewId(), definitionId = definitionId, x = x, y = y };
            if (!MapSpatialRules.CanPlaceFacility(Snapshot, _catalog, candidate, out reason))
                return false;
            var next = Snapshot;
            next.facilities.Add(candidate);
            var navigation = MapSpatialRules.CreateNavigationMap(next, _catalog);
            if (!unitsRemainClear(navigation))
            {
                reason = "新建筑覆盖单位，拒绝建设";
                return false;
            }
            Commit(next, navigation);
            reason = null;
            return true;
        }

        /// <summary>运行时放门或调整已有门；复用工事规则，净空失败不提交任何墙网变化。</summary>
        public bool TryPlaceGate(string edgeId, WallGateSnapshot gate, Func<GridMap, bool> unitsRemainClear, out string reason)
        {
            var walls = _walls.CreateCandidate();
            if (!walls.TryPlaceGate(edgeId, gate, _catalog.Wall, out reason))
                return false;
            var next = Snapshot;
            MapWallSnapshotMapper.Write(walls.Snapshot, next);
            var navigation = MapSpatialRules.CreateNavigationMap(next, _catalog);
            if (!unitsRemainClear(navigation))
            {
                reason = "新城门覆盖单位，操作未提交";
                return false;
            }
            Commit(next, navigation);
            reason = null;
            return true;
        }

        public bool TrySetGateOpen(string gateId, bool isOpen, Func<GridMap, bool> unitsRemainClear, out string reason)
        {
            var gate = _walls.Snapshot.Edges.SelectMany(edge => edge.Gates).FirstOrDefault(item => item.Id == gateId);
            if (gate == null)
            {
                reason = "该墙段没有城门";
                return false;
            }
            if (gate.IsOpen == isOpen)
            {
                reason = null;
                return true;
            }
            var next = Snapshot;
            var walls = _walls.CreateCandidate();
            if (!walls.TrySetGateOpen(gateId, isOpen, out reason))
                return false;
            MapWallSnapshotMapper.Write(walls.Snapshot, next);
            var navigation = MapSpatialRules.CreateNavigationMap(next, _catalog);
            if (!isOpen && !unitsRemainClear(navigation))
            {
                reason = "门洞内或附近有单位，不能关门";
                return false;
            }
            Commit(next, navigation);
            reason = null;
            return true;
        }

        public bool TryRemove(float x, float y)
        {
            var next = Snapshot;
            if (next.Geometry.TryWorldToCell(x, y, out var cell))
            {
                var item = next.facilities.Find(facility =>
                {
                    var definition = _catalog.Find(facility.definitionId);
                    return definition != null && cell.X >= facility.x && cell.X < facility.x + facility.Width(definition) && cell.Y >= facility.y && cell.Y < facility.y + facility.Height(definition);
                });
                if (item != null)
                {
                    next.facilities.Remove(item);
                    Commit(next, MapSpatialRules.CreateNavigationMap(next, _catalog));
                    return true;
                }
            }
            var edge = next.wallEdges.Find(wall =>
            {
                var a = next.wallNodes.Find(node => node.id == wall.from);
                var b = next.wallNodes.Find(node => node.id == wall.to);
                return SegmentGeometry.DistanceToSegment(x, y, a.x, a.y, b.x, b.y) <= 0.6f;
            });
            if (edge == null)
                return false;
            var walls = _walls.CreateCandidate();
            walls.TryRemoveEdge(edge.id);
            MapWallSnapshotMapper.Write(walls.Snapshot, next);
            Commit(next, MapSpatialRules.CreateNavigationMap(next, _catalog));
            return true;
        }

        public bool TryRemoveBuilding(string id)
        {
            var next = Snapshot;
            if (next.facilities.RemoveAll(item => item.id == id) == 0) return false;
            next.wallNodes.RemoveAll(node => !next.wallEdges.Any(edge => edge.from == node.id || edge.to == node.id) &&
                !next.facilities.Any(item => item.wallNodeId == node.id));
            Commit(next, MapSpatialRules.CreateNavigationMap(next, _catalog));
            return true;
        }

        /// <summary>工事候选只能在完整校验和单位净空通过后替换，不直接编辑对局快照。</summary>
        public bool TryApplyLayout(MapLayout layout, Func<GridMap, bool> unitsRemainClear, out string reason)
        {
            foreach (var issue in MapContentValidator.Validate(layout, _catalog))
                if (issue.IsError) { reason = issue.Message; return false; }
            var navigation = MapSpatialRules.CreateNavigationMap(layout, _catalog);
            if (!unitsRemainClear(navigation)) { reason = "新占地覆盖单位，请等待单位离开"; return false; }
            Commit(layout, navigation);
            reason = null;
            return true;
        }

        private void OwnState(MapLayout layout)
        {
            _walls = new WallNetwork(MapWallSnapshotMapper.Read(layout));
            _state = layout.Copy();
            // 地图部分不保留第二份可独立修改的墙网。导出快照时由聚合重新填入描述。
            _state.wallNodes.Clear();
            _state.wallEdges.Clear();
        }

        private void Commit(MapLayout layout, GridMap navigation)
        {
            OwnState(layout);
            Navigation = navigation;
            Version++;
        }
    }
}
