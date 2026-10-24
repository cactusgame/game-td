namespace Game.Construction.Domain.Definitions
{
    /// <summary>建设权限与功能分类；地图预放不受玩家建设权限限制。</summary>
    public enum BuildingCategory
    {
        PlayerBuildable,
        Decoration,
        BuiltIn
    }
}
