using Game.MapEngine.Domain;

namespace Game.Tools.MapPlaytest.Application.ReadModels
{
    /// <summary>由关卡定义和当前波次进度生成的出生口预览，不拥有可变玩法状态。</summary>
    public readonly struct WavePreview
    {
        public int WaveIndex { get; }
        public string EntryId { get; }
        public MapPoint Position { get; }
        public string EnemyId { get; }
        public int Count { get; }

        public WavePreview(int waveIndex, string entryId, MapPoint position, string enemyId, int count)
        {
            WaveIndex = waveIndex;
            EntryId = entryId;
            Position = position;
            EnemyId = enemyId;
            Count = count;
        }
    }
}
