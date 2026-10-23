using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Components
{
    /// <summary>战斗系统唯一写入生命与攻击时钟；地面占地由建设同步。</summary>
    public struct DefenseStructure : IComponentData
    {
        public float2 Min;
        public float2 Max;
        public int Health;
        public int MaxHealth;
        public int Attack;
        public float Range;
        public float AttackInterval;
        public double NextAttack;
        public byte IsMainCastle;
    }
}
