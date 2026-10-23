using Game.MapEngine.Domain;

namespace Game.Fortifications.Domain.ValueObjects
{
    /// <summary>门沿规范墙向的世界长度区间；规范起点取坐标较小端，反向绘墙不会翻转门位置。</summary>
    public readonly struct WallGateSpan
    {
        public MapPoint Origin { get; }
        public MapPoint Direction { get; }
        public float Start { get; }
        public float End { get; }
        public float OpeningStart { get; }
        public float OpeningEnd { get; }
        public float TileLength { get; }
        public MapPoint Center => At((Start + End) * 0.5f);

        public WallGateSpan(MapPoint origin, MapPoint direction, float start, float end, float openingStart, float openingEnd, float tileLength)
        {
            Origin = origin;
            Direction = direction;
            Start = start;
            End = end;
            OpeningStart = openingStart;
            OpeningEnd = openingEnd;
            TileLength = tileLength;
        }

        public MapPoint At(float distance)
        {
            return new MapPoint(Origin.X + Direction.X * distance, Origin.Y + Direction.Y * distance);
        }
    }
}
