using System;

namespace Game.MapEngine.Domain
{
    /// <summary>
    /// 不可变寻路地图定义：格子尺寸、地面原点和移动通行性。
    /// 创作工具以后导出这些数据即可；此模型不加载、不绘制 Tilemap，不持有单位状态。
    /// </summary>
    public sealed class GridMap
    {
        public const int MaxCellCount = 1000000;
        private readonly bool[] _walkable;

        public int Columns { get; }
        public int Rows { get; }
        public int CellCount => _walkable.Length;
        public float CellWidth { get; }
        public float CellHeight { get; }
        public float OriginX { get; }
        public float OriginY { get; }
        public float Width => Columns * CellWidth;
        public float Height => Rows * CellHeight;

        // 通行数据采用 y * Columns + x 排列；复制输入，防止编辑文档改变正式对局地图。
        public GridMap(int columns, int rows, float cellWidth, float cellHeight,
            float originX, float originY, bool[] walkable = null)
        {
            if (columns <= 0 || rows <= 0 || (long)columns * rows > MaxCellCount)
                throw new ArgumentOutOfRangeException(nameof(columns), "Grid dimensions exceed the supported cell count.");
            RequireFinite(cellWidth, nameof(cellWidth));
            RequireFinite(cellHeight, nameof(cellHeight));
            RequireFinite(originX, nameof(originX));
            RequireFinite(originY, nameof(originY));
            if (cellWidth <= 0 || cellHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(cellWidth), "Cell dimensions must be positive.");
            RequireFinite(originX + columns * cellWidth, nameof(cellWidth));
            RequireFinite(originY + rows * cellHeight, nameof(cellHeight));
            int count = columns * rows;
            if (walkable != null && walkable.Length != count)
                throw new ArgumentException("Walkability count must match the grid dimensions.", nameof(walkable));

            Columns = columns;
            Rows = rows;
            CellWidth = cellWidth;
            CellHeight = cellHeight;
            OriginX = originX;
            OriginY = originY;
            _walkable = walkable != null ? (bool[])walkable.Clone() : new bool[count];
            if (walkable == null)
                for (int i = 0; i < count; i++) _walkable[i] = true;
        }

        /// <summary>当前原型入口：全通行、覆盖整个场地，原点位于场地左下角。</summary>
        public static GridMap CreateOpenArena(float width, float height, float preferredCellSize)
        {
            RequireFinite(width, nameof(width));
            RequireFinite(height, nameof(height));
            RequireFinite(preferredCellSize, nameof(preferredCellSize));
            if (width <= 0 || height <= 0 || preferredCellSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(preferredCellSize));
            double columns = Math.Ceiling((double)width / preferredCellSize);
            double rows = Math.Ceiling((double)height / preferredCellSize);
            if (columns * rows > MaxCellCount)
                throw new ArgumentOutOfRangeException(nameof(preferredCellSize), "Cell size creates too many cells.");

            // 不能整除时略微缩小实际格宽/格高，避免地图超出场地边界或留下半格空隙。
            int columnCount = (int)columns;
            int rowCount = (int)rows;
            return new GridMap(columnCount, rowCount, width / columnCount, height / rowCount,
                -width * 0.5f, -height * 0.5f);
        }

        public bool Contains(GridCell cell)
        {
            return cell.X >= 0 && cell.X < Columns && cell.Y >= 0 && cell.Y < Rows;
        }

        public bool IsWalkable(GridCell cell)
        {
            return Contains(cell) && _walkable[cell.Y * Columns + cell.X];
        }

        public int IndexOf(GridCell cell)
        {
            if (!Contains(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
            return cell.Y * Columns + cell.X;
        }

        public GridCell CellAt(int index)
        {
            if (index < 0 || index >= CellCount) throw new ArgumentOutOfRangeException(nameof(index));
            return new GridCell(index % Columns, index / Columns);
        }

        public MapPoint CellCenter(GridCell cell)
        {
            if (!Contains(cell)) throw new ArgumentOutOfRangeException(nameof(cell));
            return new MapPoint(OriginX + (cell.X + 0.5f) * CellWidth,
                OriginY + (cell.Y + 0.5f) * CellHeight);
        }

        // 左/下边界包含，右/上边界不包含；不把地图外的坐标悄悄夹到某个合法格子。
        public bool TryWorldToCell(float x, float y, out GridCell cell)
        {
            cell = default;
            if (float.IsNaN(x) || float.IsNaN(y) || x < OriginX || y < OriginY
                || x >= OriginX + Width || y >= OriginY + Height)
                return false;
            cell = new GridCell((int)Math.Floor((x - OriginX) / CellWidth),
                (int)Math.Floor((y - OriginY) / CellHeight));
            return Contains(cell);
        }

        /// <summary>八邻接移动；对角步不能穿过两个直邻格形成的阻挡拐角。</summary>
        public bool CanStep(GridCell from, GridCell to)
        {
            if (!IsWalkable(from) || !IsWalkable(to)) return false;
            int dx = Math.Abs(to.X - from.X);
            int dy = Math.Abs(to.Y - from.Y);
            if (dx > 1 || dy > 1 || dx + dy == 0) return false;
            return dx == 0 || dy == 0 ||
                (IsWalkable(new GridCell(to.X, from.Y)) && IsWalkable(new GridCell(from.X, to.Y)));
        }

        private static void RequireFinite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name, "Grid geometry must be finite.");
        }
    }
}
