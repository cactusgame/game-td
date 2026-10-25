using System;
using System.Collections.Generic;
using Game.Battle.Domain.Definitions;
using Game.Encounter.Domain.Definitions;
using Game.Maps.Domain;

namespace Game.Tools.MapPlaytest.Application.Adapters
{
    /// <summary>关卡与地图、战斗内容的应用边界。以稳定内容 ID 校验引用，不让领域依赖地图实现。</summary>
    public static class LevelDefinitionFactory
    {
        public static LevelDefinition Create(LevelLayout level, MapLayout map, BattleDefinition battle)
        {
            var entries = new List<string>();
            foreach (var marker in map.markers)
                if (marker.kind == MarkerKind.EnemyEntry) entries.Add(marker.id);
            var definition = new LevelDefinition(level, entries, new[] { battle.Goblin.Id, battle.Runner.Id });
            long maximumGold = level.initialGold;
            for (int w = 0; w < level.waves.Count; w++)
            {
                maximumGold += level.waves[w].completionGold + (long)level.waves[w].earlyStartGold;
                foreach (var group in level.waves[w].groups)
                {
                    var unit = group.enemyId == battle.Goblin.Id ? battle.Goblin : battle.Runner;
                    maximumGold += (long)group.count * definition.ResolveKillGold(w, group.enemyId, unit.KillGold, out _);
                }
            }
            if (maximumGold > int.MaxValue) throw new ArgumentException("关卡最大金币收入超过账户上限");
            return definition;
        }
    }
}
