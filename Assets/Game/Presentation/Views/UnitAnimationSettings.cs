using System;

namespace Game.Presentation.Views
{
    /// <summary>产品可变的单位外观选择，不包含素材制作参数或实例状态。</summary>
    [Serializable]
    public sealed class UnitAnimationSettings
    {
        public string UnitKind;
        public string ResourceId;
    }
}
