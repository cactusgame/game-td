using Game.MapEngine.Domain.Geometry;
using System;
using Game.MapEngine.Domain;

namespace Game.Maps.Domain
{
    /// <summary>
    /// 首版设施初始布置的运行拥有者。只暴露快照，建设与拆除完整提交后重建空间派生数据。
    /// 将来 Facilities/Fortifications 扩展生命周期时，从这里迁出各自状态；编辑文档始终不参与。
    /// </summary>
    public sealed class MapPlacementState
    {
        private MapLayout _state;
        private readonly MapContentCatalog _catalog;
        public int Version { get; private set; }
        public MapLayout Snapshot => _state.Copy();
        public GridMap Navigation { get; private set; }

        public MapPlacementState(MapDefinition definition, MapContentCatalog catalog)
        {
            _catalog = catalog;
            _state = definition.CreateWorkingCopy();
            Navigation = MapSpatialRules.CreateNavigationMap(_state, catalog);
        }

        private MapPlacementState(MapPlacementState source)
        {
            _catalog = source._catalog;
            _state = source._state.Copy();
            Navigation = source.Navigation;
            Version = source.Version;
        }

        /// <summary>创建未提交的布置候选；应用会话在模拟接受新空间后才发布它。</summary>
        public MapPlacementState CreateCandidate() => new MapPlacementState(this);

        public bool TryBuild(string definitionId, int x, int y, Func<GridMap, bool> unitsRemainClear, out string reason)
        {
            var candidate = new InitialFacility { id = MapLayout.NewId(), definitionId = definitionId, x = x, y = y };
            if (!MapSpatialRules.CanPlaceFacility(_state, _catalog, candidate, out reason))
                return false;
            var next = _state.Copy();
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

        public bool TryRemove(float x, float y)
        {
            var next = _state.Copy();
            if (next.Geometry.TryWorldToCell(x, y, out var cell))
            {
                var item = next.facilities.Find(facility =>
                {
                    var definition = _catalog.Find(facility.definitionId);
                    return definition != null && cell.X >= facility.x && cell.X < facility.x + definition.width && cell.Y >= facility.y && cell.Y < facility.y + definition.height;
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
            next.wallEdges.Remove(edge);
            next.wallNodes.RemoveAll(node => !next.wallEdges.Exists(wall => wall.from == node.id || wall.to == node.id));
            Commit(next, MapSpatialRules.CreateNavigationMap(next, _catalog));
            return true;
        }

        private void Commit(MapLayout layout, GridMap navigation)
        {
            _state = layout;
            Navigation = navigation;
            Version++;
        }
    }
}
