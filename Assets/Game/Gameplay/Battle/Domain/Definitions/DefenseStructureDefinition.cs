using System;

namespace Game.Battle.Domain.Definitions
{
    /// <summary>地图集成导出的建筑战斗能力与地面矩形，不持有运行生命。</summary>
    public sealed class DefenseStructureDefinition
    {
        public string Id { get; }
        public bool IsMainCastle { get; }
        public float MinX { get; }
        public float MinY { get; }
        public float MaxX { get; }
        public float MaxY { get; }
        public int MaxHealth { get; }
        public int Attack { get; }
        public float Range { get; }
        public float AttackInterval { get; }

        public DefenseStructureDefinition(string id, bool isMainCastle, float minX, float minY,
            float maxX, float maxY, int maxHealth, int attack, float range, float attackInterval)
        {
            if (string.IsNullOrWhiteSpace(id) || maxHealth <= 0 || attack < 0 ||
                !(maxX > minX) || !(maxY > minY) || !(range >= 0) || !(attackInterval > 0))
                throw new ArgumentException("Invalid defense structure.");
            Id = id;
            IsMainCastle = isMainCastle;
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
            MaxHealth = maxHealth;
            Attack = attack;
            Range = range;
            AttackInterval = attackInterval;
        }
    }
}
