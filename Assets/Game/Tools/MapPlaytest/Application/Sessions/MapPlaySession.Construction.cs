using System;
using System.Collections.Generic;
using System.Linq;
using Game.Battle.Contracts.Ports;
using Game.Construction.Domain.Aggregates.BuildingDevelopment;
using Game.Construction.Domain.Definitions;
using Game.MapEngine.Domain;
using Game.Maps.Domain;

namespace Game.Tools.MapPlaytest.Application.Sessions
{
    public sealed partial class MapPlaySession
    {
        private readonly Dictionary<string, BuildingDevelopment> _developments = new Dictionary<string, BuildingDevelopment>();
        private readonly Dictionary<string, int> _wallInvestments = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _gateInvestments = new Dictionary<string, int>();
        public void CancelPlayerOperations()
        {
            (_input as IPlayerOperationInput)?.ResetOperations();
            (_simulation as IPlayerOperations)?.Execute(Game.Battle.Contracts.Commands.PlayerOperation.Cancel);
        }
        public IReadOnlyList<BuildingDevelopmentDefinition> BuildableBuildings => _construction == null
            ? Array.Empty<BuildingDevelopmentDefinition>()
            : _construction.Buildings.Values.Where(rule => rule.CanPlayerBuild).ToArray();
        public BuildingDevelopmentDefinition BuildingRule(string definitionId) =>
            _construction != null && _construction.Buildings.TryGetValue(definitionId, out var rule) ? rule : null;

        private BuildingDevelopment Development(string id)
        {
            if (!_developments.TryGetValue(id, out var state))
            {
                state = new BuildingDevelopment();
                _developments.Add(id, state);
            }
            return state;
        }

        private bool CanBuildDefinition(string id, ConstructionPlacementKind kind, out string reason)
        {
            reason = null;
            // 不带建设配置的旧空间测试保留免费布置；实际游戏始终加载建设配置。
            if (_construction == null) return true;
            if (!_construction.Buildings.TryGetValue(id, out var rule) || !rule.CanPlayerBuild)
                reason = "该建筑不允许玩家在战斗中建设";
            else if (rule.PlacementKind != kind)
                reason = "请使用对应的城墙、城门或敌台工具";
            else if (rule.BuildSeconds != 0)
                reason = "当前仅支持即时建设";
            return reason == null;
        }

        /// <summary>预览在隔离候选上复用正式占地和单位净空规则，不发布、不扣款。</summary>
        public bool PreviewBuild(string definitionId, int x, int y, out string reason)
        {
            RequireActive();
            if (!CanChangeConstruction(out reason) || !CanBuildDefinition(definitionId, ConstructionPlacementKind.Building, out reason)) return false;
            if (_construction != null && !_account.CanSpend(_construction.Buildings[definitionId].BuildCost))
            {
                reason = "金币不足";
                return false;
            }
            var candidate = _placements.CreateCandidate();
            return candidate.TryBuild(definitionId, x, y, UnitsRemainClear, out reason) &&
                ValidateBuildingExit(candidate, definitionId, x, y, out reason);
        }

        private bool ValidateBuildingExit(Game.Construction.Application.Sessions.MapConstructionState candidate,
            string definitionId, int x, int y, out string reason)
        {
            reason = null;
            if (_construction == null || !_construction.Barracks.TryGetValue(definitionId, out var training)) return true;
            var soldier = Definition.Soldiers.First(unit => unit.Id == training.UnitId);
            var size = _catalog.Find(definitionId);
            var map = candidate.Snapshot;
            float exitX = map.originX + (x + size.width * 0.5f) * map.cellWidth;
            float exitY = map.originY + y * map.cellHeight - soldier.Radius - 0.08f;
            if (candidate.Navigation.CanOccupy(exitX, exitY, soldier.Radius)) return true;
            reason = "兵营下方出口缺少通行空间，请重新选择地块";
            return false;
        }

        public InitialFacility FindBuilding(float x, float y)
        {
            var map = Snapshot;
            if (!map.Geometry.TryWorldToCell(x, y, out var cell)) return null;
            return map.facilities.Find(item =>
            {
                var size = _catalog.Find(item.definitionId);
                return size != null && cell.X >= item.x && cell.X < item.x + item.Width(size) &&
                    cell.Y >= item.y && cell.Y < item.y + item.Height(size);
            });
        }

        public int BuildingLevel(string id) => _barracks.TryGetValue(id, out var roster) ? roster.Level : Development(id).Level;
        public float BuildingAttackRange(string id)
        {
            var building = Snapshot.facilities.Find(item => item.id == id);
            return building == null ? 0 : _catalog.Find(building.definitionId).attackRange + Development(id).RangeBonus;
        }

        public IReadOnlyList<UpgradeBranchDefinition> AvailableUpgrades(string id)
        {
            var building = Snapshot.facilities.Find(item => item.id == id);
            if (building == null || _construction == null || !_catalog.Find(building.definitionId).hasFunction)
                return Array.Empty<UpgradeBranchDefinition>();
            var state = Development(id);
            return _construction.Upgrades.Values.Where(upgrade => upgrade.BuildingId == building.definitionId &&
                (_barracks.TryGetValue(id, out var roster) ? roster.CanUpgrade(upgrade) : state.CanUpgrade(upgrade))).ToArray();
        }

        public bool TryUpgradeBuilding(string id, string upgradeId, out string reason)
        {
            RequireActive();
            if (!CanChangeConstruction(out reason)) return false;
            var upgrade = AvailableUpgrades(id).FirstOrDefault(item => item.Id == upgradeId);
            if (upgrade == null) { reason = "升级前置、互斥条件不满足或目标已移除"; return false; }
            ReadStructures();
            if (_structures.Exists(item => item.Id == id && item.Health <= 0))
            { reason = "建筑已毁，不能升级"; return false; }
            if (!_account.CanSpend(upgrade.Cost)) { reason = "金币不足"; return false; }
            if (upgrade.Seconds != 0) { reason = "当前仅支持即时升级"; return false; }
            if (_barracks.ContainsKey(id)) return TryUpgradeBarracks(id, out reason, upgradeId);
            var previous = Development(id);
            var candidate = previous.CreateCandidate();
            candidate.ApplyUpgrade(upgrade);
            // 同步期间仅能力投影读取候选。失败恢复旧投影，生命真值仍归模拟。
            _developments[id] = candidate;
            try
            {
                ((IDefenseBattleSimulation)_simulation).SynchronizeStructures(DescribeStructures(_placements.Snapshot));
            }
            catch
            {
                _developments[id] = previous;
                ((IDefenseBattleSimulation)_simulation).SynchronizeStructures(DescribeStructures(_placements.Snapshot));
                reason = "升级同步失败，未扣款";
                return false;
            }
            _account.TrySpend(upgrade.Cost);
            ReadStructures();
            reason = null;
            return true;
        }

        public bool QuoteBuildingSale(string id, out int refund, out string reason)
        {
            refund = 0;
            if (!CanChangeConstruction(out reason)) return false;
            var building = Snapshot.facilities.Find(item => item.id == id);
            if (building == null || _construction == null || !_construction.Buildings.TryGetValue(building.definitionId, out var rule))
            { reason = "建筑已移除或缺少建设规则"; return false; }
            if (!rule.CanPlayerBuild || !rule.CanDemolish || building.definitionId == "hall")
            { reason = "装饰、内置建筑和主堡不能售卖"; return false; }
            if (!string.IsNullOrEmpty(building.wallNodeId) && Snapshot.wallEdges.Any(edge => edge.from == building.wallNodeId || edge.to == building.wallNodeId))
            { reason = "请先售卖敌台相连的墙段，再售卖敌台"; return false; }
            ReadStructures();
            var health = _structures.Find(item => item.Id == id);
            if (health.Health <= 0) { reason = "建筑已被摧毁，不能售卖"; return false; }
            int maxHealth = _catalog.Find(building.definitionId).maxHealth + Development(id).HealthBonus;
            refund = Development(id).Refund(rule.RefundRatio, health.Health, maxHealth);
            if (!_account.CanCredit(refund)) { reason = "金币账户已达上限"; return false; }
            reason = null;
            return true;
        }

        public bool TrySellBuilding(string id, out string reason)
        {
            RequireActive();
            if (!QuoteBuildingSale(id, out int refund, out reason)) return false;
            var retired = new List<ulong>();
            if (_barracks.TryGetValue(id, out var roster))
                for (int slot = 0; slot < roster.Capacity; slot++)
                    if (roster.UnitAt(slot) != 0) retired.Add(roster.UnitAt(slot));
            if (retired.Count > 0 && !(_simulation is ISoldierDismissal))
            { reason = "当前模拟不支持兵营撤军"; return false; }
            var candidate = _placements.CreateCandidate();
            if (!candidate.TryRemoveBuilding(id) || !TryPublish(candidate, out reason)) return false;
            (_simulation as ISoldierDismissal)?.DismissSoldiers(retired);
            _barracks.Remove(id);
            _developments.Remove(id);
            _account.Credit(refund);
            ReadStructures();
            _battle.RefreshSnapshot();
            return true;
        }
    }
}
