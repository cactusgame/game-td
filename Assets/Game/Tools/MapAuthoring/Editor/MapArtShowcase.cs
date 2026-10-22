using System;
using Game.MapEngine.Domain;
using Game.Maps.Domain;
using UnityEngine;

namespace Game.Tools.MapAuthoring.Editor
{
    /// <summary>确定性的 72×48 美术验收地图。只创建新内容，不改写产品人员正在编辑的文档。</summary>
    public static class MapArtShowcase
    {
        public const string Path = "Assets/Content/Maps/ArtV2/green-valley.map.json";

        public static MapLayout Create(MapContentCatalog catalog)
        {
            var map = new MapLayout
            {
                id = "green-valley", columns = 72, rows = 48, originX = -36, originY = -24,
                terrain = new TerrainKind[72 * 48]
            };
            for (int y = 0; y < map.rows; y++)
            {
                float riverX = 49 + Mathf.Sin(y * 0.13f) * 3.2f + Mathf.Sin(y * 0.31f) * 1.1f;
                for (int x = 0; x < map.columns; x++)
                {
                    if (Mathf.Abs(x - riverX) < 1.6f + Mathf.Sin(y * 0.26f) * 0.4f ||
                        Mathf.Pow((x - 54) / 7f, 2) + Mathf.Pow((y - 36) / 4.8f, 2) < 1)
                        Set(map, x, y, TerrainKind.Water);
                }
            }
            Road(map, new Vector2(0, 17), new Vector2(12, 17), new Vector2(23, 10), new Vector2(30, 26), 1.1f);
            Road(map, new Vector2(30, 26), new Vector2(29, 9), new Vector2(51, 23), new Vector2(71, 12), 1.15f);
            Road(map, new Vector2(30, 26), new Vector2(31, 32), new Vector2(30, 39), new Vector2(35, 47), 0.8f);
            Road(map, new Vector2(16, 9), new Vector2(15, 14), new Vector2(19, 15), new Vector2(23, 17), 0.8f);

            for (int y = 0; y < map.rows; y++)
            {
                for (int x = 0; x < map.columns; x++)
                {
                    float noise = Mathf.Sin(x * 0.43f + y * 0.17f) + Mathf.Cos(y * 0.37f - x * 0.19f);
                    bool forest = (x < 12 || y > 38 || x > 61 || y < 5) && noise > 0.15f;
                    if (forest && map.terrain[y * map.columns + x] == TerrainKind.Grass && !NearRoad(map, x, y))
                        Set(map, x, y, TerrainKind.Tree);
                    if (((x - 62) * (x - 62) + (y - 7) * (y - 7) < 7 || (x - 10) * (x - 10) + (y - 33) * (y - 33) < 5) && !NearRoad(map, x, y))
                        Set(map, x, y, TerrainKind.Rock);
                }
            }
            // 村落的软土广场由连续地形轮廓绘制，不附带独立方形底座。
            for (int y = 27; y <= 33; y++)
                for (int x = 26; x <= 35; x++)
                    if (Mathf.Pow((x - 30) / 5f, 2) + Mathf.Pow((y - 30) / 3.5f, 2) < 1)
                        Set(map, x, y, TerrainKind.Dirt);

            Node(map, "west-south", 22.5f, 24.5f);
            Node(map, "west-north", 22.5f, 35.5f);
            Node(map, "north-east", 40.5f, 35.5f);
            Node(map, "east-south", 40.5f, 24.5f);
            Node(map, "gate-east", 33.5f, 24.5f);
            Node(map, "gate-west", 28.5f, 24.5f);
            Node(map, "north-gate-west", 28.5f, 35.5f);
            Node(map, "north-gate-east", 33.5f, 35.5f);
            Edge(map, "west-south", "west-north");
            Edge(map, "west-north", "north-gate-west");
            Edge(map, "north-gate-east", "north-east");
            Edge(map, "north-east", "east-south");
            Edge(map, "east-south", "gate-east");
            Edge(map, "west-south", "gate-west");
            Place(map, catalog, "hall", 29, 30);
            Place(map, catalog, "mage", 25, 29);
            Place(map, catalog, "foundry", 35, 28);
            Place(map, catalog, "tower", 24, 33);
            Place(map, catalog, "tower", 38, 33);
            Place(map, catalog, "tower", 24, 26);
            Place(map, catalog, "tower", 38, 26);
            Place(map, catalog, "farm", 14, 8);
            Place(map, catalog, "farm", 19, 8);
            Place(map, catalog, "tower", 58, 20);
            Place(map, catalog, "mage", 8, 21);
            Place(map, catalog, "foundry", 61, 17);
            Marker(map, "hero", "村口出生", MarkerKind.HeroSpawn, 30.5f, 26.5f);
            Marker(map, "west-entry", "西部林道", MarkerKind.EnemyEntry, 0.5f, 17.5f);
            Marker(map, "east-entry", "东部山道", MarkerKind.EnemyEntry, 71.5f, 12.5f);
            return map;
        }

        private static void Place(MapLayout map, MapContentCatalog catalog, string kind, int x, int y)
        {
            var definition = catalog.Find(kind);
            for (int cy = y; cy < y + definition.height; cy++)
                for (int cx = x; cx < x + definition.width; cx++)
                    if (!TerrainRules.IsBuildable(map.terrain[cy * map.columns + cx]))
                        Set(map, cx, cy, TerrainKind.Grass);
            var item = new InitialFacility { id = kind + "-" + x + "-" + y, definitionId = kind, x = x, y = y };
            if (!MapSpatialRules.CanPlaceFacility(map, catalog, item, out var reason))
                throw new InvalidOperationException(reason);
            map.facilities.Add(item);
        }

        private static void Road(MapLayout map, Vector2 a, Vector2 b, Vector2 c, Vector2 d, float radius)
        {
            for (int i = 0; i <= 400; i++)
            {
                float t = i / 400f;
                float u = 1 - t;
                Vector2 point = u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
                for (int y = Mathf.FloorToInt(point.y - radius); y <= Mathf.CeilToInt(point.y + radius); y++)
                    for (int x = Mathf.FloorToInt(point.x - radius); x <= Mathf.CeilToInt(point.x + radius); x++)
                        if (Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), point) < radius)
                            Set(map, x, y, TerrainKind.Road);
            }
        }

        private static bool NearRoad(MapLayout map, int x, int y)
        {
            for (int cy = Mathf.Max(0, y - 1); cy <= Mathf.Min(map.rows - 1, y + 1); cy++)
                for (int cx = Mathf.Max(0, x - 1); cx <= Mathf.Min(map.columns - 1, x + 1); cx++)
                    if (map.terrain[cy * map.columns + cx] == TerrainKind.Road)
                        return true;
            return false;
        }

        private static void Set(MapLayout map, int x, int y, TerrainKind kind)
        {
            if (x >= 0 && x < map.columns && y >= 0 && y < map.rows)
                map.terrain[y * map.columns + x] = kind;
        }

        private static void Node(MapLayout map, string id, float x, float y) => map.wallNodes.Add(new WallNode { id = id, x = map.originX + x, y = map.originY + y });
        private static void Edge(MapLayout map, string a, string b) => map.wallEdges.Add(new WallEdge { id = a + "--" + b, from = a, to = b });
        private static void Marker(MapLayout map, string id, string name, MarkerKind kind, float x, float y) => map.markers.Add(new MapMarker { id = id, name = name, kind = kind, x = map.originX + x, y = map.originY + y });
    }
}
