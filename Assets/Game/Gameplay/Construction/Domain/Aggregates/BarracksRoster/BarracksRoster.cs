using System;
using System.Collections.Generic;
using Game.Construction.Domain.Definitions;

namespace Game.Construction.Domain.Aggregates.BarracksRoster
{
    /// <summary>每座兵营独占的编制与训练槽。只保存单位身份，不复制单位生命。</summary>
    public sealed class BarracksRoster
    {
        private sealed class Slot
        {
            public ulong UnitId;
            public double ReadyAt;
        }
        private readonly List<Slot> _slots = new List<Slot>();
        private readonly HashSet<string> _upgrades = new HashSet<string>();
        private readonly HashSet<string> _exclusiveGroups = new HashSet<string>();
        public string Id { get; }
        public string BuildingId { get; }
        public int Capacity => _slots.Count;
        public int Level => _upgrades.Count + 1;
        public BarracksRoster(string instanceId, string buildingId, int capacity)
        {
            if (string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(buildingId))
                throw new ArgumentException("Barracks instance and definition IDs are required.");
            Id = instanceId;
            BuildingId = buildingId;
            AddSlots(capacity, 0);
        }
        public ulong UnitAt(int slot) => _slots[slot].UnitId;
        public bool IsReady(int slot, double now) => _slots[slot].UnitId == 0 && now >= _slots[slot].ReadyAt;
        public double RemainingTraining(int slot, double now) => _slots[slot].UnitId == 0 ? Math.Max(0, _slots[slot].ReadyAt - now) : 0;
        public void ObserveDeath(int slot, double now, float retrainSeconds)
        {
            if (_slots[slot].UnitId == 0) return;
            _slots[slot].UnitId = 0;
            _slots[slot].ReadyAt = now + retrainSeconds;
        }
        public void Assign(int slot, ulong unitId)
        {
            if (unitId == 0 || _slots[slot].UnitId != 0) throw new InvalidOperationException("Training slot is occupied.");
            _slots[slot].UnitId = unitId;
        }
        public bool CanUpgrade(UpgradeBranchDefinition upgrade)
        {
            return upgrade.BuildingId == BuildingId && upgrade.CapacityBonus > 0 && Capacity + upgrade.CapacityBonus <= 100 &&
                !_upgrades.Contains(upgrade.Id) && (string.IsNullOrEmpty(upgrade.ExclusiveGroup) || !_exclusiveGroups.Contains(upgrade.ExclusiveGroup)) &&
                (string.IsNullOrEmpty(upgrade.PrerequisiteId) || _upgrades.Contains(upgrade.PrerequisiteId));
        }
        public void Upgrade(UpgradeBranchDefinition upgrade, double now, float retrainSeconds)
        {
            if (!CanUpgrade(upgrade)) throw new InvalidOperationException("Barracks upgrade prerequisites are not satisfied.");
            _upgrades.Add(upgrade.Id);
            if (!string.IsNullOrEmpty(upgrade.ExclusiveGroup)) _exclusiveGroups.Add(upgrade.ExclusiveGroup);
            AddSlots(upgrade.CapacityBonus, now + retrainSeconds);
        }
        private void AddSlots(int count, double readyAt)
        {
            if (count <= 0 || count > 100) throw new ArgumentOutOfRangeException(nameof(count));
            for (int i = 0; i < count; i++) _slots.Add(new Slot { ReadyAt = readyAt });
        }
    }
}
