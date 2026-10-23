using System.Collections.Generic;
using Game.Battle.Domain.Definitions;
using Game.Battle.Contracts.ReadModels;
using Game.MapEngine.Domain;

namespace Game.Battle.Contracts.Ports
{
    /// <summary>对局在固定步边界调用；建筑生命仅归模拟，波次进度仅归对局。</summary>
    public interface IDefenseBattleSimulation : ISpatialBattleSimulation
    {
        void SynchronizeStructures(IReadOnlyList<DefenseStructureDefinition> structures);
        void SpawnEnemies(IReadOnlyList<MapPoint> entries, int countPerEntry);
        void CopyStructures(List<DefenseStructureSnapshot> destination);
    }
}
