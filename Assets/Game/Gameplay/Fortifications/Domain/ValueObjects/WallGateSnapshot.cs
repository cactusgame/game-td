namespace Game.Fortifications.Domain.ValueObjects
{
    /// <summary>整数 Tile 门框区间；实际长度由墙向和地形格度量解析，不引用素材。</summary>
    public sealed class WallGateSnapshot
    {
        public string Id { get; }
        public int StartTile { get; }
        public int WidthTiles { get; }
        public int OpeningTiles { get; }
        public bool IsOpen { get; }
        public bool IsDestroyed { get; }

        public WallGateSnapshot(string id, int startTile, int widthTiles, int openingTiles, bool isOpen, bool isDestroyed = false)
        {
            Id = id;
            StartTile = startTile;
            WidthTiles = widthTiles;
            OpeningTiles = openingTiles;
            IsOpen = isOpen;
            IsDestroyed = isDestroyed;
        }

        public WallGateSnapshot WithOpen(bool isOpen)
        {
            return new WallGateSnapshot(Id, StartTile, WidthTiles, OpeningTiles, isOpen, IsDestroyed);
        }
    }
}
