using System;

namespace Game.Battle.Domain
{
    public enum UnitKind : byte { Hero, Goblin, Runner }

    /// <summary>某类单位的只读配置，不是场上某只单位的当前生命或当前位置。</summary>
    public sealed class UnitDefinition
    {
        public UnitKind Kind { get; }
        public int MaxHealth { get; }
        public int Attack { get; }
        public int Defense { get; }
        public float Speed { get; }
        // 逻辑碰撞半径与画面高度分开；放大 Sprite 不会改变接触检测结果。
        public float Radius { get; }
        public float VisualHeight { get; }

        public UnitDefinition(UnitKind kind, int maxHealth, int attack, int defense,
            float speed, float radius, float visualHeight)
        {
            if (maxHealth <= 0 || attack < 0 || defense < 0)
                throw new ArgumentException("Health must be positive; attack and defense must be nonnegative.");
            RequirePositive(speed, nameof(speed));
            RequirePositive(radius, nameof(radius));
            RequirePositive(visualHeight, nameof(visualHeight));
            Kind = kind; MaxHealth = maxHealth; Attack = attack; Defense = defense;
            Speed = speed; Radius = radius; VisualHeight = visualHeight;
        }

        internal static void RequirePositive(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
                throw new ArgumentOutOfRangeException(name, "Must be finite and positive.");
        }
    }

    /// <summary>通过验证的本局配置；运行时组件从它初始化，局内变化不会回写共享配置。</summary>
    public sealed class BattleDefinition
    {
        public UnitDefinition Hero { get; }
        public UnitDefinition Goblin { get; }
        public UnitDefinition Runner { get; }
        public int GoblinCount { get; }
        public int RunnerCount { get; }
        public int MonsterCount => GoblinCount + RunnerCount;
        public float Width { get; }
        public float Height { get; }
        // 模拟步长，单位为秒；与显示器刷新率和画面 FPS 独立。
        public float FixedStep { get; }
        public uint Seed { get; }

        public BattleDefinition(UnitDefinition hero, UnitDefinition goblin, UnitDefinition runner,
            int goblinCount, int runnerCount, float width, float height, float fixedStep, uint seed)
        {
            Hero = hero ?? throw new ArgumentNullException(nameof(hero));
            Goblin = goblin ?? throw new ArgumentNullException(nameof(goblin));
            Runner = runner ?? throw new ArgumentNullException(nameof(runner));
            if (hero.Kind != UnitKind.Hero || goblin.Kind != UnitKind.Goblin || runner.Kind != UnitKind.Runner)
                throw new ArgumentException("Unit definitions do not match their roles.");
            if (goblinCount < 0 || runnerCount < 0 || (long)goblinCount + runnerCount > 100000)
                throw new ArgumentOutOfRangeException(nameof(goblinCount), "Total monsters must be between 0 and 100000.");
            UnitDefinition.RequirePositive(width, nameof(width));
            UnitDefinition.RequirePositive(height, nameof(height));
            UnitDefinition.RequirePositive(fixedStep, nameof(fixedStep));
            float maxRadius = Math.Max(hero.Radius, Math.Max(goblin.Radius, runner.Radius));
            if (Math.Min(width, height) <= maxRadius * 2 + 2)
                throw new ArgumentException("Arena is too small for the configured units.");
            ValidateSpawnSpace(hero, goblin, goblinCount, width, height);
            ValidateSpawnSpace(hero, runner, runnerCount, width, height);
            if (fixedStep > 0.1f) throw new ArgumentOutOfRangeException(nameof(fixedStep));
            GoblinCount = goblinCount; RunnerCount = runnerCount;
            Width = width; Height = height; FixedStep = fixedStep; Seed = seed == 0 ? 1u : seed;
        }

        public UnitDefinition GetUnit(UnitKind kind)
            => kind == UnitKind.Hero ? Hero : kind == UnitKind.Goblin ? Goblin : Runner;

        // 保证出生位置的兜底角落位于英雄安全距离外，避免极小地图无法找到合法出生位置。
        private static void ValidateSpawnSpace(UnitDefinition hero, UnitDefinition enemy, int count, float width, float height)
        {
            if (count == 0) return;
            float x = width / 2 - enemy.Radius - 0.2f, y = height / 2 - enemy.Radius - 0.2f;
            float clearance = hero.Radius + enemy.Radius + 1;
            if (x * x + y * y <= clearance * clearance)
                throw new ArgumentException("Arena has no safe initial monster spawn outside the hero clearance.");
        }
    }
}
