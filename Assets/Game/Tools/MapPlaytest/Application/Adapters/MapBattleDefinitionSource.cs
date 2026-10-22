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
            // 首版试玩每入口生成一只怪物，避免沿用压力测试的千只怪物挤在同一个入口。
            return new BattleDefinition(baseRules.Hero, baseRules.Goblin, baseRules.Runner, enemies.Count, 0,
                layout.columns * layout.cellWidth, layout.rows * layout.cellHeight, baseRules.FixedStep, baseRules.Seed,
                baseRules.Navigation, _map.CreateNavigationMap(_catalog), new MapPoint(hero.x, hero.y), enemies);
        }
    }
}
