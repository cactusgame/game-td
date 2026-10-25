using Unity.Entities;

namespace Game.Battle.Simulation.Components
{
    /// <summary>人物状态的权威存储，使用模拟时间。伤害入口检查无敌；表现只读取快照。
    /// 后续控制、持续伤害状态应在此类状态边界明确持续时间与写入系统，不由特效裁决。</summary>
    public struct UnitStatusState : IComponentData
    {
        public double InvulnerableUntil;

        public bool IsInvulnerableAt(double now) => now < InvulnerableUntil;
    }
}
