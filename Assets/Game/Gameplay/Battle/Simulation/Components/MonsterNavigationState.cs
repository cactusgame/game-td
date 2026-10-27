using Game.Battle.Domain.ValueObjects;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Components
{
    /// <summary>每个实体独立的导航进度；由导航系统选路，移动 Job 消费路径。</summary>
    public struct MonsterNavigationState : IComponentData
    {
        public Entity StructureTarget;
        public float2 StructureGoal;
        public byte HasCastleObjective;
        public byte IsChasing;
        public int WaypointIndex;
        public int2 GoalCell;
        public double NextRepathTime;
        // 怪物导航裁决基础行为，后续近身/攻城阶段可提升为攻击；士兵行为由 SoldierState 拥有。
        public UnitBehavior Behavior;
        public MovementStopReason StopReason;
    }
}
