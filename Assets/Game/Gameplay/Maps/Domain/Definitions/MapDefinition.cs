using System;
using Game.MapEngine.Domain;

namespace Game.Maps.Domain
{
    /// <summary>经校验的地图初始定义；对外只提供副本，加载和试玩都不能回写源文档。</summary>
    public sealed class MapDefinition
    {
        private readonly MapLayout _source;
        public string Id => _source.id;

        public MapDefinition(MapLayout layout, MapContentCatalog catalog)
        {
            var issues = MapContentValidator.Validate(layout, catalog);
            foreach (var issue in issues)
            {
                if (issue.IsError)
                    throw new ArgumentException(issue.Message, nameof(layout));
            }
            _source = layout.Copy();
        }

        public MapLayout CreateWorkingCopy() => _source.Copy();

        public GridMap CreateNavigationMap(MapContentCatalog catalog)
        {
            return MapSpatialRules.CreateNavigationMap(_source, catalog);
        }
    }
}
