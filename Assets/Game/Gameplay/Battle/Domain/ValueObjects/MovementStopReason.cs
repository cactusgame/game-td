namespace Game.Battle.Domain.ValueObjects
{
    /// <summary>模拟记录的本步移动停止原因；调试界面只翻译，不重新推断寻路或战斗规则。</summary>
    public enum MovementStopReason : byte
    {
        None,
        Dead,
        Stunned,
        Rooted,
        HoldGround,
        HeroUnavailable,
        InAttackRange,
        Arrived,
        NoTarget,
        NoPath,
        WaitingForPath,
        BlockedByTerrain,
        NoMoveInput
    }
}
