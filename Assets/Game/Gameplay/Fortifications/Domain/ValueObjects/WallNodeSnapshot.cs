using System;
using Game.MapEngine.Domain;

namespace Game.Fortifications.Domain.ValueObjects
{
    public sealed class WallNodeSnapshot
    {
        public string Id { get; }
        public MapPoint Position { get; }

        public WallNodeSnapshot(string id, MapPoint position)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("墙节点必须具有稳定身份。", nameof(id));
            Id = id;
            Position = position;
        }
    }
}
