using System;
using System.Collections.Generic;
using Game.Maps.Domain;
using UnityEngine;

namespace Game.Infrastructure.Maps.Serialization
{
    /// <summary>仅在导入边界兼容旧单门数据；正式文档只输出整数 Tile 的 gates 列表。</summary>
    internal static class MapGateMigration
    {
        public static void Read(string json, MapLayout layout)
        {
            var source = JsonUtility.FromJson<GateDocument>(json);
            foreach (var edge in layout.wallEdges)
            {
                if (edge == null)
                    continue;
                var raw = source.wallEdges?.Find(item => item.id == edge.id);
                edge.gates ??= new List<WallGate>();
                if (raw?.gates != null)
                {
                    foreach (var gate in raw.gates)
                    {
                        if (!Integer(gate.startTile) || !Integer(gate.widthTiles) || !Integer(gate.openingTiles))
                            throw new FormatException("城门位置与宽度必须是整数 Tile：" + gate.id);
                    }
                }
                if (edge.gates.Count > 0 || raw == null || !raw.hasGate)
                    continue;
                var old = raw.gate ?? new LegacyGate();
                var a = layout.wallNodes.Find(node => node?.id == edge.from);
                var b = layout.wallNodes.Find(node => node?.id == edge.to);
                float dx = a == null || b == null ? 1 : b.x - a.x;
                float dy = a == null || b == null ? 0 : b.y - a.y;
                double length = Math.Sqrt(dx * dx + dy * dy);
                double gridLength = Math.Sqrt(dx * dx / (layout.cellWidth * layout.cellWidth) + dy * dy / (layout.cellHeight * layout.cellHeight));
                double tile = gridLength > 0 ? length / gridLength : Math.Min(layout.cellWidth, layout.cellHeight);
                if (!Finite(old.width) || !Finite(old.openingWidth) || !Finite(old.position) || tile <= 0)
                    throw new FormatException("旧城门尺寸或位置无效：" + edge.id);
                int opening = Math.Max(1, (int)Math.Round(old.openingWidth / tile));
                int width = Math.Max(opening + 2, (int)Math.Ceiling(old.width / tile));
                if ((width - opening) % 2 != 0)
                    width++;
                double center = old.position * length;
                if (a != null && b != null && (a.x > b.x || a.x == b.x && a.y > b.y))
                    center = length - center;
                edge.gates.Add(new WallGate
                {
                    id = edge.id + "/gate",
                    startTile = (int)Math.Round(center / tile - width * 0.5, MidpointRounding.AwayFromZero),
                    widthTiles = width, openingTiles = opening, isOpen = old.isOpen
                });
            }
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool Integer(double value) => Finite(value) && value == Math.Truncate(value) && value >= int.MinValue && value <= int.MaxValue;

        [Serializable] private sealed class GateDocument { public List<EdgeDocument> wallEdges; }
        [Serializable] private sealed class EdgeDocument
        {
            public string id;
            public bool hasGate;
            public LegacyGate gate;
            public List<GateNumbers> gates;
        }
        [Serializable] private sealed class GateNumbers
        {
            public string id;
            public double startTile;
            public double widthTiles;
            public double openingTiles;
        }
        [Serializable] private sealed class LegacyGate
        {
            public float position = 0.5f;
            public float width = 4.5f;
            public float openingWidth = 2;
            public bool isOpen;
        }
    }
}
