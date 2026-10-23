using Game.Fortifications.Domain.Rules;
using Game.Fortifications.Domain.ValueObjects;
using UnityEngine;

namespace Game.Fortifications.Unity.Views
{
    /// <summary>把纯工事中心线转换为 Unity 坐标，编辑器与游戏共用。</summary>
    public static class WallPresentation
    {
        public static bool TryGetSpan(WallNetworkSnapshot network, WallEdgeSnapshot edge, out Vector2 start, out Vector2 end)
        {
            bool found = WallConnectionRules.TryGetSpan(network, edge, out var a, out var b);
            start = new Vector2(a.X, a.Y);
            end = new Vector2(b.X, b.Y);
            return found;
        }
    }
}
