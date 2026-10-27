using Unity.Entities;

namespace Game.Battle.Simulation.Components
{
    /// <summary>所有阵营共享的状态真值。桥接施加效果，状态系统推进周期伤害，死亡/复活清理。
    /// 截止时刻使用模拟时间，各效果独立叠加；行动权限在实际移动、攻击、施法处检查。</summary>
    public struct UnitStatusState : IComponentData
    {
        public double InvulnerableUntil;
        public double StunnedUntil;
        public double RootedUntil;
        public double SilencedUntil;
        public double PoisonedUntil;
        public double NextPoisonTickAt;
        public double PoisonIntervalSeconds;
        public int PoisonDamagePerTick;

        public bool IsInvulnerableAt(double now) => now < InvulnerableUntil;
        public bool IsStunnedAt(double now) => now < StunnedUntil;
        public bool IsRootedAt(double now) => now < RootedUntil;
        public bool IsSilencedAt(double now) => now < SilencedUntil;
        public bool IsPoisonedAt(double now) => now < PoisonedUntil;
        public bool CanMoveAt(double now) => !IsStunnedAt(now) && !IsRootedAt(now);
        public bool CanAttackAt(double now) => !IsStunnedAt(now);
        public bool CanCastAt(double now) => !IsStunnedAt(now) && !IsSilencedAt(now);
    }
}
