using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Components
{
    /// <summary>二维权威地面位置；移动系统写入，Previous 用于本步扫掠检测。</summary>
    public struct GroundTransform : IComponentData
    {
        public float2 Position;
        public float2 Previous;
        public float FacingX;
    }
}
