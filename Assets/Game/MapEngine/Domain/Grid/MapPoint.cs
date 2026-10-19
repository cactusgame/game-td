using System;

namespace Game.MapEngine.Domain
{
    /// <summary>连续 XY 地面坐标；不是 Transform，也不是格子索引。</summary>
    public readonly struct MapPoint : IEquatable<MapPoint>
    {
        public float X { get; }

        public float Y { get; }

        public MapPoint(float x, float y)
        {
            if (float.IsNaN(x) || float.IsInfinity(x))
            {
                throw new ArgumentOutOfRangeException(nameof(x), "Ground coordinates must be finite.");
            }

            if (float.IsNaN(y) || float.IsInfinity(y))
            {
                throw new ArgumentOutOfRangeException(nameof(y), "Ground coordinates must be finite.");
            }

            X = x;
            Y = y;
        }

        public bool Equals(MapPoint other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is MapPoint other && Equals(other);
        public override int GetHashCode() => unchecked(X.GetHashCode() * 397 ^ Y.GetHashCode());
        public static bool operator ==(MapPoint left, MapPoint right) => left.Equals(right);
        public static bool operator !=(MapPoint left, MapPoint right) => !left.Equals(right);
    }
}
