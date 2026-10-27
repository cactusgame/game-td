namespace Game.Battle.Domain.ValueObjects
{
    /// <summary>自主交战约束，与选择、命令及当前行为正交。</summary>
    public enum CombatStance : byte
    {
        Alert,
        HoldGround
    }
}
