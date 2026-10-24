using System;
using Game.MapEngine.Domain;

namespace Game.Battle.Domain.ValueObjects
{
    /// <summary>单位的领域分类；与 Unity Entity 或场景对象身份无关。</summary>
    public enum UnitKind : byte
    {
        Hero,
        Goblin,
        Runner,
        Soldier,
        Archer
    }
}
