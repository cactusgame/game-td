namespace Game.MapEngine.Domain
{
    public enum TerrainKind
    {
        Grass,
        Dirt,
        Road,
        Water,
        Rock,
        Tree
    }

    /// <summary>首版地形语义；外观资源替换不会改变通行和建设许可。</summary>
    public static class TerrainRules
    {
        public static bool IsWalkable(TerrainKind terrain)
        {
            return terrain == TerrainKind.Grass || terrain == TerrainKind.Dirt || terrain == TerrainKind.Road;
        }

        public static bool IsBuildable(TerrainKind terrain)
        {
            return IsWalkable(terrain);
        }

        public static bool BlocksShot(TerrainKind terrain)
        {
            return terrain == TerrainKind.Rock || terrain == TerrainKind.Tree;
        }
    }
}
