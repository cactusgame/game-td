using System;
using Game.Maps.Domain;
using UnityEngine;

namespace Game.Infrastructure.Maps.Serialization
{
    /// <summary>Unity JSON 适配；不在领域模型内读写文件。源文档允许保存校验未通过的草稿。</summary>
    public static class MapJson
    {
        public static MapLayout ReadDocument(string json)
        {
            var layout = JsonUtility.FromJson<MapLayout>(json);
            if (layout == null || layout.schemaVersion != 1 || layout.terrain == null || layout.markers == null || layout.facilities == null || layout.wallNodes == null || layout.wallEdges == null)
                throw new FormatException("地图文档格式无效，要求 schemaVersion 1 及完整内容集合。");
            var geometry = layout.Geometry;
            if (layout.terrain.Length != geometry.CellCount)
                throw new FormatException("地形数量与地图尺寸不匹配。");
            if (Array.Exists(layout.terrain, value => !Enum.IsDefined(typeof(Game.MapEngine.Domain.TerrainKind), value)) ||
                layout.markers.Exists(item => item == null) || layout.facilities.Exists(item => item == null) ||
                layout.wallNodes.Exists(item => item == null) || layout.wallEdges.Exists(item => item == null))
                throw new FormatException("地图包含未知地形或空对象，请修正 JSON 后重新打开。");
            return layout;
        }

        public static string WriteDocument(MapLayout layout) => JsonUtility.ToJson(layout, true);

        public static MapDefinition LoadDefinition(string json, MapContentCatalog catalog)
        {
            return new MapDefinition(ReadDocument(json), catalog);
        }
    }
}
