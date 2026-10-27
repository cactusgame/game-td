using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Components
{
    /// <summary>怪物与英雄的上次接触状态；仅接触结算系统写入，持续重叠不重复伤害。</summary>
    public struct HeroContact : IComponentData
    {
        public byte WasOverlapping;
        public byte WasHeroInvulnerable;
        // 双方独立记忆攻击受控，解晕后只补该方的一次持续接触攻击，不重复另一方伤害。
        public byte WasHeroAttackBlocked;
        public byte WasMonsterAttackBlocked;
    }
}
