namespace Game.Presentation.Views
{
    /// <summary>死亡表现的统一配置，不参与领域伤害和 ECS 生命周期计算。</summary>
    [System.Serializable]
    public sealed class UnitDeathSettings
    {
        public float AnimationSeconds;
        public float CorpseHoldSeconds;

        public float Duration => AnimationSeconds;
        public float Hold => CorpseHoldSeconds;
    }
}
