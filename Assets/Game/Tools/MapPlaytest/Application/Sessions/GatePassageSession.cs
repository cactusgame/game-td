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
        // 墙门只拥有耐久；攻击者的普通攻击冷却统一由战斗模拟拥有。
        private readonly Dictionary<string, GateDefense> _wallDefenses = new Dictionary<string, GateDefense>();
        private readonly Dictionary<ulong, string> _attackTargets = new Dictionary<ulong, string>();
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

        /// <summary>移动后投影开门，选择并保持可攻击墙门；伤害与空间成功提交才消耗单位共享冷却。</summary>
        public void Update(IDefenseBattleSimulation simulation, MapLayout layout, BattleDefinition definition,
            IReadOnlyList<UnitSnapshot> units, double now, bool applyDamage)
        {
            var network = MapWallSnapshotMapper.Read(layout);
            var opened = new HashSet<string>();
            var livingEnemies = new HashSet<ulong>();
            foreach (var unit in units)
            {
                if (unit.Health <= 0) continue;
                var stats = definition.GetUnit(unit.Kind);
                var candidates = new List<AttackTarget>();
                foreach (var edge in network.Edges)
                {
                    foreach (var gate in edge.Gates)
                    {
                        if (!Game.Fortifications.Domain.Rules.WallGateRules.TryGetSpan(network, edge, gate, out var span)) continue;
                        float x = unit.X - span.Center.X;
                        float y = unit.Y - span.Center.Y;
                        bool friendly = UnitKinds.IsFriendly(unit.Kind);
                        float reach = friendly ? stats.Speed * definition.FixedStep + 0.5f : Math.Max(layout.cellWidth, layout.cellHeight) * 0.5f + 0.25f;
                        float along = Math.Abs(x * span.Direction.X + y * span.Direction.Y);
                        float across = Math.Abs(-x * span.Direction.Y + y * span.Direction.X);
                        if (along > (span.OpeningEnd - span.OpeningStart) * 0.5f + stats.Radius ||
                            across > _catalog.Wall.Width * 0.5f + stats.Radius + reach) continue;
                        if (friendly) opened.Add(gate.Id);
                        else if (!Destroyed(gate.Id)) candidates.Add(new AttackTarget(gate.Id, false, span.Center, x * x + y * y));
                    }
                }
                if (!UnitKinds.IsEnemy(unit.Kind)) continue;
                livingEnemies.Add(unit.Id);
                if (!applyDamage || !unit.CanAttack || !stats.CanAttackGates) continue;
                AddWallTargets(layout, unit, stats, candidates);
                AttackTarget? chosen = null;
                _attackTargets.TryGetValue(unit.Id, out string previous);
                foreach (var candidate in candidates)
                {
                    if (candidate.Key == previous) { chosen = candidate; break; }
                    if (!chosen.HasValue || candidate.DistanceSquared < chosen.Value.DistanceSquared ||
                        (candidate.DistanceSquared == chosen.Value.DistanceSquared && string.CompareOrdinal(candidate.Key, chosen.Value.Key) < 0))
                        chosen = candidate;
                }
                if (!chosen.HasValue)
                {
                    _attackTargets.Remove(unit.Id);
                    continue;
                }
                var target = chosen.Value;
                _attackTargets[unit.Id] = target.Key;
                simulation.TryAttackExternalTarget(unit.Id, target.Point, now,
                    damage => CommitAttack(simulation, layout, definition, target, damage), target.Key);
            }
            foreach (ulong id in _attackTargets.Keys.Where(id => !livingEnemies.Contains(id)).ToArray()) _attackTargets.Remove(id);
            if (!_opened.SetEquals(opened))
            {
                _opened.Clear();
                _opened.UnionWith(opened);
                ViewVersion++;
            }
        }

        private void AddWallTargets(MapLayout layout, UnitSnapshot unit, UnitDefinition stats, List<AttackTarget> targets)
        {
            foreach (var edge in layout.wallEdges)
            {
                if (WallHealth(edge.id) <= 0) continue;
                var from = layout.wallNodes.Find(node => node.id == edge.from);
                var to = layout.wallNodes.Find(node => node.id == edge.to);
                if (from.x > to.x || from.x == to.x && from.y > to.y) { var swap = from; from = to; to = swap; }
                float dx = to.x - from.x;
                float dy = to.y - from.y;
                float lengthSquared = dx * dx + dy * dy;
                if (lengthSquared <= 0) continue;
                float t = Math.Max(0, Math.Min(1, ((unit.X - from.x) * dx + (unit.Y - from.y) * dy) / lengthSquared));
                float alongTiles = t * (float)Math.Sqrt(dx * dx / (layout.cellWidth * layout.cellWidth) + dy * dy / (layout.cellHeight * layout.cellHeight));
                if (edge.gates.Any(gate => alongTiles >= gate.startTile && alongTiles <= gate.startTile + gate.widthTiles)) continue;
                var point = new MapPoint(from.x + t * dx, from.y + t * dy);
                float x = unit.X - point.X;
                float y = unit.Y - point.Y;
                float reach = _catalog.Wall.Width * 0.5f + stats.Radius + Math.Max(layout.cellWidth, layout.cellHeight) * 0.5f + 0.25f;
                if (x * x + y * y <= reach * reach) targets.Add(new AttackTarget(edge.id, true, point, x * x + y * y));
            }
        }

        private bool CommitAttack(IDefenseBattleSimulation simulation, MapLayout layout, BattleDefinition definition, AttackTarget target, int damage)
        {
            var states = target.IsWall ? _wallDefenses : _defenses;
            int maximum = target.IsWall ? _catalog.Wall.WallHealth : _catalog.Wall.GateHealth;
            var candidate = states.TryGetValue(target.Id, out var old) ? old.Copy() : new GateDefense(maximum);
            if (!candidate.Damage(damage)) return false;
            if (candidate.IsDestroyed)
            {
                try
                {
                    simulation.UpdateNavigationMap(Navigation(layout, false, target.IsWall ? null : target.Id, target.IsWall ? target.Id : null),
                        definition.Navigation, Navigation(layout, true, target.IsWall ? null : target.Id, target.IsWall ? target.Id : null));
                }
                catch
                {
                    Synchronize(simulation, layout, definition);
                    throw;
                }
                if (target.IsWall) DestroyAttachedGates(layout, target.Id);
                SpatialVersion++;
            }
            states[target.Id] = candidate;
            ViewVersion++;
            return true;
        }

        private readonly struct AttackTarget
        {
            public readonly string Id;
            public readonly bool IsWall;
            public readonly MapPoint Point;
            public readonly float DistanceSquared;
            public string Key => (IsWall ? "wall:" : "gate:") + Id;
            public AttackTarget(string id, bool isWall, MapPoint point, float distanceSquared)
            {
                Id = id;
                IsWall = isWall;
                Point = point;
                DistanceSquared = distanceSquared;
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
