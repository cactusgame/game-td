using UnityEngine;

namespace Game.Presentation.Views
{
    /// <summary>纯表现资源。帧顺序和播放速度不参与 ECS 伤害或位移计算。</summary>
    public sealed class UnitAnimationSet : ScriptableObject
    {
        public Sprite[] Idle, Move, Attack, Hurt, Death;
        public float IdleFps = 5, MoveFps = 9, AttackFps = 10, HurtFps = 14;
        // 固定画布内的角色高度比例，避免按不同动作的包围盒缩放导致抖动。
        public float BodyHeightFraction = 0.72f;
        public float DeathBodyHeightFraction = 0.6f;

        public Sprite Frame(Sprite[] frames, double age, float fps, bool loop)
        {
            if (frames == null || frames.Length == 0) return null;
            int index = Mathf.Max(0, (int)(age * fps));
            return frames[loop ? index % frames.Length : Mathf.Min(index, frames.Length - 1)];
        }

        public double AttackDuration => Attack == null ? 0 : Attack.Length / (double)AttackFps;
        public double HurtDuration => Hurt == null ? 0 : Hurt.Length / (double)HurtFps;
    }
}
