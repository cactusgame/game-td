using System.Collections.Generic;
using Game.Battle.Contracts.Ports;
using Game.Battle.Domain.Definitions;
using Game.MapEngine.Domain;
using Game.Maps.Domain;

namespace Game.Tools.MapPlaytest.Application.Adapters
{
    /// <summary>地图与战斗集成：空间与出生位置来自地图，单位属性来自现有战斗 JSON。</summary>
    public sealed class MapBattleDefinitionSource : IBattleDefinitionSource
    {
        private readonly MapDefinition _map;
        private readonly MapContentCatalog _catalog;
        private readonly BattleDefinition _baseRules;
        public MapBattleDefinitionSource(MapDefinition map, MapContentCatalog catalog, BattleDefinition baseRules)
        {
            _map = map;
            _catalog = catalog;
            _baseRules = baseRules;
        }

        public BattleDefinition Load()
        {
            var baseRules = _baseRules;
            var layout = _map.CreateWorkingCopy();
            MapPlayabilityValidator.RequirePlayable(layout, _catalog, baseRules.Hero.Radius,
                System.Math.Max(baseRules.Goblin.Radius, baseRules.Runner.Radius));
            var hero = layout.markers.Find(marker => marker.kind == MarkerKind.HeroSpawn);
            var enemies = new List<MapPoint>();
            foreach (var marker in layout.markers)
            {
                if (marker.kind == MarkerKind.EnemyEntry)
                    enemies.Add(new MapPoint(marker.x, marker.y));
            }
            // 怪物由对局波次在固定步边界生成，初始化不提前生成。
            return new BattleDefinition(baseRules.Hero, baseRules.Goblin, baseRules.Runner, 0, 0,
                layout.columns * layout.cellWidth, layout.rows * layout.cellHeight, baseRules.FixedStep, baseRules.Seed,
                baseRules.Navigation, _map.CreateNavigationMap(_catalog), new MapPoint(hero.x, hero.y), enemies, baseRules.Soldiers);
        }
    }
}
