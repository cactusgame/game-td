namespace Game.Fortifications.Domain.ValueObjects
{
    /// <summary>只读门状态。创建有效门的规则由 WallGateRules 执行。</summary>
    public sealed class WallGateSnapshot
    {
        public float Position { get; }
        public float Width { get; }
        public float OpeningWidth { get; }
        public bool IsOpen { get; }

        public WallGateSnapshot(float position, float width, float openingWidth, bool isOpen)
        {
            Position = position;
            Width = width;
            OpeningWidth = openingWidth;
            IsOpen = isOpen;
        }

        public WallGateSnapshot WithOpen(bool isOpen)
        {
            return new WallGateSnapshot(Position, Width, OpeningWidth, isOpen);
        }
    }
}
