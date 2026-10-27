using Unity.Entities;

namespace Game.Battle.Simulation.Components
{
    /// <summary>出生时投影每秒绝对恢复量；模拟拥有恢复余量与阻断时间，战斗、异常、满血和死亡时清空余量。</summary>
    public struct HealthRegenerationState : IComponentData
    {
        public float HealthPerSecond;
        public double AccumulatedHealing;
        public double CombatBlockedUntil;
        // 状态系统在清除到期中毒前保存本步的状态截止时刻，供恢复阶段裁剪有效时间。
        public double StatusBlockedUntil;
        // 墙门在 ECS 固定步之后结算；若本时刻开始外部攻击，撤销本步刚恢复的生命。
        public double LastRecoveryAt;
        public int HealthRestoredThisStep;
    }
}
