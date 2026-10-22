using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Components
{
    /// <summary>World 内本局设置与当前移动输入；模拟桥接器在初始化及步进前写入。</summary>
    public struct BattleSettings : IComponentData
    {
        public float2 HalfSize;
        public float2 Center;
        public float2 MoveInput;
    }
}
