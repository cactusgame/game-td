using System;
using System.Collections.Generic;
namespace Game.Fortifications.Domain.Aggregates.GateDefense
{
    /// <summary>运行城门的耐久与攻击间隔，由对局独占；自动开门不改变防御状态。</summary>
    public sealed class GateDefense
    {
        private readonly Dictionary<ulong, double> _nextAttacks = new Dictionary<ulong, double>();
        public int Health { get; private set; }
        public bool IsDestroyed => Health == 0;
        public GateDefense(int health) { Health = health; }
        /// <summary>创建候选，空间同步失败时保留旧耐久和攻击间隔。</summary>
        public GateDefense Copy()
        {
            var copy = new GateDefense(Health);
            foreach (var item in _nextAttacks)
                copy._nextAttacks.Add(item.Key, item.Value);
            return copy;
        }
        /// <summary>应用层确认接触后，以模拟时钟结算单个攻击者的伤害。</summary>
        public bool ReceiveAttack(ulong id, int damage, double now, float interval)
        {
            if (IsDestroyed || damage <= 0 || (_nextAttacks.TryGetValue(id, out var next) && now < next))
                return false;
            Health = Math.Max(0, Health - damage);
            _nextAttacks[id] = now + interval;
            return true;
        }
    }
}
