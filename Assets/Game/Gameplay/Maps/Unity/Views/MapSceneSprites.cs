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
        public sealed class Entry
        {
            public string Id;
            public Sprite Sprite;
            public Vector2 Center;
            public float Width;
            public float Height;
            public float Angle;
            public float GroundY;
            public float GroundX;
            public string SortKey;
            public int DepthLayer;
            public MapRaisedWallPresentation.Shape Wall;
        }

        public static List<Entry> Build(MapLayout layout, MapVisualCatalog visuals, MapContentCatalog catalog)
        {
            var entries = new List<Entry>();
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
                float x = layout.originX + (item.x + definition.width * 0.5f) * layout.cellWidth;
                float y = layout.originY + (item.y + 0.5f) * layout.cellHeight;
                AddFoot(entries, item.id, visuals.Facility(item.definitionId), new Vector2(x, y), definition.width * layout.cellWidth * 1.2f);
            }
            foreach (var edge in layout.wallEdges)
            {
                entries.AddRange(MapRaisedWallPresentation.Build(layout, edge, visuals));
                if (edge.hasGate && WallGateRules.Validate(layout, edge, out _))
                {
                    var center = WallGateRules.Center(layout, edge);
                    int count = entries.Count;
                    AddFoot(entries, edge.id, edge.gate.isOpen ? visuals.gateOpen : visuals.gateClosed,
                        new Vector2(center.X, center.Y), edge.gate.width, "gate/" + edge.id);
                    if (entries.Count > count)
                    {
                        var gate = entries[entries.Count - 1];
                        var wall = new MapWallGeometry(layout, visuals);
                        gate.Center.y = center.Y - wall.Width * 0.5f + gate.Height * (0.5f - visuals.gateFootInset);
                        gate.GroundY = center.Y - wall.Width * 0.5f - wall.DepthStep;
                    }
                }
            }
            foreach (var node in layout.wallNodes)
                entries.AddRange(MapWallNodePresentation.Build(layout, node, visuals));
            entries.Sort(CompareDepth);
            return entries;
        }

        private static int CompareDepth(Entry left, Entry right)
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

        /// <summary>图片点击属于表现查询；命中后只把稳定墙段 ID 交给编辑或开关门用例。</summary>
        public static string PickGate(MapLayout layout, MapVisualCatalog visuals, Vector2 point)
        {
            string selected = null;
            float front = float.PositiveInfinity;
            var wall = new MapWallGeometry(layout, visuals);
            foreach (var edge in layout.wallEdges)
            {
                if (!edge.hasGate || !WallGateRules.Validate(layout, edge, out _))
                    continue;
                var sprite = edge.gate.isOpen ? visuals.gateOpen : visuals.gateClosed;
                if (sprite == null)
                    continue;
                var center = WallGateRules.Center(layout, edge);
                float height = edge.gate.width * sprite.rect.height / sprite.rect.width;
                float bottom = center.Y - wall.Width * 0.5f - height * visuals.gateFootInset;
                var bounds = new Rect(center.X - edge.gate.width * 0.5f, bottom, edge.gate.width, height);
                if (bounds.Contains(point) && center.Y < front)
                {
                    selected = edge.id;
                    front = center.Y;
                }
            }
            return selected;
        }

        private static void AddFoot(List<Entry> entries, string id, Sprite sprite, Vector2 ground, float width, string sortKey = null)
        {
            if (sprite == null)
                return;
            float height = width * sprite.rect.height / sprite.rect.width;
            entries.Add(new Entry { Id = id, SortKey = sortKey ?? "object/" + id, DepthLayer = 1, Sprite = sprite, Center = ground + Vector2.up * height * 0.45f, Width = width, Height = height, GroundY = ground.y, GroundX = ground.x });
        }
    }
}
