using System;
using System.Collections.Generic;

namespace Game.Battle.Contracts.Commands
{
    /// <summary>设备无关的移动意图；实际位移由模拟根据速度和步长计算。</summary>
    public readonly struct MoveIntent
    {
        public readonly float X;
        public readonly float Y;
        public MoveIntent(float x, float y)
        {
            if (float.IsNaN(x) || float.IsInfinity(x))
            {
                throw new ArgumentOutOfRangeException(nameof(x), "Movement input must be finite.");
            }

            if (float.IsNaN(y) || float.IsInfinity(y))
            {
                throw new ArgumentOutOfRangeException(nameof(y), "Movement input must be finite.");
            }

            X = x;
            Y = y;
        }
    }
}
