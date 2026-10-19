using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Components
{
    /// <summary>当前 World 的接触计数；出生阶段清零，接触结算系统归并写入。</summary>
    public struct ContactStatistics : IComponentData
    {
        public int Count;
    }
}
