using System;
using System.Collections.Generic;
using Game.Battle.Domain.Definitions;

namespace Game.Battle.Contracts.Ports
{
    /// <summary>配置加载端口；调用方不依赖 JSON 或 TextAsset。</summary>
    public interface IBattleDefinitionSource
    {
        BattleDefinition Load();
    }
}
