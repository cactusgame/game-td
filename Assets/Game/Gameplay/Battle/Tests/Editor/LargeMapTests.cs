using System.Collections.Generic;
using System.IO;
using Game.Battle.Contracts.Commands;
using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.Definitions;
using Game.Battle.Domain.ValueObjects;
using Game.Battle.Simulation.Bridges;
using Game.Infrastructure.Configuration;
using Game.MapEngine.Domain;
using Game.Presentation.Views;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Battle.Tests
{
    public sealed class LargeMapTests
    {
        [Test]
        public void DefaultLargeMapIsNineLandscapeViewsAndNavigationCoversIt()
        {
            var definition = JsonBattleDefinitionSource.Parse(File.ReadAllText("Assets/Content/Configuration/Resources/GameConfig/battle.json"));
            var settings = BattlePresentationSettings.Parse(System.IO.File.ReadAllText("Assets/Content/Configuration/Resources/GameConfig/presentation.json")).Map;
            Assert.That(settings, Is.Not.Null);
            float visibleWidth = settings.VisibleWorldHeight * 16 / 9;
            Assert.That(definition.Width / visibleWidth, Is.EqualTo(3).Within(0.0001));
            Assert.That(definition.Height / settings.VisibleWorldHeight, Is.EqualTo(3).Within(0.0001));
            Assert.That(definition.NavigationMap.Width, Is.EqualTo(definition.Width));
            Assert.That(definition.NavigationMap.Height, Is.EqualTo(definition.Height));
            var path = new List<GridCell>();
            var map = definition.NavigationMap;
            Assert.That(new GridPathfinder(map).TryFindPath(new GridCell(0, 0), new GridCell(map.Columns - 1, map.Rows - 1), path), Is.True);
        }

        [TestCase(0, 0, 0, 0)]
        [TestCase(15, 5, 15, 5)]
        [TestCase(48, 27, 32.75f, 18.75f)]
        [TestCase(-48, -27, -32.75f, -18.75f)]
        public void CameraFollowsHeroAndStopsAtMapEdges(float heroX, float heroY, float expectedX, float expectedY)
        {
            var center = BattleCameraFraming.Center(new Vector2(heroX, heroY), 96, 54, 18, 16f / 9, 0.75f);
            Assert.That(center.x, Is.EqualTo(expectedX).Within(0.0001));
            Assert.That(center.y, Is.EqualTo(expectedY).Within(0.0001));
        }

        [TestCase(9f / 16)]
        [TestCase(16f / 9)]
        [TestCase(32f / 9)]
        public void CameraKeepsEdgeVisibleAcrossPortraitAndWideScreens(float aspect)
        {
            var center = BattleCameraFraming.Center(new Vector2(48, 27), 96, 54, 18, aspect, 0.75f);
            Assert.That(center.x + 9 * aspect, Is.EqualTo(48.75f).Within(0.0001));
            Assert.That(center.y + 9, Is.EqualTo(27.75f).Within(0.0001));
            Assert.That(BattleCameraFraming.Center(Vector2.one * 100, 8, 6, 18, aspect, 0.75f), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void HeroStartsAtCenterCanReachEveryCornerAndRestartsAtCenter()
        {
            var configured = JsonBattleDefinitionSource.Parse(File.ReadAllText("Assets/Content/Configuration/Resources/GameConfig/battle.json"));
            var hero = new UnitDefinition(UnitKind.Hero, 100, 15, 3, 100, 0.42f, 1.65f);
            var definition = new BattleDefinition(hero, configured.Goblin, configured.Runner, 0, 0, 96, 54, 0.02f, 123, configured.Navigation);
            using var simulation = new EcsBattleSimulation();
            var units = new List<UnitSnapshot>();
            simulation.Reset(definition);
            simulation.CopyUnits(units);
            Assert.That(new float2(units[0].X, units[0].Y), Is.EqualTo(float2.zero));
            foreach (var direction in new[]{new float2(1, 1), new float2(-1, 1), new float2(-1, -1), new float2(1, -1)})
            {
                for (int i = 0; i < 120; i++)
                    simulation.Step(0.02f, new MoveIntent(direction.x, direction.y));
                simulation.CopyUnits(units);
                Assert.That(units[0].X, Is.EqualTo(direction.x * (48 - hero.Radius)).Within(0.001));
                Assert.That(units[0].Y, Is.EqualTo(direction.y * (27 - hero.Radius)).Within(0.001));
            }

            simulation.Reset(definition);
            simulation.CopyUnits(units);
            Assert.That(new float2(units[0].X, units[0].Y), Is.EqualTo(float2.zero));
        }

        [Test]
        public void MonstersSpawnAcrossTheLargeMapAndStayInsideItsNavigationCoverage()
        {
            var configured = JsonBattleDefinitionSource.Parse(File.ReadAllText("Assets/Content/Configuration/Resources/GameConfig/battle.json"));
            var definition = new BattleDefinition(configured.Hero, configured.Goblin, configured.Runner, 6, 4, 96, 54, 0.02f, 123, configured.Navigation);
            using var simulation = new EcsBattleSimulation();
            var units = new List<UnitSnapshot>();
            simulation.Reset(definition);
            simulation.CopyUnits(units);
            bool outsideOldArena = false;
            foreach (var unit in units)
            {
                if (unit.Kind == UnitKind.Hero)
                    continue;
                outsideOldArena |= math.abs(unit.X) > 13 || math.abs(unit.Y) > 7;
                Assert.That(definition.NavigationMap.TryWorldToCell(unit.X, unit.Y, out _), Is.True);
            }

            Assert.That(outsideOldArena, Is.True);
        }
    }
}
