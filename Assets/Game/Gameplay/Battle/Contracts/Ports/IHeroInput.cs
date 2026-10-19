using System;
using System.Collections.Generic;
using Game.Battle.Contracts.Commands;

namespace Game.Battle.Contracts.Ports
{
    /// <summary>输入读取端口；键盘、手柄和触控统一转换为移动意图。</summary>
    public interface IHeroInput
    {
        MoveIntent Read();
    }
}
