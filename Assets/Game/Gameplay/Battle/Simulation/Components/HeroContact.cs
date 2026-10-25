using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Components
{
    /// <summary>怪物与英雄的上次接触状态；仅接触结算系统写入，持续重叠不重复伤害。</summary>
    public struct HeroContact : IComponentData
    {
        public byte WasOverlapping;
        public byte WasHeroInvulnerable;
    }
}
