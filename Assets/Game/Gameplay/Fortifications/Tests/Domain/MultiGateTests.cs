using System;
using System.Linq;
using Game.Fortifications.Domain.Aggregates.WallNetwork;
using Game.Fortifications.Domain.Definitions;
using Game.Fortifications.Domain.Rules;
using Game.Fortifications.Domain.ValueObjects;
using Game.MapEngine.Domain;
using NUnit.Framework;

namespace Game.Fortifications.Tests.Domain
{
    public sealed class MultiGateTests
    {
        private static WallNetwork Network(float x = 20, float y = 0, float cellWidth = 1, float cellHeight = 1)
        {
            return new WallNetwork(new WallNetworkSnapshot(new[]
            {
                new WallNodeSnapshot("a", new MapPoint(0, 0)),
                new WallNodeSnapshot("b", new MapPoint(x, y))
            }, new[] { new WallEdgeSnapshot("wall", "a", "b") }, cellWidth, cellHeight));
        }

        [TestCase(20, 0)]
        [TestCase(0, 20)]
        [TestCase(12, 16)]
        [TestCase(16, -12)]
        [TestCase(-12, 16)]
        public void Place_TwoDoorsOnAnyWallDirection_PreservesIndependentIdentity(float x, float y)
        {
            var walls = Network(x, y);
            Assert.That(walls.TryPlaceGate("wall", new WallGateSnapshot("gate-a", 2, 4, 2, false), WallDefinition.Default, out var reason), Is.True, reason);
            Assert.That(walls.TryPlaceGate("wall", new WallGateSnapshot("gate-b", 10, 4, 2, true), WallDefinition.Default, out reason), Is.True, reason);
            Assert.That(walls.TrySetGateOpen("gate-a", true, out _), Is.True);
            Assert.That(walls.TrySetGateOpen("gate-b", false, out _), Is.True);
            Assert.That(walls.Snapshot.Edges[0].Gates[0].IsOpen, Is.True);
            Assert.That(walls.Snapshot.Edges[0].Gates[1].IsOpen, Is.False);
        }

        [TestCase(3, 4, 2)]
        [TestCase(18, 4, 2)]
        [TestCase(-1, 4, 2)]
        [TestCase(10, 2, 1)]
        [TestCase(10, 5, 2)]
        public void Place_OverlapOverflowOrFractionalSideColumns_RejectsWithoutPartialState(int start, int width, int opening)
        {
            var walls = Network();
            walls.TryPlaceGate("wall", new WallGateSnapshot("gate-a", 2, 4, 2, false), WallDefinition.Default, out _);
            var before = walls.Snapshot;
            Assert.That(walls.TryPlaceGate("wall", new WallGateSnapshot("gate-b", start, width, opening, false), WallDefinition.Default, out _), Is.False);
            Assert.That(walls.Snapshot, Is.SameAs(before));
        }

        [Test]
        public void ReverseWall_StoredTileIntervals_KeepDoorWorldPosition()
        {
            var source = Network().Snapshot;
            var gate = new WallGateSnapshot("gate-a", 3, 4, 2, false);
            var forward = new WallEdgeSnapshot("wall", "a", "b", new[] { gate });
            var backward = new WallEdgeSnapshot("wall", "b", "a", new[] { gate });
            WallGateRules.TryGetSpan(source, forward, gate, out var first);
            WallGateRules.TryGetSpan(source, backward, gate, out var second);
            Assert.That(first.Center, Is.EqualTo(second.Center));
            Assert.That(first.Start, Is.EqualTo(second.Start));
        }

        [Test]
        public void RectangularTiles_SameTileCount_UsesPhysicalLengthAlongWall()
        {
            var horizontal = Network(40, 0, 2, 1).Snapshot;
            var vertical = Network(0, 20, 2, 1).Snapshot;
            var gate = new WallGateSnapshot("gate-a", 3, 4, 2, false);
            WallGateRules.TryGetSpan(horizontal, horizontal.Edges[0], gate, out var across);
            WallGateRules.TryGetSpan(vertical, vertical.Edges[0], gate, out var up);
            Assert.That(across.End - across.Start, Is.EqualTo(8));
            Assert.That(up.End - up.Start, Is.EqualTo(4));
        }

        [Test]
        public void OpenDoors_NavigationCuts_ExcludeOnlyTheirOwnOpenings()
        {
            var walls = Network();
            walls.TryPlaceGate("wall", new WallGateSnapshot("gate-a", 2, 4, 2, true), WallDefinition.Default, out _);
            walls.TryPlaceGate("wall", new WallGateSnapshot("gate-b", 10, 4, 2, false), WallDefinition.Default, out _);
            Assert.That(WallObstacleRules.BuildNavigationObstacles(walls.Snapshot, WallDefinition.Default, 0.22f).Count(), Is.EqualTo(2));
            walls.TrySetGateOpen("gate-b", true, out _);
            Assert.That(WallObstacleRules.BuildNavigationObstacles(walls.Snapshot, WallDefinition.Default, 0.22f).Count(), Is.EqualTo(3));
        }
    }
}
