namespace Game.Battle.Contracts.ReadModels
{
    /// <summary>战斗拥有的建筑生命快照；身份对应建设实例 ID。</summary>
    public readonly struct DefenseStructureSnapshot
    {
        public string Id { get; }
        public int Health { get; }
        public int MaxHealth { get; }
        public bool IsMainCastle { get; }

        public DefenseStructureSnapshot(string id, int health, bool isMainCastle, int maxHealth = 0)
        {
            Id = id;
            Health = health;
            MaxHealth = maxHealth;
            IsMainCastle = isMainCastle;
        }
    }
}
