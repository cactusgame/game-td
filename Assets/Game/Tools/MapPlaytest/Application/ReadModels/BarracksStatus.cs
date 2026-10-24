namespace Game.Tools.MapPlaytest.Application.ReadModels
{
    /// <summary>从建设编制和战斗快照派生的查询结果，不保存第二份士兵状态。</summary>
    public readonly struct BarracksStatus
    {
        public string Id { get; }
        public string BuildingId { get; }
        public int Level { get; }
        public int Capacity { get; }
        public int Alive { get; }
        public int Training { get; }
        public int WaitingAtExit { get; }
        public bool IsDestroyed { get; }
        public double NextReadySeconds { get; }
        public BarracksStatus(string id, string buildingId, int level, int capacity, int alive,
            int training, int waitingAtExit, bool isDestroyed, double nextReadySeconds)
        {
            Id = id;
            BuildingId = buildingId;
            Level = level;
            Capacity = capacity;
            Alive = alive;
            Training = training;
            WaitingAtExit = waitingAtExit;
            IsDestroyed = isDestroyed;
            NextReadySeconds = nextReadySeconds;
        }
    }
}
