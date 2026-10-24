using System;
using Game.MapEngine.Domain;
using Game.MapEngine.Domain.Geometry;
using Game.Maps.Domain;
using UnityEngine;

namespace Game.Tools.MapAuthoring.Editor
{
    /// <summary>
    /// 参考截图的逐点布置。截图为1936×1080，20像素对应一个地面单位；
    /// 墙节点记录墙脚，建筑记录脚底，图片顶部不作为地面位置。
    /// 生成结果保存为普通地图文档，编辑器与试玩无需依赖本生成器。
    /// </summary>
    public static class ReferenceCitadelLayout
    {
        public static MapLayout Create()
        {
            var map = new MapLayout
            {
                id = "reference-citadel", columns = 97, rows = 54,
                originX = -48.5f, originY = -27, terrain = new TerrainKind[97 * 54]
            };
            BuildOuterWalls(map);
            BuildInnerWalls(map);
            BuildKeep(map);
            PlaceVillage(map);
            // 广场两侧预置真实兵营类型，不能仅放置外观相似的民居或教堂。
            map.facilities.Add(new InitialFacility { id = "plaza-melee-barracks", definitionId = "barracks.melee", x = 45, y = 24 });
            map.facilities.Add(new InitialFacility { id = "plaza-ranged-barracks", definitionId = "barracks.ranged", x = 49, y = 24 });
            PlantTrees(map);
            map.markers.Add(new MapMarker { id = "hero", name = "主堡南广场", kind = MarkerKind.HeroSpawn, x = -0.5f, y = -3 });
            map.markers.Add(new MapMarker { id = "west", name = "西侧破口外", kind = MarkerKind.EnemyEntry, x = -45.5f, y = -4 });
            return map;
        }

        private static void BuildOuterWalls(MapLayout map)
        {
            Node(map, "outer-nw", 16.5f, 47.5f);
            Node(map, "outer-ne", 79.5f, 47.5f);
            Node(map, "outer-west-shoulder", 16.5f, 40.5f);
            Node(map, "outer-west-step", 10.5f, 40.5f);
            Node(map, "outer-west-breach", 10.5f, 25.5f);
            Node(map, "outer-sw", 24.5f, 11.5f);
            Node(map, "barbican-nw", 40.5f, 11.5f);
            Node(map, "barbican-ne", 61.5f, 11.5f);
            Node(map, "outer-se", 85.5f, 11.5f);
            Node(map, "outer-east-breach-low", 85.5f, 24.5f);
            Node(map, "outer-east-breach-high", 85.5f, 33.5f);
            Node(map, "outer-east-step", 85.5f, 40.5f);
            Node(map, "outer-east-shoulder", 79.5f, 40.5f);
            Node(map, "barbican-sw", 40.5f, 5.5f);
            Node(map, "barbican-se", 61.5f, 5.5f);
            Edge(map, "outer-north", "outer-nw", "outer-ne", 26, 10, 6);
            Edge(map, "outer-north-west", "outer-nw", "outer-west-shoulder");
            Edge(map, "outer-west-shoulder-edge", "outer-west-shoulder", "outer-west-step");
            Edge(map, "outer-west", "outer-west-step", "outer-west-breach");
            // 参考图的两处破口不保留隐形墙，瓦砾按独立障碍布置。
            Edge(map, "outer-south-west", "outer-sw", "barbican-nw");
            Edge(map, "outer-south-gate", "barbican-nw", "barbican-ne", 7, 8, 4);
            Edge(map, "outer-south-east", "barbican-ne", "outer-se");
            Edge(map, "outer-east-low", "outer-se", "outer-east-breach-low");
            Edge(map, "outer-east-high", "outer-east-breach-high", "outer-east-step");
            Edge(map, "outer-east-shoulder-edge", "outer-east-step", "outer-east-shoulder");
            Edge(map, "outer-north-east", "outer-east-shoulder", "outer-ne");
            Edge(map, "barbican-west", "barbican-nw", "barbican-sw");
            Edge(map, "barbican-east", "barbican-ne", "barbican-se");
            Edge(map, "barbican-south", "barbican-sw", "barbican-se", 7, 8, 4);
        }

        private static void BuildInnerWalls(MapLayout map)
        {
            Node(map, "inner-nw", 27.5f, 38.5f);
            Node(map, "inner-ne", 69.5f, 38.5f);
            Node(map, "inner-sw", 27.5f, 20.5f);
            Node(map, "inner-se", 69.5f, 20.5f);
            Edge(map, "inner-north", "inner-nw", "inner-ne", 17, 8, 4);
            Edge(map, "inner-south", "inner-sw", "inner-se", 17, 8, 4);
            Edge(map, "inner-west", "inner-sw", "inner-nw", 7, 6, 2);
            Edge(map, "inner-east", "inner-se", "inner-ne", 7, 6, 2);
        }

        private static void BuildKeep(MapLayout map)
        {
            Node(map, "keep-nw", 41.5f, 34.5f);
            Node(map, "keep-ne", 55.5f, 34.5f);
            Node(map, "keep-sw", 41.5f, 27.5f);
            Node(map, "keep-se", 55.5f, 27.5f);
            Edge(map, "keep-north", "keep-nw", "keep-ne");
            Edge(map, "keep-west", "keep-nw", "keep-sw");
            Edge(map, "keep-east", "keep-ne", "keep-se");
            Edge(map, "keep-south", "keep-sw", "keep-se", 4, 6, 4);
            Facility(map, "keep", "hall", 960, 470);
        }

        private static void PlaceVillage(MapLayout map)
        {
            Facility(map, "north-west-house", "house", 480, 235);
            Facility(map, "north-church", "church", 660, 195);
            Facility(map, "north-east-house", "house", 1310, 215);
            Facility(map, "north-well", "well", 1180, 180);
            Facility(map, "west-church", "church", 400, 415);
            Facility(map, "west-field", "farm", 450, 345);
            Facility(map, "west-market", "market", 425, 490);
            Facility(map, "west-cottage", "house", 360, 580);
            Facility(map, "inner-nw-house", "church", 690, 405);
            Facility(map, "inner-sw-house", "church", 675, 565);
            Facility(map, "inner-ne-house", "church", 1255, 415);
            Facility(map, "inner-se-house", "house", 1220, 560);
            Facility(map, "east-church", "church", 1480, 320);
            Facility(map, "east-house", "house", 1490, 420);
            Facility(map, "east-market", "market", 1495, 495);
            Facility(map, "east-well", "well", 1530, 570);
            Facility(map, "east-foundry", "foundry", 1520, 650);
            Facility(map, "south-west-foundry", "foundry", 650, 775);
            Facility(map, "south-east-house", "church", 1280, 765);
            Facility(map, "south-east-field", "farm", 1490, 775);
            Facility(map, "south-east-cottage", "house", 1570, 745);
            // 西南破墙沿截图中的斜线排列；东侧破墙沿纵向排列。
            Facility(map, "west-rubble-1", "rubble", 230, 605);
            Facility(map, "west-rubble-2", "rubble", 285, 655);
            Facility(map, "west-rubble-3", "rubble", 335, 705);
            Facility(map, "west-rubble-4", "rubble", 390, 760);
            Facility(map, "west-rubble-5", "rubble", 445, 810);
            Facility(map, "east-rubble-1", "rubble", 1720, 470);
            Facility(map, "east-rubble-2", "rubble", 1730, 500);
            Facility(map, "east-rubble-3", "rubble", 1720, 560);
        }

        private static void PlantTrees(MapLayout map)
        {
            // 明确列出截图中的散植位置，避免随机森林改变构图。
            int[,] points =
            {
                {385,185},{375,290},{345,410},{545,200},{560,150},{590,160},
                {710,170},{790,155},{1120,155},{1260,150},{1370,170},{1460,150},{1530,180},
                {610,320},{605,375},{740,325},{785,330},{1130,330},{1195,325},{1300,330},
                {1450,340},{1580,455},{1395,390},{1305,485},{665,480},{755,575},{600,535},
                {450,550},{465,600},{1400,570},{1450,600},{1560,590},{1600,680},
                {745,705},{800,725},{870,720},{1120,720},{1160,745},{1350,740},{1400,725},
                {1470,720},{1515,700},{1630,760},{550,880},{620,930},{680,950},{730,940},
                {1310,940},{1370,930},{1450,940},{1560,900},{1630,940},{1770,900},
                {150,300},{110,390},{80,480},{70,600},{1800,270},{1830,180},{1880,110},
                {1800,730},{1870,820},{400,980},{480,950},{750,1030},{1320,1030}
            };
            for (int i = 0; i < points.GetLength(0); i++)
            {
                int x = Mathf.Clamp(Mathf.RoundToInt(points[i, 0] / 20f), 0, map.columns - 1);
                int y = Mathf.Clamp(Mathf.RoundToInt((1080 - points[i, 1]) / 20f), 0, map.rows - 1);
                bool occupied = map.facilities.Exists(item => Math.Abs(item.x - x) <= 2 && Math.Abs(item.y - y) <= 2);
                bool nearWall = false;
                foreach (var edge in map.wallEdges)
                {
                    var a = map.wallNodes.Find(node => node.id == edge.from);
                    var b = map.wallNodes.Find(node => node.id == edge.to);
                    if (SegmentGeometry.DistanceToSegment(map.originX + x + 0.5f, map.originY + y + 0.5f, a.x, a.y, b.x, b.y) < 1.2f)
                        nearWall = true;
                }
                if (!occupied && !nearWall)
                    map.terrain[y * map.columns + x] = TerrainKind.Tree;
            }
        }

        private static void Facility(MapLayout map, string id, string definition, int pixelX, int footPixelY)
        {
            map.facilities.Add(new InitialFacility
            {
                id = id, definitionId = definition,
                x = Mathf.RoundToInt(pixelX / 20f - 1),
                y = Mathf.RoundToInt((1080 - footPixelY) / 20f - 0.5f)
            });
        }

        private static void Node(MapLayout map, string id, float x, float y)
        {
            map.wallNodes.Add(new WallNode { id = id, x = x + map.originX, y = y + map.originY });
        }

        private static void Edge(MapLayout map, string id, string from, string to,
            int gateStart = -1, int gateWidth = 0, int openingWidth = 0)
        {
            var edge = new WallEdge { id = id, from = from, to = to };
            if (gateStart >= 0)
            {
                edge.gates.Add(new WallGate
                {
                    id = id + "-gate", startTile = gateStart,
                    widthTiles = gateWidth, openingTiles = openingWidth
                });
            }
            map.wallEdges.Add(edge);
        }
    }
}



