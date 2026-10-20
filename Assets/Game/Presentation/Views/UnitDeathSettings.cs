namespace Game.Presentation.Views
{
    /// <summary>尸体停留是产品表现策略；死亡动画时长由素材自身的片段和帧率决定。</summary>
    [System.Serializable]
    public sealed class UnitDeathSettings
    {
        public float CorpseHoldSeconds;
        public float Hold => CorpseHoldSeconds;
    }
}
