using Game.MapEngine.Domain.Geometry;
using Game.MapEngine.Domain.Spatial;
using System;
using Game.MapEngine.Domain;

namespace Game.Maps.Domain
{
    /// <summary>编辑放置与运行建设共享的空间规则。动态占地只从设施与墙网派生。</summary>
    public static class MapSpatialRules
    {
        // 旧导航适配的保守线段包络；不是墙的物理宽度。精确轮廓接入按用户要求另行处理。
        private const float LegacyNavigationEnvelopeWidth = 0.22f;

        public static GridMap CreateNavigationMap(MapLayout layout, MapContentCatalog catalog)
        {
            var geometry = layout.Geometry;
            var walkable = new bool[geometry.CellCount];
            var obstacles = new System.Collections.Generic.List<ObstacleFootprint>();
            for (int i = 0; i < walkable.Length; i++)
                walkable[i] = TerrainRules.IsWalkable(layout.terrain[i]);
            if (Mappings.MapWallSnapshotMapper.TryRead(layout, out var walls))
                obstacles.AddRange(Game.Fortifications.Domain.Rules.WallObstacleRules.BuildNavigationObstacles(walls, catalog.Wall, LegacyNavigationEnvelopeWidth));
            foreach (var item in layout.facilities)
            {
                var definition = catalog.Find(item.definitionId);
                if (definition == null || !definition.blocksMovement)
                    continue;
                var min = new MapPoint(layout.originX + item.x * layout.cellWidth, layout.originY + item.y * layout.cellHeight);
                var max = new MapPoint(min.X + item.Width(definition) * layout.cellWidth, min.Y + item.Height(definition) * layout.cellHeight);
                obstacles.Add(ObstacleFootprint.Rectangle(min, max));
            }
            return GridNavigationBuilder.Build(geometry, walkable, obstacles);
        }

        public static bool CanPlaceFacility(MapLayout layout, MapContentCatalog catalog, InitialFacility candidate, out string reason)
        {
            var definition = catalog.Find(candidate.definitionId);
            if (definition == null)
            {
                reason = "建筑定义不存在：" + candidate.definitionId;
                return false;
            }
            var geometry = layout.Geometry;
            if (!string.IsNullOrEmpty(candidate.wallNodeId))
            {
                var node = layout.wallNodes.Find(item => item.id == candidate.wallNodeId);
                if (!definition.wallMounted || node == null ||
                    node.x < layout.originX + candidate.x * layout.cellWidth || node.x > layout.originX + (candidate.x + candidate.Width(definition)) * layout.cellWidth ||
                    node.y < layout.originY + candidate.y * layout.cellHeight || node.y > layout.originY + (candidate.y + candidate.Height(definition)) * layout.cellHeight)
                { reason = "敌台必须占据有效的墙连接节点"; return false; }
            }
            if (candidate.x < 0 || candidate.y < 0 || (long)candidate.x + candidate.Width(definition) > layout.columns || (long)candidate.y + candidate.Height(definition) > layout.rows)
            {
                reason = $"建筑占地超出地图：({candidate.x}, {candidate.y})";
                return false;
            }
            for (int y = candidate.y; y < candidate.y + candidate.Height(definition); y++)
            {
                for (int x = candidate.x; x < candidate.x + candidate.Width(definition); x++)
                {
                    var cell = new GridCell(x, y);
                    if (!geometry.Contains(cell) || !TerrainRules.IsBuildable(layout.terrain[geometry.IndexOf(cell)]))
                    {
                        reason = $"格子 ({x}, {y}) 越界或地形不可建造";
                        return false;
                    }
                    foreach (var existing in layout.facilities)
                    {
                        if (existing.id == candidate.id)
                            continue;
                        var other = catalog.Find(existing.definitionId);
                        if (other != null && x >= existing.x && x < existing.x + existing.Width(other) && y >= existing.y && y < existing.y + existing.Height(other))
                        {
                            reason = "与建筑重叠：" + existing.id;
                            return false;
                        }
                    }
                    var center = geometry.CellCenter(cell);
                    if (TouchesWall(layout, center.X, center.Y, Math.Max(geometry.CellWidth, geometry.CellHeight) * 0.5f, candidate.wallNodeId))
                    {
                        reason = "与墙体重叠";
                        return false;
                    }
                    foreach (var marker in layout.markers)
                    {
                        if (geometry.TryWorldToCell(marker.x, marker.y, out var markerCell) && markerCell.Equals(cell))
                        {
                            reason = "与入口或出生位置重叠";
                            return false;
                        }
                    }
                }
            }
            reason = null;
            return true;
        }

        public static bool HasFacility(MapLayout layout, MapContentCatalog catalog, GridCell cell, bool movementOnly)
        {
            foreach (var item in layout.facilities)
            {
                var definition = catalog.Find(item.definitionId);
                if (definition != null && (!movementOnly || definition.blocksMovement) && cell.X >= item.x && cell.X < item.x + item.Width(definition) && cell.Y >= item.y && cell.Y < item.y + item.Height(definition))
                    return true;
            }
            return false;
        }

        public static bool TouchesWall(MapLayout layout, float x, float y, float radius, string permittedNodeId = null)
        {
            foreach (var edge in layout.wallEdges)
            {
                if (!string.IsNullOrEmpty(permittedNodeId) && (edge.from == permittedNodeId || edge.to == permittedNodeId))
                {
                    foreach (var gate in edge.gates)
                        if (WallGateRules.TryGetSpan(layout, edge, gate, out var span) &&
                            SegmentGeometry.DistanceToSegment(x, y, span.At(span.Start).X, span.At(span.Start).Y,
                                span.At(span.End).X, span.At(span.End).Y) < radius + LegacyNavigationEnvelopeWidth * 0.5f)
                            return true;
                    continue;
                }
                var from = layout.wallNodes.Find(node => node.id == edge.from);
                var to = layout.wallNodes.Find(node => node.id == edge.to);
                if (from != null && to != null && SegmentGeometry.DistanceToSegment(x, y, from.x, from.y, to.x, to.y) < radius + LegacyNavigationEnvelopeWidth * 0.5f)
                    return true;
            }
            return false;
        }

    }
}
