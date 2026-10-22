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
            return new MapContentCatalog(data.facilities);
        }

        [Serializable]
        private sealed class CatalogDocument
        {
            public int schemaVersion;
            public List<FacilityDefinition> facilities;
        }
    }
}
