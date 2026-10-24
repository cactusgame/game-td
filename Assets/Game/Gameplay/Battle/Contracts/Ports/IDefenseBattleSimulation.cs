using System.Collections.Generic;
using Game.Battle.Domain.Definitions;
using Game.Battle.Contracts.ReadModels;
using Game.MapEngine.Domain;
using Game.Battle.Domain.ValueObjects;

namespace Game.Battle.Contracts.Ports
{
    /// <summary>对局在固定步边界调用；建筑生命仅归模拟，波次进度仅归对局。</summary>
    public interface IDefenseBattleSimulation : ISpatialBattleSimulation
    {
        void SynchronizeStructures(IReadOnlyList<DefenseStructureDefinition> structures);
        void SpawnEnemies(IReadOnlyList<MapPoint> entries, int countPerEntry);
        void CopyStructures(List<DefenseStructureSnapshot> destination);
        /// <summary>步边界记录外部目标已结算的攻击。动作时点和朝向仍由战斗模拟保存。</summary>
        void RecordExternalAttack(ulong attackerId, MapPoint target);
        /// <summary>营门外无净空时返回false，应用层保留已训练名额，不丢兵或重计冷却。</summary>
        bool TrySpawnSoldier(UnitKind kind, MapPoint exit, MapPoint rally, out ulong unitId);
    }
}
