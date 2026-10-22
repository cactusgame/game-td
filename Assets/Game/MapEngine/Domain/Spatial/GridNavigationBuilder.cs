using System;
using System.Collections.Generic;

namespace Game.MapEngine.Domain.Spatial
{
    /// <summary>将地形通行数据和通用障碍投影为不可变导航快照，不拥有障碍的业务生命周期。</summary>
    public static class GridNavigationBuilder
    {
        public static GridMap Build(GridMap geometry, IReadOnlyList<bool> terrainWalkable, IReadOnlyList<ObstacleFootprint> obstacles)
        {
            if (geometry == null || terrainWalkable == null || obstacles == null)
                throw new ArgumentNullException();
            if (terrainWalkable.Count != geometry.CellCount)
                throw new ArgumentException("Terrain walkability must match grid dimensions.");
            var walkable = new bool[geometry.CellCount];
            for (int i = 0; i < walkable.Length; i++)
            {
                walkable[i] = terrainWalkable[i];
                if (!walkable[i])
                    continue;
                var cell = geometry.CellAt(i);
                foreach (var obstacle in obstacles)
                {
                    if (!obstacle.BlocksCell(geometry, cell))
                        continue;
                    walkable[i] = false;
                    break;
                }
            }
            return new GridMap(geometry.Columns, geometry.Rows, geometry.CellWidth, geometry.CellHeight,
                geometry.OriginX, geometry.OriginY, walkable);
        }
    }
}
