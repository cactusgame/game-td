using System;
using System.Collections.Generic;
using System.IO;
using Game.Battle.Domain;
using Game.Infrastructure.Configuration;
using Game.MapEngine.Domain;
using NUnit.Framework;

namespace Game.Battle.Tests
{
    public sealed class GridNavigationTests
    {
        [Test] public void OpenArenaIsFullyConnectedAndUsesGroundCoordinates()
        {
            var map = GridMap.CreateOpenArena(26, 14, 1);
            Assert.That(map.Columns, Is.EqualTo(26));
            Assert.That(map.Rows, Is.EqualTo(14));
            Assert.That(map.TryWorldToCell(-13, -7, out var first), Is.True);
            Assert.That(first, Is.EqualTo(new GridCell(0, 0)));
            Assert.That(map.TryWorldToCell(0, 0, out var center), Is.True);
            Assert.That(center, Is.EqualTo(new GridCell(13, 7)));
            Assert.That(map.TryWorldToCell(13, 0, out _), Is.False);
            Assert.That(map.TryWorldToCell(0, 7, out _), Is.False);
            Assert.That(map.TryWorldToCell(float.NaN, 0, out _), Is.False);

            var search = new GridPathfinder(map);
            var path = new List<GridCell>();
            for (int i = 0; i < map.CellCount; i++)
            {
                var cell = map.CellAt(i);
                Assert.That(map.IsWalkable(cell), Is.True);
                Assert.That(search.TryFindPath(first, cell, path), Is.True);
                Assert.That(path[0], Is.EqualTo(first));
                Assert.That(path[path.Count - 1], Is.EqualTo(cell));
                for (int step = 1; step < path.Count; step++)
                    Assert.That(map.CanStep(path[step - 1], path[step]), Is.True);
            }
        }

        [Test] public void NondivisibleArenaStillHasExactCoverageAndCellCenterRoundTrips()
        {
            var map = GridMap.CreateOpenArena(5.5f, 3.2f, 1);
            Assert.That(map.Columns, Is.EqualTo(6));
            Assert.That(map.Rows, Is.EqualTo(4));
            Assert.That(map.Width, Is.EqualTo(5.5f).Within(0.0001));
            Assert.That(map.Height, Is.EqualTo(3.2f).Within(0.0001));
            for (int index = 0; index < map.CellCount; index++)
            {
                var cell = map.CellAt(index);
                var point = map.CellCenter(cell);
                Assert.That(map.TryWorldToCell(point.X, point.Y, out var result), Is.True);
                Assert.That(result, Is.EqualTo(cell));
            }
        }

        [Test] public void RectangularCellPathHasShortestPhysicalLength()
        {
            var map = new GridMap(4, 4, 2, 1, 0, 0);
            var path = new List<GridCell>();
            Assert.That(new GridPathfinder(map).TryFindPath(new GridCell(0, 0), new GridCell(3, 2), path), Is.True);
            double length = 0;
            for (int i = 1; i < path.Count; i++)
            {
                var previous = map.CellCenter(path[i - 1]);
                var current = map.CellCenter(path[i]);
                double dx = current.X - previous.X;
                double dy = current.Y - previous.Y;
                length += Math.Sqrt(dx * dx + dy * dy);
            }
            Assert.That(length, Is.EqualTo(2 * Math.Sqrt(5) + 2).Within(0.0001));
        }

        [Test] public void SearchWorkspaceCanBeReusedAndCannotCutBlockedCorners()
        {
            var map = new GridMap(2, 2, 1, 1, 0, 0, new[] { true, false, false, true });
            var search = new GridPathfinder(map);
            var path = new List<GridCell> { new GridCell(99, 99) };
            Assert.That(search.TryFindPath(new GridCell(0, 0), new GridCell(1, 1), path), Is.False);
            Assert.That(path, Is.Empty);
            Assert.That(search.TryFindPath(new GridCell(1, 1), new GridCell(1, 1), path), Is.True);
            Assert.That(path, Is.EqualTo(new[] { new GridCell(1, 1) }));
            Assert.That(search.TryFindPath(new GridCell(-1, 0), new GridCell(1, 1), path), Is.False);
            Assert.That(path, Is.Empty);
        }

        [Test] public void ImportedDataIsCopiedAndPathCanGoThroughAWallGap()
        {
            var walkable = new bool[25];
            for (int i = 0; i < walkable.Length; i++) walkable[i] = true;
            for (int y = 0; y < 4; y++) walkable[y * 5 + 2] = false;
            var map = new GridMap(5, 5, 1, 1, 0, 0, walkable);
            walkable[0] = false;
            Assert.That(map.IsWalkable(new GridCell(0, 0)), Is.True);
            var path = new List<GridCell>();
            Assert.That(new GridPathfinder(map).TryFindPath(new GridCell(0, 0), new GridCell(4, 0), path), Is.True);
            Assert.That(path, Does.Contain(new GridCell(2, 4)));
            for (int i = 1; i < path.Count; i++)
                Assert.That(map.CanStep(path[i - 1], path[i]), Is.True);
        }

        [Test] public void InvalidMapAndConfigurationAreRejectedBeforeSimulation()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridMap(0, 1, 1, 1, 0, 0));
            Assert.Throws<ArgumentException>(() => new GridMap(2, 2, 1, 1, 0, 0, new bool[3]));
            Assert.Throws<ArgumentOutOfRangeException>(() => GridMap.CreateOpenArena(26, 14, float.Epsilon));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NavigationDefinition(1, -1, 0.25f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NavigationDefinition(1, 5, float.NaN));
            var definition = JsonBattleDefinitionSource.Parse(File.ReadAllText("Assets/Content/Combat/Prototype/battle.json"));
            Assert.That(definition.Navigation.DetectionRadius, Is.EqualTo(5));
            Assert.That(definition.Navigation.RepathSeconds, Is.EqualTo(0.25f));
            Assert.Throws<ArgumentException>(() => new BattleDefinition(definition.Hero, definition.Goblin, definition.Runner,
                0, 0, 26, 14, 0.02f, 1, navigationMap: new GridMap(2, 2, 1, 1, 0, 0)));
        }

        [Test] public void MapDomainDoesNotDependOnUnityOrEcs()
        {
            foreach (var reference in typeof(GridMap).Assembly.GetReferencedAssemblies())
                Assert.That(reference.Name.StartsWith("Unity", StringComparison.Ordinal), Is.False);
        }
    }
}
