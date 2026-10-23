using Game.Battle.Domain.Definitions;
using Game.MapEngine.Domain;

namespace Game.Battle.Contracts.Ports
{
    /// <summary>地图集成使用的模拟端口。替换导航与碰撞快照，在下一次 Step 前完成同步。</summary>
    public interface ISpatialBattleSimulation : IBattleSimulation
    {
        void UpdateNavigationMap(GridMap map, NavigationDefinition navigation, GridMap friendlyMap = null);
    }
}
