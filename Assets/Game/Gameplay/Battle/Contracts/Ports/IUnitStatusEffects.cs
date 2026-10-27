using Game.Battle.Contracts.Commands;

namespace Game.Battle.Contracts.Ports
{
    /// <summary>固定步边界的共享效果入口；英雄、我方单位和敌方单位使用相同规则。</summary>
    public interface IUnitStatusEffects
    {
        /// <summary>目标不存在、死亡或时间参数无效时返回 false，不提交效果。</summary>
        bool TryApplyStatus(ApplyUnitStatusCommand command);
    }
}
