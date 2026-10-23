using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Fortifications.Domain.ValueObjects
{
    public sealed class WallEdgeSnapshot
    {
        public string Id { get; }
        public string From { get; }
        public string To { get; }
        public IReadOnlyList<WallGateSnapshot> Gates { get; }

        public WallEdgeSnapshot(string id, string from, string to, IEnumerable<WallGateSnapshot> gates = null)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to) || from == to)
                throw new ArgumentException("墙段必须有稳定身份并连接两个不同节点。");
            Id = id;
            From = from;
            To = to;
            Gates = Array.AsReadOnly((gates ?? Array.Empty<WallGateSnapshot>()).ToArray());
        }
    }
}
