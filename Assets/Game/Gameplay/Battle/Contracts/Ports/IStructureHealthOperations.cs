namespace Game.Battle.Contracts.Ports
{
    /// <summary>固定步之间的建筑伤害与修复入口。生命仍只由战斗模拟保存，损毁后不能修复。</summary>
    public interface IStructureHealthOperations
    {
        bool DamageStructure(string id, int amount);
        bool RepairStructure(string id, int amount);
    }
}
