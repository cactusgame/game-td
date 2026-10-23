using System;
using System.Collections.Generic;
using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.Definitions;
using Game.Battle.Domain.ValueObjects;
using Game.Battle.Unity.Assets;
using Game.Presentation.Views;
using UnityEngine;

namespace Game.Battle.Unity.Views
{
    /// <summary>单位与战斗的快照表现桥接。终局只推进收尾动画，不推进战斗。</summary>
    public sealed class BattleUnitPresentation
    {
        private readonly Transform _parent;
        private readonly UnitVisualCatalog _catalog;
        private readonly BattlePresentationSettings _settings;
        private readonly Dictionary<UnitKind, UnitAnimationPlayer> _players = new Dictionary<UnitKind, UnitAnimationPlayer>();
        private readonly Dictionary<ulong, UnitView> _views = new Dictionary<ulong, UnitView>();
        private readonly HashSet<ulong> _removed = new HashSet<ulong>();
        private double _time;
        private readonly Func<float, int> _depth;

        public BattleUnitPresentation(Transform parent, UnitVisualCatalog catalog, Func<float, int> depth)
        {
            _depth = depth;
            _parent = parent;
            _catalog = catalog ?? throw new InvalidOperationException("Missing UnitVisualCatalog.asset");
            _settings = BattlePresentationSettings.Parse(catalog.Presentation.text);
            foreach (UnitKind kind in Enum.GetValues(typeof(UnitKind)))
            {
                string resource = _settings.AnimationFor(kind).ResourceId;
                var asset = Array.Find(catalog.Animations, animation => animation != null && animation.name == resource);
                var player = new UnitAnimationPlayer(asset);
                player.Require(UnitAction.Hurt);
                player.Require(UnitAction.Death);
                _players.Add(kind, player);
            }
        }

        public void Synchronize(IReadOnlyList<UnitSnapshot> units, BattleDefinition definition, double simulationTime, float deltaTime, bool ended)
        {
            _time = Math.Max(_time, simulationTime);
            if (ended)
                _time += deltaTime;
            foreach (var unit in units)
            {
                if (_removed.Contains(unit.Id))
                    continue;
                if (!_views.TryGetValue(unit.Id, out var view))
                {
                    var stats = definition.GetUnit(unit.Kind);
                    var player = _players[unit.Kind];
                    view = new UnitView(_parent, player.Require(UnitAction.Idle).Frame(0), _catalog.White, _catalog.Circle,
                        stats.VisualHeight, stats.Radius, unit.Kind == UnitKind.Hero ? Color.cyan : Color.red, player, _settings.Death, _depth, "Default");
                    _views.Add(unit.Id, view);
                }
                view.Render(unit, _time, deltaTime);
                if (view.IsReadyForRemoval)
                {
                    view.Destroy();
                    _views.Remove(unit.Id);
                    _removed.Add(unit.Id);
                }
            }
        }

        public void Clear()
        {
            foreach (var view in _views.Values)
                view.Destroy();
            _views.Clear();
            _removed.Clear();
            _time = 0;
        }
    }
}
