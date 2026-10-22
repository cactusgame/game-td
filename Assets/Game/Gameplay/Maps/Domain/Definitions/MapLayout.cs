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
        public InitialFacility Copy() => (InitialFacility)MemberwiseClone();
    }

    [Serializable]
    public sealed class WallNode
    {
        public string id;
        public float x;
        public float y;
        public WallNode Copy() => (WallNode)MemberwiseClone();
    }

    [Serializable]
    public sealed class WallEdge
    {
        public string id;
        public string from;
        public string to;
        public WallEdge Copy() => (WallEdge)MemberwiseClone();
    }
}
