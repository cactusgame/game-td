using System.Collections.Generic;

namespace Game.Battle.Contracts.Ports
{
    /// <summary>在建设提交边界撤离售出兵营的士兵，不发放阵亡奖励。</summary>
    public interface ISoldierDismissal
    {
        void DismissSoldiers(IReadOnlyList<ulong> ids);
    }
}
