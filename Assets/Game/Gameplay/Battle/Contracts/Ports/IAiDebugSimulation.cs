using System.Collections.Generic;
using Game.Battle.Contracts.ReadModels;

namespace Game.Battle.Contracts.Ports
{
    /// <summary>按需复制 AI 诊断数据；关闭调试时无需调用，不参与玩法决策。</summary>
    public interface IAiDebugSimulation
    {
        void CopyAiDebug(List<UnitAiDebugSnapshot> destination);
    }
}
