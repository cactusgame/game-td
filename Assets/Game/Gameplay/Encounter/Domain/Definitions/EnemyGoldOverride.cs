using System;

namespace Game.Encounter.Domain.Definitions
{
    [Serializable]
    public sealed class EnemyGoldOverride
    {
        public string enemyId;
        public int gold;
        public EnemyGoldOverride Copy() => new EnemyGoldOverride { enemyId = enemyId, gold = gold };
    }
}
