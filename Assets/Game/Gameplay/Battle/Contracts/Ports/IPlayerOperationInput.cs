using System.Collections.Generic;
using Game.Battle.Contracts.Commands;

namespace Game.Battle.Contracts.Ports
{
    /// <summary>每画面帧追加离散操作；应用会话保留到下一个固定步，仅消费一次。</summary>
    public interface IPlayerOperationInput
    {
        void ReadOperations(List<PlayerOperation> destination);
        void ResetOperations();
    }
}
