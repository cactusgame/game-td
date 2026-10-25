using System;
using System.Collections.Generic;
using System.Linq;
using Game.Battle.Domain.Definitions;
using Game.Encounter.Domain.Definitions;
using Game.Maps.Domain;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Game.Tools.MapAuthoring.Editor
{
    /// <summary>关卡创作视图。只提交工作副本，历史、保存和试玩统一由主窗口组织。</summary>
    internal sealed class LevelEditorPanel : VisualElement
    {
        private readonly MapEditSession _session;
        private readonly BattleDefinition _battle;
        private readonly Action _refresh;
        private readonly Action<int> _play;
        private readonly Action<int> _select;
        private readonly int _selected;
        private readonly List<MapMarker> _entries;
        private readonly List<UnitDefinition> _enemies;

        public LevelEditorPanel(MapEditSession session, BattleDefinition battle, int selected,
            Action refresh, Action<int> select, Action<int> play)
        {
            _session = session;
            _battle = battle;
            _refresh = refresh;
            _play = play;
            _select = select;
            _selected = selected;
            _entries = session.Document.markers.FindAll(item => item.kind == MarkerKind.EnemyEntry);
            _enemies = new List<UnitDefinition> { battle.Goblin, battle.Runner };
            AddToClassList("level-editor");
            Build();
        }

        private void Change(Action<LevelLayout> change)
        {
            var draft = _session.Level;
            change(draft);
            _session.CommitLevel(draft);
            _refresh();
        }

        private void Build()
        {
            var level = _session.Level;
            if (level == null)
            {
                Add(StyledLabel("当前为地图文档。创建关卡后，地图和波次会统一保存为关卡文件。", "hint"));
                Add(new Button(() =>
                {
                    var draft = new LevelLayout { initialGold = 1000 };
                    var wave = new WaveLayout { name = "第一波", waitSeconds = 5 };
                    foreach (var entry in _entries)
                        wave.groups.Add(new SpawnGroupLayout { entryId = entry.id, enemyId = _battle.Goblin.Id });
                    draft.waves.Add(wave);
                    _session.CommitLevel(draft);
                    _refresh();
                }) { text = "将当前地图升级为关卡", name = "createLevel" });
                return;
            }

            var rail = new ScrollView();
            rail.AddToClassList("wave-rail");
            Add(rail);
            Heading(rail, "关卡设置");
            Text(rail, "关卡 ID", level.id, value => Change(d => d.id = value));
            Text(rail, "名称", level.name, value => Change(d => d.name = value));
            Integer(rail, "初始金币", level.initialGold, value => Change(d => d.initialGold = value));
            Heading(rail, "关卡击杀金币");
            GoldOverrides(rail, level.goldOverrides, -1);
            Heading(rail, "波次列表");
            for (int i = 0; i < level.waves.Count; i++)
            {
                int index = i;
                var button = new Button(() => _select(index)) { text = $"{i + 1:D2}  {level.waves[i].name}" };
                button.EnableInClassList("wave-selected", index == _selected);
                rail.Add(button);
            }
            rail.Add(new Button(() =>
            {
                Change(d => d.waves.Add(new WaveLayout { name = $"第 {d.waves.Count + 1} 波" }));
                _select(_session.Level.waves.Count - 1);
            }) { text = "＋ 新增波次", name = "addWave" });
            var content = new ScrollView();
            content.AddToClassList("wave-content");
            Add(content);
            if (level.waves.Count == 0) { Heading(content, "请新增波次"); return; }
            int w = Math.Min(Math.Max(_selected, 0), level.waves.Count - 1);
            var wave = level.waves[w];
            Heading(content, $"第 {w + 1} 波 · {wave.name}");
            var actions = Row(content);
            actions.Add(new Button(() => _play(w)) { text = "从本波试玩", name = "playSelectedWave" });
            actions.Add(new Button(() =>
            {
                Change(d =>
                {
                    var copy = d.waves[w].Copy();
                    copy.id = Guid.NewGuid().ToString("N");
                    copy.name += "（副本）";
                    foreach (var group in copy.groups) group.id = Guid.NewGuid().ToString("N");
                    d.waves.Insert(w + 1, copy);
                });
                _select(w + 1);
            }) { text = "复制本波", name = "duplicateWave" });
            var up = new Button(() => MoveWave(w, w - 1)) { text = "上移" };
            up.SetEnabled(w > 0); actions.Add(up);
            var down = new Button(() => MoveWave(w, w + 1)) { text = "下移" };
            down.SetEnabled(w + 1 < level.waves.Count); actions.Add(down);
            actions.Add(new Button(() => { Change(d => d.waves.RemoveAt(w)); _select(Math.Max(0, w - 1)); }) { text = "删除本波" });

            Text(content, "波次名称", wave.name, value => Change(d => d.waves[w].name = value));
            var modes = new List<string> { "玩家手动开始", "上一波开始后计时", "上一波完成后计时" };
            var mode = new DropdownField("开始方式", modes, (int)wave.startMode);
            mode.RegisterValueChangedCallback(evt => Change(d => d.waves[w].startMode = (WaveStartMode)mode.index));
            content.Add(mode);
            Number(content, w == 0 ? "开局等待（秒）" : "等待（秒）", wave.waitSeconds, value => Change(d => d.waves[w].waitSeconds = value));
            Integer(content, "完成奖励金币", wave.completionGold, value => Change(d => d.waves[w].completionGold = value));
            var early = new Toggle("允许上一波未清完时手动开波") { value = wave.allowEarlyStart };
            early.RegisterValueChangedCallback(evt => Change(d => d.waves[w].allowEarlyStart = evt.newValue));
            content.Add(early);
            Integer(content, "提前开波奖励", wave.earlyStartGold, value => Change(d => d.waves[w].earlyStartGold = value));
            Heading(content, "本波击杀金币");
            GoldOverrides(content, wave.goldOverrides, w);
            Heading(content, "出怪组");
            content.Add(StyledLabel("各组独立计时，可在同一出生口重叠。延迟相对于本波开始；间隔 0 表示同时出生。", "hint"));
            for (int g = 0; g < wave.groups.Count; g++) BuildGroup(content, w, g, wave.groups[g]);
            var addGroup = new Button(() => Change(d => d.waves[w].groups.Add(new SpawnGroupLayout
            { entryId = _entries[0].id, enemyId = _enemies[0].Id }))) { text = "＋ 添加出怪组", name = "addSpawnGroup" };
            addGroup.SetEnabled(_entries.Count > 0);
            content.Add(addGroup);
            if (_entries.Count == 0) content.Add(new HelpBox("请先在地图中放置敌人入口。", HelpBoxMessageType.Warning));
            BuildTimelineAndStatistics(content, level, w);
        }

        private void MoveWave(int from, int to)
        {
            Change(d => { var wave = d.waves[from]; d.waves.RemoveAt(from); d.waves.Insert(to, wave); });
            _select(to);
        }

        private void GoldOverrides(VisualElement host, List<EnemyGoldOverride> values, int waveIndex)
        {
            foreach (var enemy in _enemies)
            {
                var item = values.Find(value => value.enemyId == enemy.Id);
                var level = _session.Level;
                var inherited = level.goldOverrides.Find(value => value.enemyId == enemy.Id);
                int gold = item?.gold ?? (waveIndex >= 0 ? inherited?.gold : null) ?? enemy.KillGold;
                var row = Row(host);
                var toggle = new Toggle(enemy.Id + " 覆盖") { value = item != null };
                toggle.RegisterValueChangedCallback(evt => Change(d =>
                {
                    var target = waveIndex < 0 ? d.goldOverrides : d.waves[waveIndex].goldOverrides;
                    target.RemoveAll(value => value.enemyId == enemy.Id);
                    if (evt.newValue) target.Add(new EnemyGoldOverride { enemyId = enemy.Id, gold = gold });
                }));
                row.Add(toggle);
                var amount = new IntegerField { value = gold, isDelayed = true };
                amount.SetEnabled(item != null);
                amount.RegisterValueChangedCallback(evt => Change(d =>
                    (waveIndex < 0 ? d.goldOverrides : d.waves[waveIndex].goldOverrides).Find(value => value.enemyId == enemy.Id).gold = evt.newValue));
                row.Add(amount);
                string source = item != null ? (waveIndex >= 0 ? "波次覆盖" : "关卡覆盖") :
                    waveIndex >= 0 && inherited != null ? "关卡覆盖" : "怪物默认";
                host.Add(StyledLabel($"生效：{gold} 金币/只 · {source}", "hint"));
            }
        }

        private void BuildGroup(VisualElement host, int w, int g, SpawnGroupLayout group)
        {
            var card = new VisualElement(); card.AddToClassList("spawn-card"); host.Add(card);
            var row = Row(card);
            row.Add(new Label($"出怪组 {g + 1}"));
            row.Add(new Button(() => Change(d =>
            {
                var copy = d.waves[w].groups[g].Copy(); copy.id = Guid.NewGuid().ToString("N");
                d.waves[w].groups.Insert(g + 1, copy);
            })) { text = "复制" });
            row.Add(new Button(() => Change(d => d.waves[w].groups.RemoveAt(g))) { text = "删除" });
            var entries = _entries.ConvertAll(item => $"{item.name} [{item.id}]");
            int entryIndex = _entries.FindIndex(item => item.id == group.entryId);
            if (entryIndex < 0) { entries.Add("缺失出生口：" + group.entryId); entryIndex = entries.Count - 1; }
            var entry = new DropdownField("出生口", entries, entryIndex);
            entry.RegisterValueChangedCallback(evt => { if (entry.index < _entries.Count) Change(d => d.waves[w].groups[g].entryId = _entries[entry.index].id); });
            card.Add(entry);
            var enemies = _enemies.ConvertAll(item => item.Id);
            int enemyIndex = enemies.IndexOf(group.enemyId);
            if (enemyIndex < 0) { enemies.Add("缺失怪物：" + group.enemyId); enemyIndex = enemies.Count - 1; }
            var enemy = new DropdownField("怪物", enemies, enemyIndex);
            enemy.RegisterValueChangedCallback(evt => { if (enemy.index < _enemies.Count) Change(d => d.waves[w].groups[g].enemyId = _enemies[enemy.index].Id); });
            card.Add(enemy);
            Integer(card, "数量", group.count, value => Change(d => d.waves[w].groups[g].count = value));
            Number(card, "起始延迟（秒）", group.delaySeconds, value => Change(d => d.waves[w].groups[g].delaySeconds = value));
            Number(card, "出生间隔（秒）", group.intervalSeconds, value => Change(d => d.waves[w].groups[g].intervalSeconds = value));
            Text(card, "备注", group.note, value => Change(d => d.waves[w].groups[g].note = value));
        }

        private void BuildTimelineAndStatistics(VisualElement host, LevelLayout level, int w)
        {
            Heading(host, "出怪时间轴（相对本波开始）");
            var wave = level.waves[w];
            double duration = wave.groups.Count == 0 ? 0 : wave.groups.Max(group => group.EndSeconds);
            if (!double.IsNaN(duration) && !double.IsInfinity(duration) && duration >= 0)
            {
                foreach (var lane in wave.groups.GroupBy(group => group.entryId))
                {
                    var entry = _entries.Find(item => item.id == lane.Key);
                    host.Add(StyledLabel(entry?.name ?? "缺失出生口", "heading"));
                    foreach (var group in lane)
                    {
                        host.Add(new Label($"{group.enemyId} × {group.count}  |  {group.delaySeconds:0.##}s → {group.EndSeconds:0.##}s"));
                        host.Add(new SpawnTimeline(group, duration));
                    }
                }
            }
            Heading(host, "收入与数量统计");
            try
            {
                var definition = Game.Tools.MapPlaytest.Application.Adapters.LevelDefinitionFactory.Create(level, _session.Document, _battle);
                long cumulative = level.initialGold;
                for (int i = 0; i <= w; i++)
                {
                    long kills = 0;
                    foreach (var group in level.waves[i].groups)
                    {
                        var unit = _enemies.Find(item => item.Id == group.enemyId);
                        kills += (long)group.count * definition.ResolveKillGold(i, group.enemyId, unit.KillGold, out _);
                    }
                    cumulative += kills + level.waves[i].completionGold;
                    if (i == w)
                    {
                        host.Add(new Label($"本波 {wave.groups.Sum(group => group.count)} 只 · 出怪持续 {duration:0.##} 秒"));
                        foreach (var type in wave.groups.GroupBy(group => group.enemyId))
                            host.Add(new Label($"{type.Key}：{type.Sum(group => group.count)} 只"));
                        host.Add(new Label($"预计击杀收入 {kills} · 完成奖励 {wave.completionGold} · 合计 {kills + wave.completionGold}"));
                        host.Add(new Label($"截至本波累计可获得 {cumulative} 金币（含初始金币，不含提前奖励及支出）"));
                    }
                }
            }
            catch (ArgumentException error) { host.Add(new HelpBox(error.Message, HelpBoxMessageType.Error)); }
            host.Add(StyledLabel("出怪持续时间不等于战斗时长。胜利需要所有波次清完；主堡被摧毁则失败。", "hint"));
        }

        private static Label StyledLabel(string text, string css)
        {
            var label = new Label(text); label.AddToClassList(css); return label;
        }
        private static void Heading(VisualElement host, string text) => host.Add(StyledLabel(text, "heading"));
        private static VisualElement Row(VisualElement host) { var row = new VisualElement(); row.AddToClassList("level-row"); host.Add(row); return row; }
        private static void Text(VisualElement host, string label, string value, Action<string> change)
        {
            var field = new TextField(label) { value = value ?? "", isDelayed = true };
            field.RegisterValueChangedCallback(evt => change(evt.newValue)); host.Add(field);
        }
        private static void Integer(VisualElement host, string label, int value, Action<int> change)
        {
            var field = new IntegerField(label) { value = value, isDelayed = true };
            field.RegisterValueChangedCallback(evt => change(evt.newValue)); host.Add(field);
        }
        private static void Number(VisualElement host, string label, double value, Action<double> change)
        {
            var field = new DoubleField(label) { value = value, isDelayed = true };
            field.RegisterValueChangedCallback(evt => change(evt.newValue)); host.Add(field);
        }
    }
}
