using System;
namespace Game.Presentation.Views
{
    /// <summary>JSON 中的动画参数；与 Sprite 资源引用分离。</summary>
    [Serializable]
    public sealed class UnitAnimationSettings
    {
        public float IdleFps, MoveFps, AttackFps, HurtFps;
        public float BodyHeightFraction, DeathBodyHeightFraction;
    }
}
