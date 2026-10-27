namespace Game.Battle.Contracts.ReadModels
{
    /// <summary>固定步末的只读效果投影；权限由剩余时间派生，不保存第二份可变状态。</summary>
    public readonly struct UnitStatusSnapshot
    {
        public readonly double StunRemainingSeconds;
        public readonly double RootRemainingSeconds;
        public readonly double SilenceRemainingSeconds;
        public readonly double PoisonRemainingSeconds;

        public UnitStatusSnapshot(double stunRemainingSeconds, double rootRemainingSeconds,
            double silenceRemainingSeconds, double poisonRemainingSeconds)
        {
            StunRemainingSeconds = stunRemainingSeconds;
            RootRemainingSeconds = rootRemainingSeconds;
            SilenceRemainingSeconds = silenceRemainingSeconds;
            PoisonRemainingSeconds = poisonRemainingSeconds;
        }

        public bool IsStunned => StunRemainingSeconds > 0;
        public bool IsRooted => RootRemainingSeconds > 0;
        public bool IsSilenced => SilenceRemainingSeconds > 0;
        public bool IsPoisoned => PoisonRemainingSeconds > 0;
        public bool CanMove => !IsStunned && !IsRooted;
        public bool CanAttack => !IsStunned;
        public bool CanCast => !IsStunned && !IsSilenced;
    }
}
