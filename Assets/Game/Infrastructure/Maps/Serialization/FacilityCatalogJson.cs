using Game.Fortifications.Domain.Definitions;
using System;
using System.Collections.Generic;
using Game.Maps.Domain;
using UnityEngine;

namespace Game.Infrastructure.Maps.Serialization
{
    /// <summary>把外部设施 JSON 转成只读空间内容目录；素材资产只保存引用，不执行配置加载。</summary>
    public static class FacilityCatalogJson
    {
        public static MapContentCatalog Read(string json)
        {
            var data = JsonUtility.FromJson<CatalogDocument>(json);
            if (data == null || data.schemaVersion != 1 || data.facilities == null)
                throw new FormatException("Invalid map facility catalog.");
            var wall = data.wall == null ? WallDefinition.Default : new WallDefinition(data.wall.width, data.wall.height, data.wall.junctionRadius, gateHealth: data.wall.gateHealth, gateAttackInterval: data.wall.gateAttackInterval);
            return new MapContentCatalog(data.facilities, wall);
        }

        [Serializable]
        private sealed class WallDocument
        {
            public float width;
            public float height;
            public float junctionRadius;
            public int gateHealth = 100;
            public float gateAttackInterval = 1;
        }

        [Serializable]
        private sealed class CatalogDocument
        {
            public int schemaVersion;
            public List<FacilityDefinition> facilities;
            public WallDocument wall;
        }
    }
}
