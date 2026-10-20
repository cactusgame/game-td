using System;
using UnityEngine;

namespace Game.Presentation.Views
{
    /// <summary>会话创建时解析的只读片段；按模拟时间采样，不接收动画事件或结算伤害。</summary>
    public sealed class UnitAnimationClip
    {
        private readonly Sprite[] _frames;
        private readonly int _start;
        public int FrameCount { get; }
        public float Fps { get; }
        public bool Loop { get; }
        public double Duration => FrameCount / (double)Fps;

        public UnitAnimationClip(UnitAnimationSet resources, UnitActionSettings settings)
        {
            if (resources == null || resources.Frames == null || settings == null ||
                settings.FirstFrame < 1 || settings.LastFrame < settings.FirstFrame ||
                settings.LastFrame > resources.Frames.Length)
                throw new FormatException("Animation frame range is outside its resource.");
            _frames = resources.Frames;
            _start = settings.FirstFrame - 1;
            FrameCount = settings.LastFrame - settings.FirstFrame + 1;
            Fps = settings.Fps;
            Loop = settings.Loop;
            if (float.IsNaN(Fps) || float.IsInfinity(Fps) || Fps <= 0)
                throw new FormatException("Animation FPS must be finite and positive.");
            for (int index = 0; index < FrameCount; index++)
            {
                if (_frames[_start + index] == null)
                    throw new FormatException("Missing Sprite in animation resource.");
            }
        }

        public Sprite Frame(double age)
        {
            double frame = Math.Floor(Math.Max(0, age) * Fps);
            int index = Loop ? (int)(frame % FrameCount) : (int)Math.Min(frame, FrameCount - 1);
            return _frames[_start + index];
        }
    }
}
