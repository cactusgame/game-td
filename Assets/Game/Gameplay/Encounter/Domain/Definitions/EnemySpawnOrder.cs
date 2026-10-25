namespace Game.Encounter.Domain.Definitions
{
    public readonly struct EnemySpawnOrder
    {
        public readonly int WaveIndex, GroupIndex, Sequence;
        public readonly string EntryId, EnemyId;
        public EnemySpawnOrder(int waveIndex, int groupIndex, int sequence, string entryId, string enemyId)
        {
            WaveIndex = waveIndex;
            GroupIndex = groupIndex;
            Sequence = sequence;
            EntryId = entryId;
            EnemyId = enemyId;
        }
    }
}
