using System;
using System.Collections.Generic;

namespace Game.Economy.Domain.Aggregates.ResourceAccount
{
    /// <summary>本局金币的唯一账户。应用协调器在建设或升级提交边界支付。</summary>
    public sealed class ResourceAccount
    {
        private readonly HashSet<string> _creditedRewards = new HashSet<string>();
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
        public bool CanCredit(int amount) => amount >= 0 && (long)Gold + amount <= int.MaxValue;
        /// <summary>经济账户拥有入账去重；失败不消费收据，重启通过新账户重置本局收据。</summary>
        public bool CreditOnce(string rewardId, int amount)
        {
            if (string.IsNullOrWhiteSpace(rewardId)) throw new ArgumentException("奖励 ID 不能为空", nameof(rewardId));
            if (_creditedRewards.Contains(rewardId)) return false;
            if (!CanCredit(amount)) throw new ArgumentOutOfRangeException(nameof(amount));
            _creditedRewards.Add(rewardId);
            Gold += amount;
            return true;
        }
        public void Credit(int amount)
        {
            if (!CanCredit(amount)) throw new ArgumentOutOfRangeException(nameof(amount));
            Gold += amount;
        }
    }
}
