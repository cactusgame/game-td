namespace Game.Battle.Domain.ValueObjects
{
    /// <summary>可同时存在的单位效果种类，不是互斥的 AI 行为。</summary>
    public enum UnitStatusKind : byte
    {
        Stun,
        Root,
        Silence,
        Poison
    }
}
