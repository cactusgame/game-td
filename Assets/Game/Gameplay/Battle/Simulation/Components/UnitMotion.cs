using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Components
{
    /// <summary>单位速度和碰撞半径；出生阶段由配置初始化，表现不修改。</summary>
    public struct UnitMotion : IComponentData
    {
        public float Speed;
        public float Radius;
    }
}
