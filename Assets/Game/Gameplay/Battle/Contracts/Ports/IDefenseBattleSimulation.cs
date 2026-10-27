using System.Collections.Generic;
using System;
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
        /// <summary>固定步边界检查共享冷却，执行同步伤害提交；成功后才消耗冷却并发布动作。
        /// commitDamage 返回 false 或抛出异常时不消耗冷却；回调不得推进模拟或发起其他攻击。</summary>
        bool TryAttackExternalTarget(ulong attackerId, MapPoint target, double simulationTime, Func<int, bool> commitDamage, string targetId = null);
        /// <summary>营门外无净空时返回false，应用层保留已训练名额，不丢兵或重计冷却。</summary>
        bool TrySpawnSoldier(UnitKind kind, MapPoint exit, MapPoint rally, out ulong unitId);
    }
}
