using System;
using System.Collections.Generic;
using Game.MapEngine.Domain;

namespace Game.Maps.Domain
{
    /// <summary>
    /// 地图创作数据。编辑会话独占可变工作副本；MapDefinition 与运行会话均深复制，禁止共享修改。
    /// 预放内容只有初始描述，不包含战斗中的血量、金币或 ECS Entity。
    /// </summary>
    [Serializable]
    public sealed class MapLayout
    {
        public int schemaVersion = 1;
        public string id = "new-map";
        public int columns = 24;
        public int rows = 16;
        public float cellWidth = 1;
        public float cellHeight = 1;
        public float originX = -12;
        public float originY = -8;
        public TerrainKind[] terrain = new TerrainKind[24 * 16];
        // 兼容旧地图：terrain 保留树石的阻挡语义，此可选数组记录障碍下方的地表。
        public TerrainKind[] groundTerrain;
        public static bool IsTerrainObstacle(TerrainKind kind) => kind == TerrainKind.Tree || kind == TerrainKind.Rock;
        public TerrainKind GroundAt(int index)
        {
            if (!IsTerrainObstacle(terrain[index]))
                return terrain[index];
            return groundTerrain != null && groundTerrain.Length == terrain.Length ? groundTerrain[index] : TerrainKind.Grass;
        }
        public void PaintGround(int index, TerrainKind kind)
        {
            if (groundTerrain == null || groundTerrain.Length != terrain.Length)
            {
                var ground = new TerrainKind[terrain.Length];
                for (int i = 0; i < terrain.Length; i++)
                    ground[i] = GroundAt(i);
                groundTerrain = ground;
            }
            groundTerrain[index] = kind;
            if (!IsTerrainObstacle(terrain[index]))
                terrain[index] = kind;
        }
        public List<MapMarker> markers = new List<MapMarker>();
        public List<InitialFacility> facilities = new List<InitialFacility>();
        public List<WallNode> wallNodes = new List<WallNode>();
        public List<WallEdge> wallEdges = new List<WallEdge>();

        [NonSerialized] private GridMap _geometry;
        public GridMap Geometry
        {
            get
            {
                if (_geometry == null || _geometry.Columns != columns || _geometry.Rows != rows ||
                    _geometry.CellWidth != cellWidth || _geometry.CellHeight != cellHeight ||
                    _geometry.OriginX != originX || _geometry.OriginY != originY)
                    _geometry = new GridMap(columns, rows, cellWidth, cellHeight, originX, originY);
                return _geometry;
            }
        }

        public MapLayout Copy()
        {
            var copy = (MapLayout)MemberwiseClone();
            copy.terrain = terrain == null ? null : (TerrainKind[])terrain.Clone();
            copy.groundTerrain = groundTerrain == null ? null : (TerrainKind[])groundTerrain.Clone();
            copy.markers = markers?.ConvertAll(item => item?.Copy());
            copy.facilities = facilities?.ConvertAll(item => item?.Copy());
            copy.wallNodes = wallNodes?.ConvertAll(item => item?.Copy());
            copy.wallEdges = wallEdges?.ConvertAll(item => item?.Copy());
            return copy;
        }

        public static string NewId() => Guid.NewGuid().ToString("N");
    }

    public enum MarkerKind { HeroSpawn, EnemyEntry }

    [Serializable]
    public sealed class MapMarker
    {
        public string id;
        public string name;
        public MarkerKind kind;
        public float x;
        public float y;
        public MapMarker Copy() => (MapMarker)MemberwiseClone();
    }

    [Serializable]
    public sealed class InitialFacility
    {
        public string id;
        public string definitionId = "tower";
        public int x;
        public int y;
        public string wallNodeId;
        public int Width(FacilityDefinition definition) => definition.width;
        public int Height(FacilityDefinition definition) => definition.height;
        public InitialFacility Copy() => (InitialFacility)MemberwiseClone();
    }

    /// <summary>地图初始节点描述；运行时墙网身份与连接状态由 Fortifications 拥有。</summary>
    [Serializable]
    public sealed class WallNode
    {
        public string id;
        public float x;
        public float y;
        public WallNode Copy() => (WallNode)MemberwiseClone();
    }

    /// <summary>地图初始墙段描述，不是可任意修改的运行墙段实体。</summary>
    [Serializable]
    public sealed class WallEdge
    {
        public string id;
        public string from;
        public string to;
        public List<WallGate> gates = new List<WallGate>();

        public WallEdge Copy()
        {
            var copy = (WallEdge)MemberwiseClone();
            copy.gates = gates?.ConvertAll(item => item?.Copy());
            return copy;
        }
    }

    /// <summary>门框起点和宽度使用沿墙整数 Tile，方向来自所属墙段；中心允许落在半格坐标。</summary>
    [Serializable]
    public sealed class WallGate
    {
        public string id;
        public int startTile = 7;
        public int widthTiles = 4;
        public int openingTiles = 2;
        public bool isOpen;
        public bool isDestroyed;
        public WallGate Copy() => (WallGate)MemberwiseClone();
    }
}
