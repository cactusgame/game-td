using System;
namespace Game.Fortifications.Domain.Aggregates.GateDefense
{
    /// <summary>运行墙门的耐久，由对局独占；单位攻击冷却归战斗模拟；自动开门不改变防御状态。</summary>
    public sealed class GateDefense
    {
        public int Health { get; private set; }
        public bool IsDestroyed => Health == 0;
        public GateDefense(int health) { Health = health; }
        /// <summary>创建耐久候选，空间同步失败时不提交。</summary>
        public GateDefense Copy() => new GateDefense(Health);

        public bool Damage(int amount)
        {
            if (IsDestroyed || amount <= 0)
                return false;
            Health = Math.Max(0, Health - amount);
            return true;
        }

        public bool Repair(int amount, int maxHealth)
        {
            if (IsDestroyed || amount <= 0 || Health >= maxHealth)
                return false;
            Health += Math.Min(amount, maxHealth - Health);
            return true;
        }
    }
}
