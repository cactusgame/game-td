using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Components
{
    /// <summary>每单位独立的随机流；移动系统推进，导航系统在脱离追击时重置剩余时间。</summary>
    public struct WanderState : IComponentData
    {
        public Random Random;
        public float2 Direction;
        public float Remaining;
    }
}
