using System;

namespace Game.MapEngine.Domain
{
    /// <summary>地图中的离散格子坐标，与 Unity Tilemap 的类型无关。</summary>
    public readonly struct GridCell : IEquatable<GridCell>
    {
        public int X { get; }

        public int Y { get; }

        public GridCell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(GridCell other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridCell other && Equals(other);
        public override int GetHashCode() => unchecked(X * 397 ^ Y);
        public override string ToString() => $"({X}, {Y})";
        public static bool operator ==(GridCell left, GridCell right) => left.Equals(right);
        public static bool operator !=(GridCell left, GridCell right) => !left.Equals(right);
    }
}
