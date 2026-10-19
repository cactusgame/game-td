using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Components
{
    /// <summary>每个实体独立的导航进度；由导航系统选路，移动 Job 消费路径。</summary>
    public struct MonsterNavigationState : IComponentData
    {
        public byte IsChasing;
        public int WaypointIndex;
        public int2 GoalCell;
        public double NextRepathTime;
    }
}
