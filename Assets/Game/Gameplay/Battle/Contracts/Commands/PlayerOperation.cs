namespace Game.Battle.Contracts.Commands
{
    /// <summary>设备无关的操作；短按与长按已由输入适配器判定。</summary>
    public enum PlayerOperation
    {
        SkillOne,
        SkillTwo,
        NearbyTap,
        NearbyHold,
        NearbyRelease,
        CallOne,
        CallAll,
        CallOneMelee,
        CallAllMelee,
        CallOneRanged,
        CallAllRanged,
        Cancel
    }
}
