namespace Game.Battle.Domain.ValueObjects
{
    /// <summary>持续意图；None 使用出生集结点，MoveToPosition 到位后继续以该点警戒。</summary>
    public enum SoldierCommand : byte
    {
        None,
        FollowHero,
        MoveToPosition
    }
}
