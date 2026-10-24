using Game.Construction.Application.Sessions;
using Game.Fortifications.Domain.ValueObjects;
using System;
using Game.Encounter.Domain.Definitions;
using Game.Encounter.Domain.Aggregates.DefenseEncounter;
using System.Collections.Generic;
using Game.Battle.Application.Sessions;
using Game.Battle.Contracts.Ports;
using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.Definitions;
using Game.Maps.Domain;
using Game.MapEngine.Domain;
using Game.Tools.MapPlaytest.Application.Adapters;
using Game.Construction.Domain.Aggregates.BarracksRoster;
using Game.Economy.Domain.Aggregates.ResourceAccount;
using Game.Tools.MapPlaytest.Application.ReadModels;

namespace Game.Tools.MapPlaytest.Application.Sessions
{
    /// <summary>
    /// 独立试玩的应用会话，拥有本局布置与模拟的生命周期；源定义不可变，单位真值仍归模拟。
    /// 输入命令在 Advance 前完整提交，地图与模拟空间同步成功后才公布新版本。
    /// </summary>
    public sealed class MapPlaySession : IDisposable
    {
        private readonly DefenseEncounterDefinition _encounterDefinition;
        private DefenseEncounter _encounter;
        private readonly Game.Construction.Domain.Definitions.ConstructionContentDefinition _construction;
        private readonly List<DefenseStructureSnapshot> _structures = new List<DefenseStructureSnapshot>();
        public IReadOnlyList<DefenseStructureSnapshot> Structures => _structures.AsReadOnly();
        public string Outcome => _encounter?.Outcome ?? "Running";
        public int SpawnedWaves => _encounter?.SpawnedWaves ?? 0;
        private readonly MapDefinition _initialMap;
        private readonly MapContentCatalog _catalog;
        private readonly BattleDefinition _rules;
        private readonly IHeroInput _input;
        private readonly Func<ISpatialBattleSimulation> _createSimulation;
        private ISpatialBattleSimulation _simulation;
        private BattleSession _battle;
        private MapConstructionState _placements;
        private bool _disposed;
        private bool _faulted;
        private GatePassageSession _gates;
        private readonly Dictionary<string, BarracksRoster> _barracks = new Dictionary<string, BarracksRoster>();
        private readonly int _initialGold;
        private ResourceAccount _account;
        public int Gold => _account?.Gold ?? 0;
        private GridMap _enemyNavigation;
        public int GateViewVersion => _gates?.ViewVersion ?? 0;
        public int GetGateHealth(string id) => _gates.Health(id);

        public MapLayout Snapshot => _placements == null ? null : _gates.Project(_placements.Snapshot);
        public GridMap Navigation => _enemyNavigation;
        public int MapVersion => _placements?.Version ?? 0;
        public int Generation { get; private set; }
        public double SimulationTime => _battle?.ElapsedTime ?? 0;
        public int HeroHealth => _battle?.HeroHealth ?? 0;
        public BattleDefinition Definition => _battle?.Definition;
        public IReadOnlyList<UnitSnapshot> Units => _battle?.Units ?? Array.Empty<UnitSnapshot>();

        public MapPlaySession(MapDefinition initialMap, MapContentCatalog catalog, BattleDefinition rules,
            IHeroInput input, Func<ISpatialBattleSimulation> createSimulation, DefenseEncounterDefinition encounterDefinition = null, Game.Construction.Domain.Definitions.ConstructionContentDefinition construction = null, int initialGold = 1000)
        {
            _construction = construction;
            if (initialGold < 0) throw new ArgumentOutOfRangeException(nameof(initialGold));
            _initialGold = initialGold;
            _encounterDefinition = encounterDefinition ?? new DefenseEncounterDefinition();
            _initialMap = initialMap ?? throw new ArgumentNullException(nameof(initialMap));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _createSimulation = createSimulation ?? throw new ArgumentNullException(nameof(createSimulation));
        }

        public void Restart()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MapPlaySession));
            // 验证、创建并初始化候选会话后再替换旧局。任何候选初始化失败均释放候选 World。
            var definition = new MapBattleDefinitionSource(_initialMap, _catalog, _rules).Load();
            if (_construction != null)
            {
                foreach (var training in _construction.Barracks.Values)
                {
                    bool found = false;
                    foreach (var soldier in definition.Soldiers) found |= soldier.Id == training.UnitId;
                    if (!found) throw new InvalidOperationException("兵营引用了未加载的士兵：" + training.UnitId);
                }
            }
            var placements = new MapConstructionState(_initialMap, _catalog);
            var simulation = _createSimulation();
            var encounter = new DefenseEncounter(_encounterDefinition);
            var structures = new List<DefenseStructureSnapshot>();
            BattleSession battle;
            var gates = new GatePassageSession(_catalog);
            GridMap enemyNavigation;
            try
            {
                battle = new BattleSession(new PreparedDefinitionSource(definition), simulation, _input);
                battle.Restart();
                if (!(simulation is IDefenseBattleSimulation defense))
                    throw new InvalidOperationException("防守对局需要支持建筑战斗和波次生成的模拟。");
                defense.SynchronizeStructures(DescribeStructures(placements.Snapshot));
                enemyNavigation = gates.Navigation(placements.Snapshot, false);
                simulation.UpdateNavigationMap(enemyNavigation, definition.Navigation, gates.Navigation(placements.Snapshot, true));
                defense.SpawnEnemies(definition.EnemySpawns, encounter.DueCount(0));
                encounter.CommitWave();
                battle.RefreshSnapshot();
                defense.CopyStructures(structures);
            }
            catch
            {
                simulation?.Dispose();
                throw;
            }
            var previousSimulation = _simulation;
            _simulation = simulation;
            _battle = battle;
            _placements = placements;
            _gates = gates;
            _enemyNavigation = enemyNavigation;
            _faulted = false;
            _encounter = encounter;
            _structures.Clear();
            _structures.AddRange(structures);
            Generation++;
            previousSimulation?.Dispose();
            _account = new ResourceAccount(_initialGold);
            _barracks.Clear();
            UpdateBarracks();
            _battle.RefreshSnapshot();
        }

        public void Advance(float deltaTime)
        {
            RequireActive();
            if (!_encounter.IsRunning)
                return;
            double before = SimulationTime;
            _battle.Advance(deltaTime, SpawnDueWave, () =>
            {
                // 波次时间只读取模拟时钟；第二波在跨过第十秒的边界提交。
                SpawnDueWave();
                UpdateGates(true);
                ReadStructures();
                UpdateBarracks();
                _encounter.Evaluate(_structures.Exists(item => item.IsMainCastle && item.Health <= 0), _battle.AliveMonsters);
                return _encounter.IsRunning;
            });
            if (SimulationTime == before)
                UpdateGates(false);
        }

        private void UpdateGates(bool applyDamage)
        {
            int spatialVersion = _gates.SpatialVersion;
            _gates.Update((IDefenseBattleSimulation)_simulation, _placements.Snapshot, Definition, Units, SimulationTime, applyDamage);
            // 自动开门仅改变表现；只有破门改变敌方通行，不逐帧重建导航图。
            if (spatialVersion != _gates.SpatialVersion)
                _enemyNavigation = _gates.Navigation(_placements.Snapshot, false);
        }

        public bool TryBuild(string definitionId, int x, int y, out string reason)
        {
            RequireActive();
            if (!CanChangeConstruction(out reason))
                return false;
            var candidate = _placements.CreateCandidate();
            int cost = _construction != null && _construction.Barracks.ContainsKey(definitionId)
                ? _construction.Buildings[definitionId].BuildCost : 0;
            if (!_account.CanSpend(cost))
            {
                reason = "金币不足";
                return false;
            }
            if (!candidate.TryBuild(definitionId, x, y, UnitsRemainClear, out reason))
                return false;
            if (!TryPublish(candidate, out reason)) return false;
            _account.TrySpend(cost);
            UpdateBarracks();
            _battle.RefreshSnapshot();
            return true;
        }

        public int BarracksCapacity(string instanceId) => _barracks.TryGetValue(instanceId, out var roster) ? roster.Capacity : 0;
        public int BarracksLevel(string instanceId) => _barracks.TryGetValue(instanceId, out var roster) ? roster.Level : 0;

        public IReadOnlyList<BarracksStatus> GetBarracksStatuses()
        {
            var aliveIds = new HashSet<ulong>();
            foreach (var unit in Units)
                if (unit.Health > 0) aliveIds.Add(unit.Id);
            var result = new List<BarracksStatus>();
            foreach (var roster in _barracks.Values)
            {
                int alive = 0, training = 0, waiting = 0;
                double nextReady = double.PositiveInfinity;
                for (int i = 0; i < roster.Capacity; i++)
                {
                    if (aliveIds.Contains(roster.UnitAt(i))) alive++;
                    else if (roster.IsReady(i, SimulationTime)) waiting++;
                    else if (roster.RemainingTraining(i, SimulationTime) > 0)
                    {
                        training++;
                        nextReady = Math.Min(nextReady, roster.RemainingTraining(i, SimulationTime));
                    }
                }
                bool destroyed = _structures.Exists(s => s.Id == roster.Id && s.Health <= 0);
                result.Add(new BarracksStatus(roster.Id, roster.BuildingId, roster.Level, roster.Capacity,
                    alive, training, waiting, destroyed, training > 0 ? nextReady : 0));
            }
            return result.AsReadOnly();
        }

        public bool TryUpgradeBarracks(string instanceId, out string reason)
        {
            RequireActive();
            if (!CanChangeConstruction(out reason)) return false;
            if (_construction == null || !_barracks.TryGetValue(instanceId, out var roster))
            {
                reason = "该建筑不是兵营";
                return false;
            }
            if (!_placements.Snapshot.facilities.Exists(building => building.id == instanceId))
            {
                reason = "兵营已被拆除";
                return false;
            }
            ReadStructures();
            if (_structures.Exists(s => s.Id == instanceId && s.Health <= 0))
            {
                reason = "兵营已被摧毁";
                return false;
            }
            foreach (var upgrade in _construction.Upgrades.Values)
            {
                if (!roster.CanUpgrade(upgrade)) continue;
                if (!_account.CanSpend(upgrade.Cost))
                {
                    reason = "金币不足";
                    return false;
                }
                // 兵营容量升级即时提交；新增槽位需要训练冷却，不重置已有士兵或补兵进度。
                roster.Upgrade(upgrade, SimulationTime, _construction.Barracks[roster.BuildingId].RetrainSeconds);
                _account.TrySpend(upgrade.Cost);
                reason = null;
                return true;
            }
            reason = "兵营已满级";
            return false;
        }

        /// <summary>对局协调建设编制与战斗部署。阵亡由战斗快照确认，每个槽位只启动一次补兵。</summary>
        private void UpdateBarracks()
        {
            if (_construction == null) return;
            var layout = _placements.Snapshot;
            var retained = new HashSet<string>();
            var alive = new HashSet<ulong>();
            foreach (var unit in Units)
                if (unit.Health > 0) alive.Add(unit.Id);
            var defense = (IDefenseBattleSimulation)_simulation;
            foreach (var building in layout.facilities)
            {
                if (!_construction.Barracks.TryGetValue(building.definitionId, out var rule)) continue;
                retained.Add(building.id);
                if (!_barracks.TryGetValue(building.id, out var roster))
                {
                    roster = new BarracksRoster(building.id, building.definitionId, rule.Capacity);
                    _barracks.Add(building.id, roster);
                }
                if (_structures.Exists(s => s.Id == building.id && s.Health <= 0)) continue;
                UnitDefinition soldier = null;
                foreach (var unit in Definition.Soldiers)
                    if (unit.Id == rule.UnitId) soldier = unit;
                if (soldier == null) throw new InvalidOperationException("兵营引用了未加载的士兵：" + rule.UnitId);
                var size = _catalog.Find(building.definitionId);
                float centerX = layout.originX + (building.x + size.width * 0.5f) * layout.cellWidth;
                float frontY = layout.originY + building.y * layout.cellHeight;
                var exit = new MapPoint(centerX, frontY - soldier.Radius - 0.08f);
                for (int i = 0; i < roster.Capacity; i++)
                {
                    if (roster.UnitAt(i) != 0 && !alive.Contains(roster.UnitAt(i)))
                        roster.ObserveDeath(i, SimulationTime, rule.RetrainSeconds);
                    if (!roster.IsReady(i, SimulationTime)) continue;
                    float spacing = soldier.Radius * 2 + Definition.Navigation.SeparationGap + 0.1f;
                    var rally = new MapPoint(centerX + ((i % 4) - 1.5f) * spacing,
                        frontY - rule.RallyDistance - (i / 4) * spacing);
                    if (defense.TrySpawnSoldier(soldier.Kind, exit, rally, out ulong unitId))
                        roster.Assign(i, unitId);
                }
            }
            var removed = new List<string>();
            foreach (var id in _barracks.Keys)
                if (!retained.Contains(id)) removed.Add(id);
            foreach (var id in removed) _barracks.Remove(id);
        }

        public bool TryRemove(float x, float y, out string reason)
        {
            RequireActive();
            if (!CanChangeConstruction(out reason))
                return false;
            var layout = Snapshot;
            foreach (var facility in layout.facilities)
            {
                var definition = _catalog.Find(facility.definitionId);
                if ((facility.definitionId == "hall" || (_construction != null && _construction.Buildings.TryGetValue(facility.definitionId, out var development) && !development.CanDemolish)) && layout.Geometry.TryWorldToCell(x, y, out var cell) &&
                    cell.X >= facility.x && cell.X < facility.x + definition.width &&
                    cell.Y >= facility.y && cell.Y < facility.y + definition.height)
                {
                    reason = "防守目标不能拆除";
                    return false;
                }
            }
            var candidate = _placements.CreateCandidate();
            if (!candidate.TryRemove(x, y))
            {
                reason = "该位置没有可拆除的建筑或墙体";
                return false;
            }
            return TryPublish(candidate, out reason);
        }

        /// <summary>运行建设入口；只有空间同步并提交后才向调用方返回新门 ID。</summary>
        public bool TryPlaceGate(string edgeId, int startTile, int widthTiles, int openingTiles, bool isOpen,
            out string gateId, out string reason)
        {
            RequireActive();
            gateId = null;
            if (!CanChangeConstruction(out reason))
                return false;
            string candidateId = MapLayout.NewId();
            var gate = new WallGateSnapshot(candidateId, startTile, widthTiles, openingTiles, isOpen);
            var candidate = _placements.CreateCandidate();
            if (!candidate.TryPlaceGate(edgeId, gate, UnitsRemainClear, out reason) || !TryPublish(candidate, out reason))
                return false;
            gateId = candidateId;
            return true;
        }

        public bool TrySetGateOpen(string gateId, bool isOpen, out string reason)
        {
            RequireActive();
            var candidate = _placements.CreateCandidate();
            if (!candidate.TrySetGateOpen(gateId, isOpen, UnitsRemainClear, out reason))
                return false;
            if (candidate.Version == _placements.Version)
                return true;
            return TryPublish(candidate, out reason);
        }

        private bool UnitsRemainClear(GridMap navigation)
        {
            foreach (var unit in _battle.Units)
            {
                float radius = Definition.GetUnit(unit.Kind).Radius;
                if (unit.Health > 0 && !navigation.CanOccupy(unit.X, unit.Y, radius))
                    return false;
            }
            return true;
        }

        private bool TryPublish(MapConstructionState candidate, out string reason)
        {
            var enemyNavigation = _gates.Navigation(candidate.Snapshot, false);
            try
            {
                _simulation.UpdateNavigationMap(enemyNavigation, Definition.Navigation, _gates.Navigation(candidate.Snapshot, true));
                if (_simulation is IDefenseBattleSimulation defense)
                    defense.SynchronizeStructures(DescribeStructures(candidate.Snapshot));
            }
            catch (Exception updateError)
            {
                try
                {
                    _simulation.UpdateNavigationMap(_enemyNavigation, Definition.Navigation, _gates.Navigation(_placements.Snapshot, true));
                    if (_simulation is IDefenseBattleSimulation defense)
                        defense.SynchronizeStructures(DescribeStructures(_placements.Snapshot));
                }
                catch (Exception rollbackError)
                {
                    // 空间无法恢复时禁止继续推进，避免单位使用未知的半更新状态。
                    _faulted = true;
                    throw new AggregateException("试玩空间同步及恢复均失败，请重启试玩。", updateError, rollbackError);
                }
                reason = "空间同步失败，操作未提交：" + updateError.Message;
                return false;
            }
            _placements = candidate;
            _enemyNavigation = enemyNavigation;
            reason = null;
            return true;
        }

        private void SpawnDueWave()
        {
            int count = _encounter.DueCount(SimulationTime);
            if (count == 0)
                return;
            if (_simulation is IDefenseBattleSimulation defense)
                defense.SpawnEnemies(Definition.EnemySpawns, count);
            _encounter.CommitWave();
            _battle.RefreshSnapshot();
        }

        private void ReadStructures()
        {
            if (_simulation is IDefenseBattleSimulation defense)
                defense.CopyStructures(_structures);
        }

        private List<DefenseStructureDefinition> DescribeStructures(MapLayout layout)
        {
            var result = new List<DefenseStructureDefinition>();
            foreach (var instance in layout.facilities)
            {
                var definition = _catalog.Find(instance.definitionId);
                float minX = layout.originX + instance.x * layout.cellWidth;
                float minY = layout.originY + instance.y * layout.cellHeight;
                result.Add(new DefenseStructureDefinition(instance.id, instance.definitionId == "hall",
                    minX, minY, minX + definition.width * layout.cellWidth,
                    minY + definition.height * layout.cellHeight, definition.maxHealth,
                    definition.attack, definition.attackRange, definition.attackInterval));
            }
            return result;
        }

        private bool CanChangeConstruction(out string reason)
        {
            reason = _encounter.IsRunning ? null : "对局已结束，请重试";
            return _encounter.IsRunning;
        }

        private void RequireActive()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MapPlaySession));
            if (_battle == null || _faulted)
                throw new InvalidOperationException("试玩尚未启动或空间状态无效，请重启。");
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _simulation?.Dispose();
            _simulation = null;
        }

        private sealed class PreparedDefinitionSource : IBattleDefinitionSource
        {
            private readonly BattleDefinition _definition;
            public PreparedDefinitionSource(BattleDefinition definition) => _definition = definition;
            public BattleDefinition Load() => _definition;
        }
    }
}
