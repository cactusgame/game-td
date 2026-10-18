namespace Game.MapEngine.Domain
{
    /// <summary>连续 XY 地面坐标；不是 Transform，也不是格子索引。</summary>
    public readonly struct MapPoint
    {
        public float X { get; }
        public float Y { get; }

        public MapPoint(float x, float y)
        {
            X = x;
            Y = y;
        }
    }
}
