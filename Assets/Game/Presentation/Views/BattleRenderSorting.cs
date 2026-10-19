using UnityEngine;

namespace Game.Presentation.Views
{
    /// <summary>地面、阴影与单位分层；Y 深度只比较单位之间的前后，不再与地面竞争。</summary>
    public static class BattleRenderSorting
    {
        public const string GroundLayer = "Battle Ground";
        public const string ShadowLayer = "Battle Shadows";
        public const string UnitLayer = "Battle Units";
        public static int UnitDepth(float groundY)
        {
            // SpriteRenderer 的排序范围是有符号 16 位，预留血条的两个偏移。
            return Mathf.Clamp(Mathf.RoundToInt(-groundY * 50), short.MinValue, short.MaxValue - 2);
        }
    }
}
