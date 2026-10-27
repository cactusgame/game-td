using Unity.Entities;

namespace Game.Battle.Simulation.Components
{
    /// <summary>导航与普通攻击共用的目标真值；目标选择阶段写入，失效后再重选。</summary>
    public struct CombatTargetState : IComponentData
    {
        public Entity Target;
    }
}
