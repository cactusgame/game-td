using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Components
{
    /// <summary>怪物是否已开始本次英雄接战。首次进入攻击条件只启动普通攻击冷却，不产生伤害或动作。</summary>
    public struct HeroContact : IComponentData
    {
        public bool IsEngaged;
    }
}
