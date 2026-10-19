using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Components
{
    /// <summary>生命与战斗数值真值；出生阶段初始化，接触结算系统写入生命和反馈时间。</summary>
    public struct Vitals : IComponentData
    {
        public int Health;
        public int MaxHealth;
        public int Attack;
        public int Defense;
        public float LastHitTime;
        public float LastAttackTime;
    }
}
