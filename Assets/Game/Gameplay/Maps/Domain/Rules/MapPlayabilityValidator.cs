using System;
using System.Collections.Generic;
namespace Game.Maps.Domain
{
    /// <summary>当前塔防试玩的进入条件。先检查地图结构，再检查出生数量、通行与单位净空。</summary>
    public static class MapPlayabilityValidator
    {
        public static List<MapIssue> Validate(MapLayout layout, MapContentCatalog catalog, float heroRadius = 0, float enemyRadius = 0)
        {
            var issues = MapContentValidator.Validate(layout, catalog);
            if (issues.Exists(issue => issue.IsError))
                return issues;
            int heroCount = 0;
            int entryCount = 0;
            var navigation = MapSpatialRules.CreateNavigationMap(layout, catalog);
            foreach (var marker in layout.markers)
            {
                if (marker.kind == MarkerKind.HeroSpawn)
                    heroCount++;
                else if (marker.kind == MarkerKind.EnemyEntry)
                    entryCount++;
                if (!Finite(marker.x) || !Finite(marker.y) || !navigation.TryWorldToCell(marker.x, marker.y, out var cell) || !navigation.IsWalkable(cell))
                    issues.Add(new MapIssue("入口或出生位置必须位于地图内的可通行位置", objectId: marker.id));
                else if (!navigation.CanOccupy(marker.x, marker.y, marker.kind == MarkerKind.HeroSpawn ? heroRadius : enemyRadius))
                    issues.Add(new MapIssue("出生位置净空不足，单位会与障碍或边界重叠", cell.X, cell.Y, marker.id));
            }
            if (heroCount != 1)
                issues.Add(new MapIssue("地图必须有且只有一个英雄出生位置"));
            if (entryCount == 0)
                issues.Add(new MapIssue("地图至少需要一个敌人入口"));
            // 无路不自动判为非法，后续破墙策略可以解释这种地图。
            return issues;
        }

        public static void RequirePlayable(MapLayout layout, MapContentCatalog catalog, float heroRadius = 0, float enemyRadius = 0)
        {
            foreach (var issue in Validate(layout, catalog, heroRadius, enemyRadius))
                if (issue.IsError)
                    throw new ArgumentException(issue.Message, nameof(layout));
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
