using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Components
{
    /// <summary>英雄复活的权威状态。生成时写入配置时长，复活系统在伤害结算后维护死亡位置和截止时间。</summary>
    public struct HeroRespawnState : IComponentData
    {
        public float DelaySeconds;
        public float InvulnerabilitySeconds;
        public bool IsWaiting;
        public double ReadyAt;
        public float2 DeathPosition;
        public uint DeathSequence;
        public float DeathFacingX;
    }
}
