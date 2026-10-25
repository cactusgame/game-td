using Game.Battle.Domain.ValueObjects;
using Game.MapEngine.Domain;

namespace Game.Battle.Contracts.Ports
{
    /// <summary>在固定步提交一种敌人，成功返回本会话唯一 ID，供波次归属和击杀奖励使用。</summary>
    public interface ILevelEnemySimulation
    {
        ulong SpawnEnemy(UnitKind kind, MapPoint entry);
    }
}
