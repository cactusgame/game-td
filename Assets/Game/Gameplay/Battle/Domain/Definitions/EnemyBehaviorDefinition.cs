using System;

namespace Game.Battle.Domain.Definitions
{
    /// <summary>单位与战斗的只读内容定义，不保存对局可变状态。</summary>
    public sealed class EnemyBehaviorDefinition
    {
        public string Id { get; }
        public string Target { get; }
        public bool CanAttackGates { get; }
        public EnemyBehaviorDefinition(string id, string target, bool canAttackGates)
        {
            if (string.IsNullOrWhiteSpace(id) || (target != "MainCastle" && target != "Hero"))
                throw new ArgumentException("Invalid EnemyBehaviorDefinition");
            Id = id;
            Target = target;
            CanAttackGates = canAttackGates;
        }
        private static bool Nonnegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
        private static bool Positive(float value) => Nonnegative(value) && value > 0;
    }
}
