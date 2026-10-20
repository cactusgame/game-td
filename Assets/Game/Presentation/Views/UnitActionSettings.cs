using System;

namespace Game.Presentation.Views
{
    /// <summary>随素材制作并固化的片段定义，帧号从 1 开始，包含首尾帧；不属于产品 JSON。</summary>
    [Serializable]
    public sealed class UnitActionSettings
    {
        public int FirstFrame;
        public int LastFrame;
        public float Fps;
        public bool Loop;

        // Unity 的内联序列化可能把空字段还原为全零对象，全零仍表示未提供此动作。
        public bool IsEmpty => FirstFrame == 0 && LastFrame == 0 && Fps == 0 && !Loop;
    }
}
