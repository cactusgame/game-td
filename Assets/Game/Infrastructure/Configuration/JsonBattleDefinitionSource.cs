using System;
using Game.Battle.Contracts;
using Game.Battle.Domain;
using UnityEngine;

namespace Game.Infrastructure.Configuration
{
    /// <summary>基础设施适配：从场景引用的 JSON TextAsset 读取数据，转换为领域层的只读配置。</summary>
    public sealed class JsonBattleDefinitionSource : IBattleDefinitionSource
    {
        private readonly TextAsset _asset;
        public JsonBattleDefinitionSource(TextAsset asset) { _asset = asset; }
        public BattleDefinition Load() => Parse(_asset != null ? _asset.text : throw new InvalidOperationException("Battle JSON is missing."));

        // JSON DTO 只负责序列化格式；Build/构造函数负责建立有约束的领域定义。
        // 先验证再初始化模拟，避免运行时单位共享并修改配置对象。
        public static BattleDefinition Parse(string json)
        {
            var data = JsonUtility.FromJson<BattleJson>(json);
            if (data == null || data.schemaVersion != 1 || data.arena == null || data.hero == null
                || data.goblin == null || data.runner == null || data.spawn == null)
                throw new FormatException("Battle JSON must use schemaVersion 1 and contain arena, hero, goblin, runner and spawn.");
            return new BattleDefinition(data.hero.Build(UnitKind.Hero), data.goblin.Build(UnitKind.Goblin),
                data.runner.Build(UnitKind.Runner), data.spawn.goblins, data.spawn.runners,
                data.arena.width, data.arena.height, data.fixedStep, data.seed);
        }

        // 这些可变字段供 JsonUtility 反序列化使用，不是场上单位的可变运行状态。
        [Serializable] private sealed class BattleJson
        {
            public int schemaVersion;
            public uint seed;
            public float fixedStep;
            public ArenaJson arena;
            public SpawnJson spawn;
            public UnitJson hero, goblin, runner;
        }
        [Serializable] private sealed class ArenaJson { public float width, height; }
        [Serializable] private sealed class SpawnJson { public int goblins, runners; }
        [Serializable] private sealed class UnitJson
        {
            public int maxHealth, attack, defense;
            public float speed, radius, visualHeight;
            public UnitDefinition Build(UnitKind kind) => new UnitDefinition(kind, maxHealth, attack, defense, speed, radius, visualHeight);
        }
    }
}
