using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Components
{
    /// <summary>战斗领域拥有选择、跟随偏移和驻守位置；IsSelected 为玩家持续跟随的权威状态。</summary>
    public struct SoldierState : IComponentData
    {
        public float2 Rally;
        public float2 FollowOffset;
        public bool IsSelected;
        public bool HasPlayerOrder;
    }
}
