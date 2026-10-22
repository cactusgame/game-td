using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.MapEngine.Domain;
using Game.Battle.Domain.ValueObjects;

namespace Game.Battle.Domain.Definitions
{
    /// <summary>通过验证的本局只读配置；运行时变化不回写定义。</summary>
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

        public NavigationDefinition Navigation { get; }

        public GridMap NavigationMap { get; }
        public MapPoint HeroSpawn { get; }
        public IReadOnlyList<MapPoint> EnemySpawns { get; }

        public BattleDefinition(UnitDefinition hero, UnitDefinition goblin, UnitDefinition runner, int goblinCount, int runnerCount, float width, float height, float fixedStep, uint seed, NavigationDefinition navigation = null, GridMap navigationMap = null, MapPoint? heroSpawn = null, IReadOnlyList<MapPoint> enemySpawns = null)
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
            if (enemySpawns == null || enemySpawns.Count == 0)
            {
                ValidateSpawnSpace(hero, goblin, goblinCount, width, height);
                ValidateSpawnSpace(hero, runner, runnerCount, width, height);
            }
            if (fixedStep > 0.1f)
                throw new ArgumentOutOfRangeException(nameof(fixedStep));
            GoblinCount = goblinCount;
            RunnerCount = runnerCount;
            Width = width;
            Height = height;
            FixedStep = fixedStep;
            Seed = seed == 0 ? 1u : seed;
            Navigation = navigation ?? new NavigationDefinition();
            NavigationMap = navigationMap ?? GridMap.CreateOpenArena(width, height, Navigation.CellSize);
            // 战斗场地与导航坐标必须相同；以后编辑器可传入已导出的纯地图定义。
            const float geometryTolerance = 0.0001f;
            if (Math.Abs(NavigationMap.Width - width) > geometryTolerance || Math.Abs(NavigationMap.Height - height) > geometryTolerance)
            {
                throw new ArgumentException("Navigation map must cover the battle arena in the same ground coordinates.", nameof(navigationMap));
            }
            HeroSpawn = heroSpawn ?? new MapPoint(NavigationMap.OriginX + width * 0.5f, NavigationMap.OriginY + height * 0.5f);
            if (!NavigationMap.CanOccupy(HeroSpawn.X, HeroSpawn.Y, Hero.Radius))
                throw new ArgumentException("Hero spawn has insufficient clearance.", nameof(heroSpawn));
            var spawnCopy = new List<MapPoint>();
            if (enemySpawns != null)
            {
                float enemyRadius = Math.Max(Goblin.Radius, Runner.Radius);
                foreach (var point in enemySpawns)
                {
                    if (!NavigationMap.CanOccupy(point.X, point.Y, enemyRadius))
                        throw new ArgumentException("Enemy entry has insufficient clearance.", nameof(enemySpawns));
                    spawnCopy.Add(point);
                }
            }
            EnemySpawns = new ReadOnlyCollection<MapPoint>(spawnCopy);
        }

        public UnitDefinition GetUnit(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Hero:
                    return Hero;
                case UnitKind.Goblin:
                    return Goblin;
                case UnitKind.Runner:
                    return Runner;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), "Unknown unit kind.");
            }
        }

        // 保证出生位置的兜底角落位于英雄安全距离外，避免极小地图无法找到合法出生位置。
        private static void ValidateSpawnSpace(UnitDefinition hero, UnitDefinition enemy, int count, float width, float height)
        {
            if (count == 0)
                return;
            float x = width / 2 - enemy.Radius - 0.2f, y = height / 2 - enemy.Radius - 0.2f;
            float clearance = hero.Radius + enemy.Radius + 1;
            if (x * x + y * y <= clearance * clearance)
                throw new ArgumentException("Arena has no safe initial monster spawn outside the hero clearance.");
        }
    }
}
