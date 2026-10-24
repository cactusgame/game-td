namespace Game.Battle.Domain.ValueObjects
{
    /// <summary>分类只用于阵营与能力判断，不能把所有非英雄对象都当作敌人。</summary>
    public static class UnitKinds
    {
        public static bool IsEnemy(UnitKind kind) => kind == UnitKind.Goblin || kind == UnitKind.Runner;
        public static bool IsSoldier(UnitKind kind) => kind == UnitKind.Soldier || kind == UnitKind.Archer;
        public static bool IsFriendly(UnitKind kind) => kind == UnitKind.Hero || IsSoldier(kind);
    }
}
