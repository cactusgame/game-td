using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Components
{
    /// <summary>怪物私有的连续地面路径点，不共享可变路线或游标。</summary>
    [InternalBufferCapacity(8)]
    public struct NavigationWaypoint : IBufferElementData
    {
        public float2 Position;
    }
}
