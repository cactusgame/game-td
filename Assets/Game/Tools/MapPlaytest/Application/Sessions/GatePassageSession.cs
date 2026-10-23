using System;
using System.Collections.Generic;
using System.Linq;
using Game.Battle.Contracts.Ports;
using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.Definitions;
using Game.Battle.Domain.ValueObjects;
using Game.Fortifications.Domain.Aggregates.GateDefense;
using Game.Maps.Domain;
using Game.Maps.Domain.Mappings;
using Game.MapEngine.Domain;

namespace Game.Tools.MapPlaytest.Application.Sessions
{
    /// <summary>对局的城门协调器：拥有低频门防御状态，按单位快照驱动表现，提交两阵营空间。</summary>
    public sealed class GatePassageSession
    {
        private readonly MapContentCatalog _catalog;
        private readonly Dictionary<string, GateDefense> _defenses = new Dictionary<string, GateDefense>();
        private readonly HashSet<string> _opened = new HashSet<string>();
        public int ViewVersion { get; private set; }
        public int SpatialVersion { get; private set; }

        public GatePassageSession(MapContentCatalog catalog)
        {
            _catalog = catalog;
        }
        public int Health(string id) => _defenses.TryGetValue(id, out var state) ? state.Health : _catalog.Wall.GateHealth;
        private bool Destroyed(string id) => Health(id) == 0;

        /// <summary>派生阵营通行图：完好门永远放行己方，永远阻挡敌方，与铁栅开关无关。</summary>
        public GridMap Navigation(MapLayout layout, bool friendly, string destroyedId = null)
        {
            var copy = layout.Copy();
            foreach (var gate in copy.wallEdges.SelectMany(edge => edge.gates))
                gate.isOpen = friendly || Destroyed(gate.id) || gate.id == destroyedId;
            return MapSpatialRules.CreateNavigationMap(copy, _catalog);
        }

        /// <summary>在模拟步之间同步路径与碰撞；敌人规划含门洞的攻城路线，碰撞仍禁止穿门。</summary>
        public void Synchronize(ISpatialBattleSimulation simulation, MapLayout layout, BattleDefinition definition)
        {
            simulation.UpdateNavigationMap(Navigation(layout, false), definition.Navigation, Navigation(layout, true));
        }

        /// <summary>表现只读取此投影；摧毁后保留门框并去掉铁栅，不回写地图定义。</summary>
        public MapLayout Project(MapLayout layout)
        {
            foreach (var gate in layout.wallEdges.SelectMany(edge => edge.gates))
            {
                gate.isDestroyed = Destroyed(gate.id);
                gate.isOpen = gate.isDestroyed || _opened.Contains(gate.id);
            }
            return layout;
        }

        /// <summary>移动完成后读取活单位；敌人接触门洞才攻击，空间发布成功才提交摧毁事实。</summary>
        public void Update(ISpatialBattleSimulation simulation, MapLayout layout, BattleDefinition definition,
            IReadOnlyList<UnitSnapshot> units, double now, bool applyDamage)
        {
            var network = MapWallSnapshotMapper.Read(layout);
            var opened = new HashSet<string>();
            foreach (var edge in network.Edges)
            {
                foreach (var gate in edge.Gates)
                {
                    if (!Game.Fortifications.Domain.Rules.WallGateRules.TryGetSpan(network, edge, gate, out var span))
                        continue;
                    var state = _defenses.TryGetValue(gate.Id, out var previous) ? previous.Copy() : new GateDefense(_catalog.Wall.GateHealth);
                    foreach (var unit in units)
                    {
                        if (unit.Health <= 0)
                            continue;
                        var stats = definition.GetUnit(unit.Kind);
                        float x = unit.X - span.Center.X;
                        float y = unit.Y - span.Center.Y;
                        float along = Math.Abs(x * span.Direction.X + y * span.Direction.Y);
                        float across = Math.Abs(-x * span.Direction.Y + y * span.Direction.X);
                        // 保守格子投影会把门前单位挡在整格边界，攻击距离需覆盖这段投影余量。
                        float projectionMargin = Math.Max(layout.cellWidth, layout.cellHeight) * 0.5f;
                        float reach = unit.Kind == UnitKind.Hero
                            ? stats.Speed * definition.FixedStep + 0.5f
                            : projectionMargin + 0.25f;
                        if (along > (span.OpeningEnd - span.OpeningStart) * 0.5f + stats.Radius ||
                            across > _catalog.Wall.Width * 0.5f + stats.Radius + reach)
                            continue;
                        if (unit.Kind == UnitKind.Hero)
                            opened.Add(gate.Id);
                        else if (applyDamage)
                            state.ReceiveAttack(unit.Id, stats.Attack, now, _catalog.Wall.GateAttackInterval);
                    }
                    if (state.IsDestroyed && !Destroyed(gate.Id))
                    {
                        try
                        {
                            simulation.UpdateNavigationMap(Navigation(layout, false, gate.Id), definition.Navigation, Navigation(layout, true));
                        }
                        catch
                        {
                            Synchronize(simulation, layout, definition);
                            throw;
                        }
                        SpatialVersion++;
                        ViewVersion++;
                    }
                    _defenses[gate.Id] = state;
                }
            }
            if (!_opened.SetEquals(opened))
            {
                _opened.Clear();
                _opened.UnionWith(opened);
                ViewVersion++;
            }
        }
    }
}
