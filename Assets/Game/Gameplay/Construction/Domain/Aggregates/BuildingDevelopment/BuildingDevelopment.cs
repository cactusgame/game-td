using System;
using System.Collections.Generic;
using Game.Construction.Domain.Definitions;

namespace Game.Construction.Domain.Aggregates.BuildingDevelopment
{
    /// <summary>本局投资和普通建筑升级选择。生命由战斗模拟拥有；兵营升级资格仍归编制聚合。</summary>
    public sealed class BuildingDevelopment
    {
        private readonly HashSet<string> _upgrades = new HashSet<string>();
        private readonly HashSet<string> _groups = new HashSet<string>();
        public int Investment { get; private set; }
        public int Level => _upgrades.Count + 1;
        public int HealthBonus { get; private set; }
        public int AttackBonus { get; private set; }
        public float RangeBonus { get; private set; }

        public BuildingDevelopment(int investment = 0) => RecordPayment(investment);
        public BuildingDevelopment CreateCandidate()
        {
            var candidate = new BuildingDevelopment(Investment)
            {
                HealthBonus = HealthBonus, AttackBonus = AttackBonus, RangeBonus = RangeBonus
            };
            candidate._upgrades.UnionWith(_upgrades);
            candidate._groups.UnionWith(_groups);
            return candidate;
        }
        public bool CanUpgrade(UpgradeBranchDefinition upgrade) => !_upgrades.Contains(upgrade.Id) &&
            (string.IsNullOrEmpty(upgrade.PrerequisiteId) || _upgrades.Contains(upgrade.PrerequisiteId)) &&
            (string.IsNullOrEmpty(upgrade.ExclusiveGroup) || !_groups.Contains(upgrade.ExclusiveGroup));
        public void ApplyUpgrade(UpgradeBranchDefinition upgrade)
        {
            if (!CanUpgrade(upgrade)) throw new InvalidOperationException("升级前置或互斥条件不满足。");
            _upgrades.Add(upgrade.Id);
            if (!string.IsNullOrEmpty(upgrade.ExclusiveGroup)) _groups.Add(upgrade.ExclusiveGroup);
            HealthBonus = checked(HealthBonus + upgrade.MaxHealthBonus);
            AttackBonus = checked(AttackBonus + upgrade.AttackBonus);
            RangeBonus += upgrade.RangeBonus;
            RecordPayment(upgrade.Cost);
        }
        public void RecordPayment(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Investment = checked(Investment + amount);
        }
        public int Refund(float ratio, int health, int maxHealth) =>
            (int)Math.Floor(Investment * (double)ratio * Math.Max(0, Math.Min(health, maxHealth)) / Math.Max(1, maxHealth));
    }
}
