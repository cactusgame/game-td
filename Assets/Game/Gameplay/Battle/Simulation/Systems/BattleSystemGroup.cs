using Unity.Entities;

namespace Game.Battle.Simulation.Systems
{
    /// <summary>手动驱动的战斗系统组，按显式更新约束排序，不自动接入 PlayerLoop。</summary>
    [DisableAutoCreation]
    public partial class BattleSystemGroup : ComponentSystemGroup
    {
    }
}
