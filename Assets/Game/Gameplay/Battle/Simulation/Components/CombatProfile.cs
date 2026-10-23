using Unity.Entities;

namespace Game.Battle.Simulation.Components
{
    /// <summary>出生时由只读内容烘焙的攻击与行为参数，不与生命或移动数据混放。</summary>
    public struct CombatProfile : IComponentData
    {
        public float AttackInterval;
        public float AttackRange;
        public byte TargetMainCastle;
    }
}
