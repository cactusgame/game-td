using System;

namespace Game.Fortifications.Domain.Definitions
{
    /// <summary>建设与发展中工事的只读尺寸和基础生命，不保存墙段或城门的当前血量。</summary>
    public sealed class WallDefinition
    {
        public static WallDefinition Default { get; } = new WallDefinition(0.52f, 0.5f, 0.4f);

        public int GateHealth { get; }
        public int WallHealth { get; }
        public float GateAttackInterval { get; }
        public float Width { get; }
        public float Height { get; }
        public float JunctionRadius { get; }
        public float JunctionClearance { get; }

        public WallDefinition(float width, float height, float junctionRadius, float junctionClearance = 0.01875f, int gateHealth = 100, float gateAttackInterval = 1, int wallHealth = 300)
        {
            if (!Positive(width) || !Positive(height) || !Positive(junctionRadius) || !Positive(junctionClearance) || junctionRadius <= width * 0.5f)
            {
                throw new ArgumentException("墙宽、墙高必须为有限正数，连接底座半径必须大于墙半宽。");
            }

            if (wallHealth <= 0 || gateHealth <= 0 || !Positive(gateAttackInterval))
            {
                throw new ArgumentException("城墙、城门生命与受击间隔必须为正数。");
            }
            WallHealth = wallHealth;
            GateHealth = gateHealth;
            GateAttackInterval = gateAttackInterval;
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
