using Game.MapEngine.Domain.Geometry;
using System;
using System.Collections.Generic;
using Game.MapEngine.Domain;

namespace Game.Maps.Domain
{
    /// <summary>地图内容的结构与空间一致性校验；不规定某种玩法必须有多少出生点。诊断保留格子与稳定 ID。</summary>
    public static class MapContentValidator
    {
        public static List<MapIssue> Validate(MapLayout layout, MapContentCatalog catalog)
        {
            var issues = new List<MapIssue>();
            if (layout == null || layout.schemaVersion != 1)
            {
                issues.Add(new MapIssue("地图格式必须是 schemaVersion 1"));
                return issues;
            }
            GridMap geometry;
            try
            {
                geometry = layout.Geometry;
            }
            catch (ArgumentException error)
            {
                issues.Add(new MapIssue("地图尺寸、格子大小或原点无效：" + error.Message));
                return issues;
            }
            if (string.IsNullOrWhiteSpace(layout.id))
                issues.Add(new MapIssue("地图 ID 不能为空"));
            if (layout.terrain == null || layout.terrain.Length != geometry.CellCount || layout.markers == null || layout.facilities == null || layout.wallNodes == null || layout.wallEdges == null)
            {
                issues.Add(new MapIssue("地形数量与地图尺寸不匹配，或内容集合缺失"));
                return issues;
            }
            for (int i = 0; i < layout.terrain.Length; i++)
            {
                if (!Enum.IsDefined(typeof(TerrainKind), layout.terrain[i]))
                {
                    var cell = geometry.CellAt(i);
                    issues.Add(new MapIssue("未知地形类型", cell.X, cell.Y));
                }
            }
            var ids = new HashSet<string>();
            bool malformed = false;
            foreach (var item in layout.facilities)
                malformed |= !CheckId(item?.id, ids, issues);
            foreach (var item in layout.markers)
                malformed |= !CheckId(item?.id, ids, issues);
            foreach (var item in layout.wallNodes)
                malformed |= !CheckId(item?.id, ids, issues);
            foreach (var item in layout.wallEdges)
                malformed |= !CheckId(item?.id, ids, issues);
            if (malformed)
                return issues;

            foreach (var item in layout.facilities)
            {
                if (!MapSpatialRules.CanPlaceFacility(layout, catalog, item, out var reason))
                    issues.Add(new MapIssue(reason, item.x, item.y, item.id));
            }
            foreach (var node in layout.wallNodes)
            {
                if (!geometry.TryWorldToCell(node.x, node.y, out var cell) || !Finite(node.x) || !Finite(node.y))
                    issues.Add(new MapIssue("墙节点超出地图", objectId: node.id));
            }
            var connections = new HashSet<string>();
            foreach (var edge in layout.wallEdges)
            {
                var a = layout.wallNodes.Find(node => node.id == edge.from);
                var b = layout.wallNodes.Find(node => node.id == edge.to);
                if (a == null || b == null || edge.from == edge.to || (a.x == b.x && a.y == b.y))
                {
                    issues.Add(new MapIssue("墙边引用缺失或长度为零", objectId: edge.id));
                    continue;
                }
                string key = string.CompareOrdinal(edge.from, edge.to) < 0 ? edge.from + "/" + edge.to : edge.to + "/" + edge.from;
                if (!connections.Add(key))
                    issues.Add(new MapIssue("两个墙节点之间存在重复墙边", objectId: edge.id));
                float length = SegmentGeometry.DistanceToSegment(b.x, b.y, a.x, a.y, a.x, a.y);
                int steps = Math.Min(100000, Math.Max(1, (int)Math.Ceiling(length / Math.Min(layout.cellWidth, layout.cellHeight) * 4)));
                for (int step = 0; step <= steps; step++)
                {
                    float t = (float)step / steps;
                    if (!geometry.TryWorldToCell(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, out var cell) || !TerrainRules.IsBuildable(layout.terrain[geometry.IndexOf(cell)]))
                    {
                        issues.Add(new MapIssue("墙体经过不可建造地形或地图外", cell.X, cell.Y, edge.id));
                        break;
                    }
                }
            }
            for (int i = 0; i < layout.wallEdges.Count; i++)
            {
                var first = layout.wallEdges[i];
                var a = layout.wallNodes.Find(node => node.id == first.from);
                var b = layout.wallNodes.Find(node => node.id == first.to);
                if (a == null || b == null)
                    continue;
                for (int j = i + 1; j < layout.wallEdges.Count; j++)
                {
                    var second = layout.wallEdges[j];
                    if (first.from == second.from || first.from == second.to || first.to == second.from || first.to == second.to)
                        continue;
                    var c = layout.wallNodes.Find(node => node.id == second.from);
                    var d = layout.wallNodes.Find(node => node.id == second.to);
                    if (c != null && d != null && SegmentGeometry.Intersects(a.x, a.y, b.x, b.y, c.x, c.y, d.x, d.y))
                        issues.Add(new MapIssue("墙线相交却未共享节点；请用同一交点节点连接，首版不自动切分墙边", objectId: first.id));
                }
            }
            foreach (var marker in layout.markers)
            {
                if (!Enum.IsDefined(typeof(MarkerKind), marker.kind))
                    issues.Add(new MapIssue("未知标记类型", objectId: marker.id));
                if (string.IsNullOrWhiteSpace(marker.name))
                    issues.Add(new MapIssue("标记名称不能为空", objectId: marker.id));
                if (!Finite(marker.x) || !Finite(marker.y) || !geometry.TryWorldToCell(marker.x, marker.y, out _))
                    issues.Add(new MapIssue("标记超出地图", objectId: marker.id));
            }
            return issues;
        }

        private static bool CheckId(string id, HashSet<string> ids, List<MapIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
            {
                issues.Add(new MapIssue("对象为空、ID 缺失或重复", objectId: id));
                return false;
            }
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
