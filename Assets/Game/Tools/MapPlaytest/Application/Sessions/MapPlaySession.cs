using System;
using System.Collections.Generic;
using Game.Battle.Application.Sessions;
using Game.Battle.Contracts.Ports;
using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.Definitions;
using Game.Maps.Domain;
using Game.MapEngine.Domain;
using Game.Tools.MapPlaytest.Application.Adapters;

namespace Game.Tools.MapPlaytest.Application.Sessions
{
    /// <summary>
    /// 独立试玩的应用会话，拥有本局布置与模拟的生命周期；源定义不可变，单位真值仍归模拟。
    /// 输入命令在 Advance 前完整提交，地图与模拟空间同步成功后才公布新版本。
    /// </summary>
    public sealed class MapPlaySession : IDisposable
    {
        private readonly MapDefinition _initialMap;
        private readonly MapContentCatalog _catalog;
        private readonly BattleDefinition _rules;
        private readonly IHeroInput _input;
        private readonly Func<ISpatialBattleSimulation> _createSimulation;
        private ISpatialBattleSimulation _simulation;
        private BattleSession _battle;
        private MapPlacementState _placements;
        private bool _disposed;
        private bool _faulted;

        public MapLayout Snapshot => _placements?.Snapshot;
        public GridMap Navigation => _placements?.Navigation;
        public int MapVersion => _placements?.Version ?? 0;
        public int Generation { get; private set; }
        public double SimulationTime => _battle?.ElapsedTime ?? 0;
        public int HeroHealth => _battle?.HeroHealth ?? 0;
        public BattleDefinition Definition => _battle?.Definition;
        public IReadOnlyList<UnitSnapshot> Units => _battle?.Units ?? Array.Empty<UnitSnapshot>();

        public MapPlaySession(MapDefinition initialMap, MapContentCatalog catalog, BattleDefinition rules,
            IHeroInput input, Func<ISpatialBattleSimulation> createSimulation)
        {
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
            var placements = new MapPlacementState(_initialMap, _catalog);
            var simulation = _createSimulation();
            BattleSession battle;
            try
            {
                battle = new BattleSession(new PreparedDefinitionSource(definition), simulation, _input);
                battle.Restart();
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
            _faulted = false;
            Generation++;
            previousSimulation?.Dispose();
        }

        public void Advance(float deltaTime)
        {
            RequireActive();
            _battle.Advance(deltaTime);
        }

        public bool TryBuild(string definitionId, int x, int y, out string reason)
        {
            RequireActive();
            var candidate = _placements.CreateCandidate();
            if (!candidate.TryBuild(definitionId, x, y, UnitsRemainClear, out reason))
                return false;
            return TryPublish(candidate, out reason);
        }

        public bool TryRemove(float x, float y, out string reason)
        {
            RequireActive();
            var candidate = _placements.CreateCandidate();
            if (!candidate.TryRemove(x, y))
            {
                reason = "该位置没有可拆除的建筑或墙体";
                return false;
            }
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

        private bool TryPublish(MapPlacementState candidate, out string reason)
        {
            try
            {
                _simulation.UpdateNavigationMap(candidate.Navigation, Definition.Navigation);
            }
            catch (Exception updateError)
            {
                try
                {
                    _simulation.UpdateNavigationMap(_placements.Navigation, Definition.Navigation);
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
            reason = null;
            return true;
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
