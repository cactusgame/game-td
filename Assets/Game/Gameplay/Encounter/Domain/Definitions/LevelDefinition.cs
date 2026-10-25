using System;
using System.Collections.Generic;

namespace Game.Encounter.Domain.Definitions
{
    /// <summary>已校验、隔离创作副本的只读关卡定义；不依赖地图、Unity 或战斗实现。</summary>
    public sealed class LevelDefinition
    {
        private readonly LevelLayout _layout;
        public int WaveCount => _layout.waves.Count;
        public int InitialGold => _layout.initialGold;
        public LevelLayout CreateWorkingCopy() => _layout.Copy();

        public LevelDefinition(LevelLayout layout, IEnumerable<string> entryIds, IEnumerable<string> enemyIds)
        {
            var errors = Validate(layout, entryIds, enemyIds);
            if (errors.Count > 0)
                throw new ArgumentException(string.Join("\n", errors));
            _layout = layout.Copy();
        }

        public static List<string> Validate(LevelLayout layout, IEnumerable<string> entryIds, IEnumerable<string> enemyIds)
        {
            var errors = new List<string>();
            if (layout == null) { errors.Add("缺少关卡配置"); return errors; }
            var entries = new HashSet<string>(entryIds);
            var enemies = new HashSet<string>(enemyIds);
            if (string.IsNullOrWhiteSpace(layout.id)) errors.Add("关卡 ID 不能为空");
            if (layout.initialGold < 0) errors.Add("初始金币不能为负数");
            ValidateGold(layout.goldOverrides, enemies, "关卡", errors);
            if (layout.waves == null || layout.waves.Count == 0 || layout.waves.Count > 1000)
            { errors.Add("关卡需要 1～1000 个波次"); return errors; }
            var ids = new HashSet<string>();
            long total = 0;
            for (int w = 0; w < layout.waves.Count; w++)
            {
                var wave = layout.waves[w];
                string label = $"第 {w + 1} 波";
                if (wave == null) { errors.Add(label + "为空"); continue; }
                if (string.IsNullOrWhiteSpace(wave.id) || !ids.Add(wave.id)) errors.Add(label + " ID 为空或重复");
                if (!Enum.IsDefined(typeof(WaveStartMode), wave.startMode) || !IsTime(wave.waitSeconds)) errors.Add(label + "开始方式或等待时间无效");
                if (wave.completionGold < 0 || wave.earlyStartGold < 0) errors.Add(label + "奖励不能为负数");
                ValidateGold(wave.goldOverrides, enemies, label, errors);
                if (wave.groups == null || wave.groups.Count == 0) { errors.Add(label + "需要至少一个出怪组"); continue; }
                foreach (var group in wave.groups)
                {
                    if (group == null) { errors.Add(label + "有空出怪组"); continue; }
                    if (string.IsNullOrWhiteSpace(group.id) || !ids.Add(group.id)) errors.Add(label + "出怪组 ID 为空或重复");
                    if (group.entryId == null || !entries.Contains(group.entryId)) errors.Add(label + "引用的出生口不存在：" + group.entryId);
                    if (group.enemyId == null || !enemies.Contains(group.enemyId)) errors.Add(label + "引用的怪物不存在：" + group.enemyId);
                    if (group.count <= 0 || group.count > 100000) errors.Add(label + "出怪数量应为 1～100000");
                    if (!IsTime(group.delaySeconds) || !IsTime(group.intervalSeconds) || !IsTime(group.EndSeconds)) errors.Add(label + "出怪时间必须是有限非负数");
                    total += Math.Max(0, group.count);
                }
            }
            if (total > 100000) errors.Add("整关怪物数量不能超过 100000");
            return errors;
        }

        private static bool IsTime(double seconds) => seconds >= 0 && !double.IsNaN(seconds) && !double.IsInfinity(seconds);

        private static void ValidateGold(List<EnemyGoldOverride> overrides, HashSet<string> enemies, string label, List<string> errors)
        {
            if (overrides == null) { errors.Add(label + "缺少金币覆盖集合"); return; }
            var ids = new HashSet<string>();
            foreach (var item in overrides)
                if (item == null || item.enemyId == null || !enemies.Contains(item.enemyId) || !ids.Add(item.enemyId) || item.gold < 0)
                    errors.Add(label + "金币覆盖包含未知怪物、重复项或负数");
        }

        public int ResolveKillGold(int waveIndex, string enemyId, int defaultGold, out string source)
        {
            var item = _layout.waves[waveIndex].goldOverrides.Find(value => value.enemyId == enemyId);
            if (item != null) { source = "波次覆盖"; return item.gold; }
            item = _layout.goldOverrides.Find(value => value.enemyId == enemyId);
            if (item != null) { source = "关卡覆盖"; return item.gold; }
            source = "怪物默认";
            return defaultGold;
        }
    }
}
