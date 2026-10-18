namespace Game.Battle.Domain
{
    /// <summary>寻路与感知的只读配置；不存储某只怪物的当前路径。</summary>
    public sealed class NavigationDefinition
    {
        public float CellSize { get; }
        public float DetectionRadius { get; }
        public float RepathSeconds { get; }

        public NavigationDefinition(float cellSize = 1, float detectionRadius = 5, float repathSeconds = 0.25f)
        {
            UnitDefinition.RequirePositive(cellSize, nameof(cellSize));
            UnitDefinition.RequirePositive(detectionRadius, nameof(detectionRadius));
            UnitDefinition.RequirePositive(repathSeconds, nameof(repathSeconds));
            CellSize = cellSize;
            DetectionRadius = detectionRadius;
            RepathSeconds = repathSeconds;
        }
    }
}
