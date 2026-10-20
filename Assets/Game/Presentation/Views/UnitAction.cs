namespace Game.Presentation.Views
{
    /// <summary>人物表现的统一动作契约；素材不能自行重命名动作。缺少可选动作时不播放。</summary>
    public enum UnitAction
    {
        Idle,
        Walk,
        Run,
        Attack,
        Hurt,
        Death,
        Skill,
        Skill2,
        Skill3,
        Jump,
        Spawn,
        WalkBack
    }
}
