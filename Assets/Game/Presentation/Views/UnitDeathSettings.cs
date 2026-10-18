using UnityEngine;

namespace Game.Presentation.Views
{
    /// <summary>死亡表现的统一配置，不参与领域伤害和 ECS 生命周期计算。</summary>
    [CreateAssetMenu(menuName = "Game/Presentation/Unit Death Settings")]
    public sealed class UnitDeathSettings : ScriptableObject
    {
        [Tooltip("死亡序列完整播放所需秒数。")]
        [Min(0.01f)] public float AnimationSeconds = 0.6f;
        [Tooltip("死亡动画播放完毕后，保留最后一帧的秒数。")]
        [Min(0)] public float CorpseHoldSeconds = 1.5f;

        public float Duration => Mathf.Max(0.01f, AnimationSeconds);
        public float Hold => Mathf.Max(0, CorpseHoldSeconds);
    }
}
