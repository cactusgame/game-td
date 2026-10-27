namespace Game.Battle.Domain.Rules
{
    /// <summary>每个单位只有一份普通攻击冷却，目标种类、换目标或离开攻击范围均不能重置。</summary>
    public static class NormalAttackRules
    {
        public static bool IsReady(double now, double nextAttackAt) => now + 1e-9 >= nextAttackAt;
        public static double NextAttackAt(double now, float interval) => now + (interval > 0 ? interval : 1);
    }
}
