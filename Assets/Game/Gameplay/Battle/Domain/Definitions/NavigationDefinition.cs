namespace Game.Battle.Domain.Definitions
{
    /// <summary>寻路与感知的只读配置；不存储某只怪物的当前路径。</summary>
    public sealed class NavigationDefinition
    {
        public float CellSize { get; }
        public float SeparationGap { get; }
        public float SeparationSpeed { get; }

        public float DetectionRadius { get; }

        public float LoseTargetRadius { get; }

        public float RepathSeconds { get; }

        public NavigationDefinition(float cellSize = 1, float detectionRadius = 5, float repathSeconds = 0.25f, float? loseTargetRadius = null, float separationGap = 0.12f, float separationSpeed = 3)
        {
            UnitDefinition.RequirePositive(cellSize, nameof(cellSize));
            UnitDefinition.RequirePositive(detectionRadius, nameof(detectionRadius));
            UnitDefinition.RequirePositive(repathSeconds, nameof(repathSeconds));
            UnitDefinition.RequirePositive(separationSpeed, nameof(separationSpeed));
            if (float.IsNaN(separationGap) || float.IsInfinity(separationGap) || separationGap < 0)
                throw new System.ArgumentOutOfRangeException(nameof(separationGap));
            SeparationGap = separationGap;
            SeparationSpeed = separationSpeed;
            float releaseRadius = loseTargetRadius ?? detectionRadius;
            UnitDefinition.RequirePositive(releaseRadius, nameof(loseTargetRadius));
            if (releaseRadius < detectionRadius)
                throw new System.ArgumentException("Lose-target radius must be at least detection radius.", nameof(loseTargetRadius));
            LoseTargetRadius = releaseRadius;
            CellSize = cellSize;
            DetectionRadius = detectionRadius;
            RepathSeconds = repathSeconds;
        }
    }
}
