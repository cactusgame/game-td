namespace Game.Battle.Contracts.ReadModels
{
    public readonly struct PlayerControlSnapshot
    {
        public readonly bool IsSelectingWhileMoving;
        public readonly float SelectionRadius;
        public readonly int SelectedCount;
        public readonly double SkillOneRemaining, SkillTwoRemaining;
        public PlayerControlSnapshot(bool selecting, float radius, int count, double first, double second)
        {
            IsSelectingWhileMoving = selecting;
            SelectionRadius = radius;
            SelectedCount = count;
            SkillOneRemaining = first;
            SkillTwoRemaining = second;
        }
    }
}
