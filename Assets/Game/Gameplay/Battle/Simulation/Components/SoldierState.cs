using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Components
{
    /// <summary>战斗领域拥有选择、跟随偏移、待机点与反击来源；IsSelected 表示跟随，IsStationed 表示死守，否则待机为警戒。</summary>
    public struct SoldierState : IComponentData
    {
        public float2 Rally;
        public float2 FollowOffset;
        public bool IsSelected;
        // 只有玩家明确长按R死守才为true；兵营出兵、短按R取消跟随均为警戒。
        public bool IsStationed;
        public bool HasPlayerOrder;
        // 玩家呼叫或离队过远后优先归位，直到到达编队点；战斗阶段不得打断。
        public bool IsRegrouping;
        // 导航按警戒配置及编队偏移派生，战斗阶段只读；0表示非跟随作战。
        public float FollowCombatRadius;
        // 最近攻击自己的怪物；战斗阶段记录，下一导航步消费，不由表现维护。
        public Entity RetaliationTarget;
    }
}
