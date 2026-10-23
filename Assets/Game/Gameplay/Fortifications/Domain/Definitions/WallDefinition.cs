using System;

namespace Game.Fortifications.Domain.Definitions
{
    /// <summary>工事的只读尺寸，使用世界地面单位；更换素材或地形格大小不会改写它。</summary>
    public sealed class WallDefinition
    {
        public static WallDefinition Default { get; } = new WallDefinition(0.52f, 0.5f, 0.4f);

        public float Width { get; }
        public float Height { get; }
        public float JunctionRadius { get; }
        public float JunctionClearance { get; }

        public WallDefinition(float width, float height, float junctionRadius, float junctionClearance = 0.01875f)
        {
            if (!Positive(width) || !Positive(height) || !Positive(junctionRadius) || !Positive(junctionClearance) || junctionRadius <= width * 0.5f)
            {
                throw new ArgumentException("墙宽、墙高必须为有限正数，连接底座半径必须大于墙半宽。");
            }

            Width = width;
            Height = height;
            JunctionRadius = junctionRadius;
            JunctionClearance = junctionClearance;
        }

        private static bool Positive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0;
        }
    }
}
