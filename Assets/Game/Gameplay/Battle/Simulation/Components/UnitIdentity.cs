using Game.Battle.Domain.ValueObjects;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Components
{
    /// <summary>单位的业务身份与分类；由出生阶段写入，不使用 Entity 句柄作为持久身份。</summary>
    public struct UnitIdentity : IComponentData
    {
        public ulong Id;
        public UnitKind Kind;
    }
}
