using System;

namespace Game.Economy.Domain.Aggregates.ResourceAccount
{
    /// <summary>本局金币的唯一账户。应用协调器在建设或升级提交边界支付。</summary>
    public sealed class ResourceAccount
    {
        public int Gold { get; private set; }
        public ResourceAccount(int initialGold)
        {
            if (initialGold < 0) throw new ArgumentOutOfRangeException(nameof(initialGold));
            Gold = initialGold;
        }
        public bool CanSpend(int amount) => amount >= 0 && Gold >= amount;
        public bool TrySpend(int amount)
        {
            if (!CanSpend(amount)) return false;
            Gold -= amount;
            return true;
        }
    }
}
