using Game.Maps.Domain.Mappings;
using Game.Fortifications.Domain.Definitions;
using Game.MapEngine.Unity.Views;
using Game.Fortifications.Unity.Views;
using Game.Maps.Unity.Assets;
using System.Collections.Generic;
using Game.MapEngine.Domain;
using Game.Maps.Domain;
using UnityEngine;

namespace Game.Maps.Unity.Views
{
    /// <summary>共享装饰与建筑的脚底锚点、尺寸、稳定排序，以及所有方向一致的实体墙几何。</summary>
    public static class MapSceneSprites
    {
        public static List<SceneRenderEntry> Build(MapLayout layout, MapVisualCatalog visuals, MapContentCatalog catalog,
            System.Func<string, int> condition = null)
        {
            var entries = new List<SceneRenderEntry>();
            var geometry = layout.Geometry;
            for (int y = 0; y < layout.rows; y++)
            {
                for (int x = 0; x < layout.columns; x++)
                {
                    var sprite = visuals.Obstacle(layout, x, y);
                    if (sprite == null)
                        continue;
                    var point = geometry.CellCenter(new GridCell(x, y));
                    float width = layout.cellWidth * (layout.terrain[y * layout.columns + x] == TerrainKind.Tree ? 1.6f : 1.25f);
                    uint hash = unchecked((uint)(x * 73856093 ^ y * 19349663));
                    float jitterX = ((hash & 255) / 255f - 0.5f) * layout.cellWidth * 0.32f;
                    float jitterY = (((hash >> 8) & 255) / 255f - 0.5f) * layout.cellHeight * 0.32f;
                    width *= 0.9f + ((hash >> 16) & 255) / 255f * 0.2f;
                    AddFoot(entries, "Obstacle", sprite, new Vector2(point.X + jitterX, point.Y + jitterY), width, "obstacle/" + x + "/" + y);
                }
            }
            foreach (var item in layout.facilities)
            {
                var definition = catalog.Find(item.definitionId);
                if (definition == null)
                    continue;
                float x = layout.originX + (item.x + item.Width(definition) * 0.5f) * layout.cellWidth;
                float y = layout.originY + (item.y + 0.5f) * layout.cellHeight;
                AddFoot(entries, item.id, (visuals.FacilityCondition(item.definitionId, condition?.Invoke(item.id) ?? 0) ?? (item.definitionId == "bastion" ? visuals.Facility("tower") : null)), new Vector2(x, y), item.Width(definition) * layout.cellWidth * visuals.facilityWidthScale * (item.definitionId == "hall" ? visuals.keepWidthScale : 1));
                if (item.definitionId == "hall" && visuals.Facility(item.definitionId) != null)
                {
                    var entry = entries[entries.Count - 1];
                    entry.Height *= visuals.keepHeightScale;
                    entry.Center = new Vector2(x, y + entry.Height * 0.45f);
                }
            }
            if (!MapWallSnapshotMapper.TryRead(layout, out var network))
            {
                entries.Sort(CompareDepth);
                return entries;
            }
            var style = visuals.WallStyle;
            // 表现高度不改变地面占地与门洞的通行宽度。
            var wallAppearance = new WallDefinition(catalog.Wall.Width * visuals.wallWidthScale, catalog.Wall.Height * visuals.wallHeightScale, Mathf.Max(catalog.Wall.JunctionRadius, catalog.Wall.Width * visuals.wallWidthScale * 0.6f), catalog.Wall.JunctionClearance);
            foreach (var edge in layout.wallEdges)
            {
                int state = condition?.Invoke(edge.id) ?? 0;
                var material = state == 2 ? visuals.destroyedMasonry : state == 1 ? visuals.damagedMasonry : null;
                var edgeStyle = material == null ? style : style.WithMaterials(material, material, style.GateSideInset);
                var edgeAppearance = state == 2
                    ? new WallDefinition(wallAppearance.Width, wallAppearance.Height * 0.18f, wallAppearance.JunctionRadius, wallAppearance.JunctionClearance)
                    : wallAppearance;
                entries.AddRange(RaisedWallPresentation.Build(network, network.FindEdge(edge.id), edgeAppearance, edgeStyle));
                var source = network.FindEdge(edge.id);
                foreach (var gate in source.Gates)
                {
                    entries.AddRange(GatePresentation.Build(network, source, gate, wallAppearance, style));
                    if (visuals.wallJunction != null && Game.Fortifications.Domain.Rules.WallGateRules.TryGetSpan(network, source, gate, out var span))
                    {
                        var origin = new Vector2(span.Origin.X, span.Origin.Y);
                        var direction = new Vector2(span.Direction.X, span.Direction.Y);
                        AddPlatform(entries, gate.Id + "/left-platform", visuals.wallJunction, origin + direction * span.Start, visuals.wallJunctionWidth, visuals.wallJunctionHeight);
                        AddPlatform(entries, gate.Id + "/right-platform", visuals.wallJunction, origin + direction * span.End, visuals.wallJunctionWidth, visuals.wallJunctionHeight);
                    }
                }
            }
            foreach (var node in layout.wallNodes)
            {
                if (visuals.wallJunction == null)
                    entries.AddRange(WallJunctionPresentation.Build(network, network.FindNode(node.id), wallAppearance, style));
                if (visuals.wallJunction != null)
                    AddPlatform(entries, node.id + "/tower", visuals.wallJunction, new Vector2(node.x, node.y), visuals.wallJunctionWidth, visuals.wallJunctionHeight);
            }
            entries.Sort(CompareDepth);
            return entries;
        }

        private static void AddPlatform(List<SceneRenderEntry> entries, string id, Sprite sprite, Vector2 ground, float width, float height)
        {
            AddFoot(entries, id, sprite, ground, width);
            if (sprite != null && height > 0)
            {
                var entry = entries[entries.Count - 1];
                entry.Height = height;
                entry.Center = ground + Vector2.up * (height * 0.45f - width * 0.3f);
            }
            // 平台图片覆盖墙体接缝，按近侧墙脚排序，避免同一点的 Mesh 遮住站兵面。
            if (sprite != null)
                entries[entries.Count - 1].GroundY -= Mathf.Max(0.6f, width);
        }

        private static int CompareDepth(SceneRenderEntry left, SceneRenderEntry right)
        {
            int result = right.GroundY.CompareTo(left.GroundY);
            if (result != 0)
                return result;
            result = left.DepthLayer.CompareTo(right.DepthLayer);
            if (result != 0)
                return result;
            result = left.GroundX.CompareTo(right.GroundX);
            if (result != 0)
                return result;
            return System.StringComparer.Ordinal.Compare(left.SortKey, right.SortKey);
        }

        /// <summary>图片点击属于表现查询；命中后只把稳定城门 ID 交给编辑或开关门用例。</summary>
        public static string PickGate(MapLayout layout, MapVisualCatalog visuals, Vector2 point, MapContentCatalog catalog)
        {
            string selected = null;
            float front = float.PositiveInfinity;
            if (!MapWallSnapshotMapper.TryRead(layout, out var network))
                return null;
            foreach (var edge in network.Edges)
            {
                foreach (var gate in edge.Gates)
                {
                    if (!Game.Fortifications.Domain.Rules.WallGateRules.ValidateGate(network, edge, gate, catalog.Wall, out _) ||
                        !Game.Fortifications.Domain.Rules.WallGateRules.TryGetSpan(network, edge, gate, out var span))
                        continue;
                    if (GatePresentation.HitTest(span, new WallDefinition(catalog.Wall.Width * visuals.wallWidthScale, catalog.Wall.Height * visuals.wallHeightScale, Mathf.Max(catalog.Wall.JunctionRadius, catalog.Wall.Width * visuals.wallWidthScale * 0.6f), catalog.Wall.JunctionClearance), point) && span.Center.Y < front)
                    {
                        selected = gate.Id;
                        front = span.Center.Y;
                    }
                }
            }
            return selected;
        }

        private static void AddFoot(List<SceneRenderEntry> entries, string id, Sprite sprite, Vector2 ground, float width, string sortKey = null)
        {
            if (sprite == null)
                return;
            float height = width * sprite.rect.height / sprite.rect.width;
            entries.Add(new SceneRenderEntry { Id = id, SortKey = sortKey ?? "object/" + id, DepthLayer = 1, Sprite = sprite, Center = ground + Vector2.up * height * 0.45f, Width = width, Height = height, GroundY = ground.y, GroundX = ground.x });
        }
    }
}









