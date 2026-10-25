using System;
using System.Collections.Generic;

namespace Game.Encounter.Domain.Definitions
{
    /// <summary>可保存不完整草稿的关卡创作数据。运行时必须先构建 LevelDefinition。</summary>
    [Serializable]
    public sealed class LevelLayout
    {
        public string id = "level-1";
        public string name = "新关卡";
        public int initialGold = 1000;
        public List<EnemyGoldOverride> goldOverrides = new List<EnemyGoldOverride>();
        public List<WaveLayout> waves = new List<WaveLayout>();

        public LevelLayout Copy()
        {
            var copy = new LevelLayout { id = id, name = name, initialGold = initialGold };
            copy.goldOverrides = goldOverrides?.ConvertAll(item => item?.Copy());
            copy.waves = waves?.ConvertAll(item => item?.Copy());
            return copy;
        }
    }
}
