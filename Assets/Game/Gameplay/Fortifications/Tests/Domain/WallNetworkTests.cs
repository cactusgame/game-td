using System;
using System.Collections.Generic;
using Game.Fortifications.Domain.Aggregates.WallNetwork;
using Game.Fortifications.Domain.Definitions;
using Game.Fortifications.Domain.Rules;
using Game.Fortifications.Domain.ValueObjects;
using Game.MapEngine.Domain;
using NUnit.Framework;

namespace Game.Fortifications.Tests.Domain
{
    public sealed class WallNetworkTests
    {
        private static WallNetwork Create(bool withGate = false)
        {
            var nodes = new[]
            {
                new WallNodeSnapshot("a", new MapPoint(-5, 0)),
                new WallNodeSnapshot("b", new MapPoint(5, 0)),
                new WallNodeSnapshot("c", new MapPoint(5, 5))
            };
            var gate = withGate ? new WallGateSnapshot("gate-a", 3, 4, 2, true) : null;
            return new WallNetwork(new WallNetworkSnapshot(nodes, new[] { new WallEdgeSnapshot("ab", "a", "b", gate == null ? null : new[] { gate }) }));
        }

        [Test]
        public void Connect_ReversedDuplicate_RejectsWithoutChangingTheNetwork()
        {
            var walls = Create();
            var before = walls.Snapshot;
            Assert.That(walls.TryConnect(new WallEdgeSnapshot("ba", "b", "a"), out _), Is.False);
            Assert.That(walls.Snapshot, Is.SameAs(before));
            Assert.That(walls.TryConnect(new WallEdgeSnapshot("bc", "b", "c"), out _), Is.True);
            Assert.That(walls.Snapshot.Edges.Count, Is.EqualTo(2));
        }

        [Test]
        public void Connect_MissingOrCoincidentEndpoints_LeavesNoPartialConnection()
        {
            var walls = Create();
            Assert.That(walls.TryConnect(new WallEdgeSnapshot("bad", "a", "missing"), out _), Is.False);
            var nodes = new[]
            {
                new WallNodeSnapshot("a", new MapPoint(1, 1)),
                new WallNodeSnapshot("b", new MapPoint(1, 1))
            };
            var zeroLength = new WallNetwork(new WallNetworkSnapshot(nodes, Array.Empty<WallEdgeSnapshot>()));
            Assert.That(zeroLength.TryConnect(new WallEdgeSnapshot("ab", "a", "b"), out _), Is.False);
            Assert.That(zeroLength.Snapshot.Edges, Is.Empty);
        }

        [Test]
        public void Import_RepeatedConnection_RejectsInvalidRuntimeTopology()
        {
            var initial = Create().Snapshot;
            var repeated = new WallNetworkSnapshot(initial.Nodes, new[]
            {
                new WallEdgeSnapshot("ab", "a", "b"),
                new WallEdgeSnapshot("ba", "b", "a")
            });
            Assert.Throws<ArgumentException>(() => new WallNetwork(repeated));
        }

        [Test]
        public void Candidate_GateChangeAndRemoval_KeepOriginalAndEarlierSnapshotsIntact()
        {
            var walls = Create(true);
            var before = walls.Snapshot;
            var candidate = walls.CreateCandidate();
            Assert.That(candidate.TrySetGateOpen("gate-a", false, out _), Is.True);
            Assert.That(candidate.Snapshot.FindEdge("ab").Gates[0].IsOpen, Is.False);
            Assert.That(before.FindEdge("ab").Gates[0].IsOpen, Is.True);
            Assert.That(walls.Snapshot.FindEdge("ab").Gates[0].IsOpen, Is.True);
            Assert.That(candidate.TryRemoveEdge("ab"), Is.True);
            Assert.That(candidate.Snapshot.Edges, Is.Empty);
            Assert.That(walls.Snapshot.Edges.Count, Is.EqualTo(1));
        }

        [Test]
        public void Snapshot_ChangingInputCollections_DoesNotChangePublishedTopology()
        {
            var nodes = new List<WallNodeSnapshot> { new WallNodeSnapshot("a", new MapPoint(0, 0)) };
            var snapshot = new WallNetworkSnapshot(nodes, Array.Empty<WallEdgeSnapshot>());
            nodes.Clear();
            Assert.That(snapshot.Nodes.Count, Is.EqualTo(1));
            Assert.Throws<NotSupportedException>(() => ((IList<WallNodeSnapshot>)snapshot.Nodes).Clear());
        }

        [Test]
        public void Junction_ConnectionChord_UsesPhysicalWallWidth()
        {
            var walls = Create();
            var definition = new WallDefinition(0.8f, 1, 0.6f);
            float radius = WallConnectionRules.JunctionRadiusAt(walls.Snapshot, "b", definition);
            float port = WallConnectionRules.PortDistance(radius, definition);
            Assert.That(port * port + definition.Width * definition.Width * 0.25f, Is.EqualTo(radius * radius).Within(0.00001f));
            Assert.That(WallConnectionRules.DirectionsAt(walls.Snapshot, "b").Count, Is.EqualTo(1));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(0)]
        [TestCase(-1)]
        public void Definition_InvalidPhysicalWidth_RejectsBeforeCreatingAView(float width)
        {
            Assert.Throws<ArgumentException>(() => new WallDefinition(width, 1, 1));
        }
    }
}
