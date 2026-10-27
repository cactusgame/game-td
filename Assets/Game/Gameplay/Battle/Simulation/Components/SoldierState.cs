using Game.Battle.Domain.ValueObjects;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Components
{
    /// <summary>
    /// 士兵 AI 的权威状态。桥接提交选择、命令、姿态及初始行为；导航裁决行为，战斗阶段可进入攻击并记录反击来源。
    /// IsSelected 仅表示玩家选中，AI 不读取它。人物效果另由 UnitStatusState 保存。
    /// </summary>
    public struct SoldierState : IComponentData
    {
        public float2 Rally;
        public float2 FollowOffset;
        public bool IsSelected;
        public SoldierCommand Command;
        public CombatStance Stance;
        // Regrouping 持续到抵达命令目标，战斗阶段不得打断。
        public UnitBehavior Behavior;
        // 导航按警戒配置及编队偏移派生，战斗阶段只读；0表示非跟随作战。
        public float FollowCombatRadius;
        // 最近攻击自己的怪物；战斗阶段记录，下一导航步消费，不由表现维护。
        public Entity RetaliationTarget;
    }
}
