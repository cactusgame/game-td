using UnityEngine;

namespace Game.Presentation.Views
{
    /// <summary>纯表现资源。帧顺序不参与 ECS 伤害或位移计算。</summary>
    public sealed class UnitAnimationSet : ScriptableObject
    {
        public Sprite[] Idle, Move, Attack, Hurt, Death;
        public Sprite Frame(Sprite[] frames, double age, float fps, bool loop)
        {
            if (frames == null || frames.Length == 0)
                return null;
            int index = Mathf.Max(0, (int)(age * fps));
            return frames[loop ? index % frames.Length : Mathf.Min(index, frames.Length - 1)];
        }
    }
}
