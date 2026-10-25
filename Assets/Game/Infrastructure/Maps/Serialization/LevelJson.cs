using System;
using Game.Encounter.Domain.Definitions;
using Game.Maps.Domain;
using UnityEngine;

namespace Game.Infrastructure.Maps.Serialization
{
    /// <summary>关卡包持有地图和关卡规则，单文件原子保存，避免两份资源修订不匹配。</summary>
    public static class LevelJson
    {
        [Serializable]
        public sealed class Document
        {
            public string documentType = "level";
            public int schemaVersion = 1;
            public MapLayout map;
            public LevelLayout level;
        }

        public static bool IsLevel(string json) => JsonUtility.FromJson<Header>(json)?.documentType == "level";
        public static Document ReadDocument(string json)
        {
            var document = JsonUtility.FromJson<Document>(json);
            if (document == null || document.documentType != "level" || document.schemaVersion != 1 || document.map == null || document.level == null)
                throw new FormatException("关卡文档格式无效");
            if (document.level.waves == null || document.level.goldOverrides == null ||
                document.level.goldOverrides.Exists(item => item == null) ||
                document.level.waves.Exists(wave => wave == null || wave.groups == null || wave.goldOverrides == null ||
                    wave.groups.Exists(group => group == null) || wave.goldOverrides.Exists(item => item == null) ||
                    !Enum.IsDefined(typeof(WaveStartMode), wave.startMode)))
                throw new FormatException("关卡文档存在缺失集合、空对象或未知开始方式");
            document.map = MapJson.ReadDocument(JsonUtility.ToJson(document.map));
            return document;
        }
        public static string WriteDocument(MapLayout map, LevelLayout level) => JsonUtility.ToJson(new Document { map = map, level = level }, true);
        [Serializable]
        private sealed class Header { public string documentType; }
    }
}
