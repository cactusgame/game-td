using System;
using System.Collections.Generic;
using System.Linq;
using Game.Fortifications.Domain.Definitions;
using Game.Fortifications.Domain.ValueObjects;
using Game.MapEngine.Domain.Spatial;

namespace Game.Fortifications.Domain.Rules
{
    /// <summary>沿墙扣除各座已开门洞。网格保守投影仍由地图引擎执行，不读取渲染轮廓。</summary>
    public static class WallObstacleRules
    {
        public static IEnumerable<ObstacleFootprint> BuildNavigationObstacles(WallNetworkSnapshot network,
            WallDefinition definition, float envelopeWidth)
        {
            foreach (var edge in network.Edges)
            {
                var probe = new WallGateSnapshot("probe", 0, 4, 2, false);
                if (!WallGateRules.TryGetSpan(network, edge, probe, out var axis))
                    continue;
                var a = network.FindNode(edge.From).Position;
                var b = network.FindNode(edge.To).Position;
                float dx = b.X - a.X;
                float dy = b.Y - a.Y;
                float length = (float)Math.Sqrt(dx * dx + dy * dy);
                float cursor = 0;
                if (WallGateRules.Validate(network, edge, definition, out _))
                {
                    foreach (var gate in edge.Gates.Where(item => item.IsOpen).OrderBy(item => item.StartTile))
                    {
                        WallGateRules.TryGetSpan(network, edge, gate, out var span);
                        if (span.OpeningStart > cursor)
                            yield return ObstacleFootprint.Segment(axis.At(cursor), axis.At(span.OpeningStart), envelopeWidth);
                        cursor = span.OpeningEnd;
                    }
                }
                if (cursor < length)
                    yield return ObstacleFootprint.Segment(axis.At(cursor), axis.At(length), envelopeWidth);
            }
        }
    }
}
