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
        // 墙和门的低频攻城耐久由本协调器独占，复用相同的按攻击者冷却规则。
        private readonly Dictionary<string, GateDefense> _wallDefenses = new Dictionary<string, GateDefense>();
        private readonly HashSet<string> _opened = new HashSet<string>();
        public int ViewVersion { get; private set; }
        public int SpatialVersion { get; private set; }

        public GatePassageSession(MapContentCatalog catalog)
        {
            _catalog = catalog;
        }
        public int Health(string id) => _defenses.TryGetValue(id, out var state) ? state.Health : _catalog.Wall.GateHealth;
        private bool Destroyed(string id) => Health(id) == 0;
        public int WallHealth(string id) => _wallDefenses.TryGetValue(id, out var state) ? state.Health : _catalog.Wall.WallHealth;

        public bool ChangeHealth(IDefenseBattleSimulation simulation, MapLayout layout, BattleDefinition definition,
            string id, bool wall, int amount, bool repair)
        {
            var states = wall ? _wallDefenses : _defenses;
            int maximum = wall ? _catalog.Wall.WallHealth : _catalog.Wall.GateHealth;
            var candidate = states.TryGetValue(id, out var previous) ? previous.Copy() : new GateDefense(maximum);
            bool changed = repair ? candidate.Repair(amount, maximum) : candidate.Damage(amount);
            if (!changed)
                return false;
            // 先发布破坏后的空间；失败时不提交耐久，避免看似破坏却仍无法通行。
            if (candidate.IsDestroyed)
            {
                try
                {
                    simulation.UpdateNavigationMap(Navigation(layout, false, wall ? null : id, wall ? id : null),
                        definition.Navigation, Navigation(layout, true, wall ? null : id, wall ? id : null));
                }
                catch
                {
                    Synchronize(simulation, layout, definition);
                    throw;
                }
                if (wall)
                    DestroyAttachedGates(layout, id);
                SpatialVersion++;
            }
            states[id] = candidate;
            ViewVersion++;
            return true;
        }

        /// <summary>派生阵营通行图：完好门永远放行己方，永远阻挡敌方，与铁栅开关无关。</summary>
        public GridMap Navigation(MapLayout layout, bool friendly, string destroyedId = null, string destroyedWallId = null)
        {
            var copy = layout.Copy();
            // 墙体损毁只移除运行时阻挡投影，保留建设身份供废墟表现和查询使用。
            copy.wallEdges.RemoveAll(edge => WallHealth(edge.id) <= 0 || edge.id == destroyedWallId);
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
        public void Update(IDefenseBattleSimulation simulation, MapLayout layout, BattleDefinition definition,
            IReadOnlyList<UnitSnapshot> units, double now, bool applyDamage)
        {
            if (applyDamage)
                AttackWalls(simulation, layout, definition, units, now);
            var network = MapWallSnapshotMapper.Read(layout);
            var opened = new HashSet<string>();
            foreach (var edge in network.Edges)
            {
                foreach (var gate in edge.Gates)
                {
                    if (!Game.Fortifications.Domain.Rules.WallGateRules.TryGetSpan(network, edge, gate, out var span))
                        continue;
                    var state = _defenses.TryGetValue(gate.Id, out var previous) ? previous.Copy() : new GateDefense(_catalog.Wall.GateHealth);
                    var attackers = new List<ulong>();
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
                        // 英雄和我方士兵使用同一套提前开门距离，不按人物类别区分通行权限。
                        bool friendly = UnitKinds.IsFriendly(unit.Kind);
                        float reach = friendly
                            ? stats.Speed * definition.FixedStep + 0.5f
                            : projectionMargin + 0.25f;
                        if (along > (span.OpeningEnd - span.OpeningStart) * 0.5f + stats.Radius ||
                            across > _catalog.Wall.Width * 0.5f + stats.Radius + reach)
                            continue;
                        if (friendly)
                            opened.Add(gate.Id);
                        else if (UnitKinds.IsEnemy(unit.Kind) && applyDamage && stats.CanAttackGates &&
                            state.ReceiveAttack(unit.Id, stats.Attack, now, _catalog.Wall.GateAttackInterval))
                        {
                            attackers.Add(unit.Id);
                        }
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
                    // 冷却内不重播动作。致命一击须等待破门空间提交成功后再发布攻击事实。
                    foreach (ulong attackerId in attackers)
                        simulation.RecordExternalAttack(attackerId, span.Center);
                }
            }
            if (!_opened.SetEquals(opened))
            {
                _opened.Clear();
                _opened.UnionWith(opened);
                ViewVersion++;
            }
        }

        private void AttackWalls(IDefenseBattleSimulation simulation, MapLayout layout, BattleDefinition definition,
            IReadOnlyList<UnitSnapshot> units, double now)
        {
            foreach (var edge in layout.wallEdges)
            {
                if (WallHealth(edge.id) <= 0)
                    continue;
                var from = layout.wallNodes.Find(node => node.id == edge.from);
                var to = layout.wallNodes.Find(node => node.id == edge.to);
                if (from.x > to.x || from.x == to.x && from.y > to.y)
                {
                    var swap = from;
                    from = to;
                    to = swap;
                }
                float dx = to.x - from.x;
                float dy = to.y - from.y;
                float lengthSquared = dx * dx + dy * dy;
                if (lengthSquared <= 0)
                    continue;
                var state = _wallDefenses.TryGetValue(edge.id, out var previous)
                    ? previous.Copy() : new GateDefense(_catalog.Wall.WallHealth);
                foreach (var unit in units)
                {
                    var stats = definition.GetUnit(unit.Kind);
                    if (unit.Health <= 0 || !UnitKinds.IsEnemy(unit.Kind) || !stats.CanAttackGates)
                        continue;
                    float t = Math.Max(0, Math.Min(1, ((unit.X - from.x) * dx + (unit.Y - from.y) * dy) / lengthSquared));
                    float alongTiles = t * (float)Math.Sqrt(dx * dx / (layout.cellWidth * layout.cellWidth)
                        + dy * dy / (layout.cellHeight * layout.cellHeight));
                    // 门洞范围由门防御结算，不能一击同时伤害门和所属墙边。
                    if (edge.gates.Any(gate => alongTiles >= gate.startTile && alongTiles <= gate.startTile + gate.widthTiles))
                        continue;
                    var point = new MapPoint(from.x + t * dx, from.y + t * dy);
                    float x = unit.X - point.X;
                    float y = unit.Y - point.Y;
                    float reach = _catalog.Wall.Width * 0.5f + stats.Radius + Math.Max(layout.cellWidth, layout.cellHeight) * 0.5f + 0.25f;
                    if (x * x + y * y > reach * reach || !state.ReceiveAttack(unit.Id, stats.Attack, now, _catalog.Wall.GateAttackInterval))
                        continue;
                    if (state.IsDestroyed)
                    {
                        try
                        {
                            simulation.UpdateNavigationMap(Navigation(layout, false, destroyedWallId: edge.id),
                                definition.Navigation, Navigation(layout, true, destroyedWallId: edge.id));
                        }
                        catch
                        {
                            Synchronize(simulation, layout, definition);
                            throw;
                        }
                        DestroyAttachedGates(layout, edge.id);
                        SpatialVersion++;
                    }
                    _wallDefenses[edge.id] = state.Copy();
                    ViewVersion++;
                    simulation.RecordExternalAttack(unit.Id, point);
                }
            }
        }

        private void DestroyAttachedGates(MapLayout layout, string wallId)
        {
            // 当前耐久身份是一整条墙边；整边倒塌时，其承载的城门也随之损毁。
            foreach (var gate in layout.wallEdges.Find(edge => edge.id == wallId).gates)
                _defenses[gate.id] = new GateDefense(0);
        }
    }
}
