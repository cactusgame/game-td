namespace Game.Battle.Domain.ValueObjects
{
    /// <summary>模拟当前执行的行为；Attacking 包含射程内等待攻击冷却。</summary>
    public enum UnitBehavior : byte
    {
        None,
        Idle,
        Moving,
        Regrouping,
        Returning,
        Chasing,
        Attacking,
        Dead
    }
}
