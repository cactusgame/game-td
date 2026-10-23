using System;
using System.Collections.Generic;
using System.Linq;
using Game.Infrastructure.Maps.Serialization;
using Game.MapEngine.Domain;
using Game.MapEngine.Domain.Geometry;
using Game.MapEngine.Domain.Spatial;
using Game.Maps.Domain;
using Game.Maps.Unity.Views;
using NUnit.Framework;

namespace Game.Maps.Tests
{
    public sealed class MapBoundaryTests
    {
        private static MapContentCatalog Catalog() => new MapContentCatalog(new[]
        {
            new FacilityDefinition { id = "tower", width = 1, height = 1 },
            new FacilityDefinition { id = "hall", width = 2, height = 2 }
        });

        [Test]
        public void SourceWithoutSpawnsCanBeDefinedButCannotEnterTowerDefensePlay()
        {
            var layout = new MapLayout();
            var catalog = Catalog();
            Assert.That(MapContentValidator.Validate(layout, catalog), Is.Empty);
            Assert.DoesNotThrow(() => new MapDefinition(layout, catalog));
            var issues = MapPlayabilityValidator.Validate(layout, catalog);
            Assert.That(issues.Any(i => i.Message.Contains("英雄出生")), Is.True);
            Assert.That(issues.Any(i => i.Message.Contains("敌人入口")), Is.True);
            layout.markers.Add(new MapMarker { id = "hero", name = "Hero", kind = MarkerKind.HeroSpawn, x = 0.5f, y = 0.5f });
            layout.markers.Add(new MapMarker { id = "enemy", name = "Entry", kind = MarkerKind.EnemyEntry, x = 2.5f, y = 0.5f });
            Assert.That(MapPlayabilityValidator.Validate(layout, catalog).Any(i => i.Message.Contains("主堡")), Is.True);
            layout.facilities.Add(new InitialFacility { id = "castle", definitionId = "hall", x = 1, y = 1 });
            Assert.That(MapPlayabilityValidator.Validate(layout, catalog), Is.Empty);
            layout.terrain[layout.Geometry.IndexOf(new GridCell(14, 8))] = TerrainKind.Water;
            Assert.That(MapContentValidator.Validate(layout, catalog), Is.Empty);
            Assert.That(MapPlayabilityValidator.Validate(layout, catalog).Any(i => i.ObjectId == "enemy"), Is.True);
        }

        [Test]
        public void MalformedSourceProducesLocationsInsteadOfCrashingPlayabilityValidation()
        {
            var layout = new MapLayout();
            layout.wallEdges.Add(new WallEdge { id = "broken", from = "missing", to = "missing" });
            Assert.That(MapPlayabilityValidator.Validate(layout, Catalog()).Any(i => i.ObjectId == "broken"), Is.True);
            layout.facilities.Add(null);
            Assert.DoesNotThrow(() => MapPlayabilityValidator.Validate(layout, Catalog()));
        }

        [Test]
        public void GenericObstaclesComposeNavigationWithoutKnowingFacilityOrWallTypes()
        {
            var geometry = new GridMap(6, 5, 2, 1, 10, 20);
            var terrain = Enumerable.Repeat(true, geometry.CellCount).ToArray();
            terrain[0] = false;
            var obstacles = new[]
            {
                ObstacleFootprint.Rectangle(new MapPoint(12, 21), new MapPoint(16, 23)),
                ObstacleFootprint.Segment(new MapPoint(19, 20.5f), new MapPoint(19, 24.5f), 0.22f)
            };
            var navigation = GridNavigationBuilder.Build(geometry, terrain, obstacles);
            Assert.That(navigation.IsWalkable(new GridCell(0, 0)), Is.False);
            Assert.That(navigation.IsWalkable(new GridCell(1, 1)), Is.False);
            Assert.That(navigation.IsWalkable(new GridCell(2, 2)), Is.False);
            Assert.That(navigation.IsWalkable(new GridCell(4, 3)), Is.False);
            Assert.That(navigation.IsWalkable(new GridCell(0, 4)), Is.True);
            terrain[0] = true;
            Assert.That(navigation.IsWalkable(new GridCell(0, 0)), Is.False);
        }

        [Test]
        public void SourceSchemaAndStableObjectIdsRoundTripAfterMovingAdapters()
        {
            var layout = new MapLayout { id = "round-trip" };
            layout.facilities.Add(new InitialFacility { id = "tower-a", definitionId = "tower", x = 1, y = 1 });
            layout.wallNodes.Add(new WallNode { id = "a", x = 0.5f, y = 2.5f });
            layout.wallNodes.Add(new WallNode { id = "b", x = 2.5f, y = 2.5f });
            layout.wallEdges.Add(new WallEdge { id = "ab", from = "a", to = "b" });
            string json = MapJson.WriteDocument(layout);
            var reopened = MapJson.ReadDocument(json);
            Assert.That(reopened.schemaVersion, Is.EqualTo(1));
            Assert.That(MapJson.WriteDocument(reopened), Is.EqualTo(json));
            Assert.That(reopened.facilities.Single().id, Is.EqualTo("tower-a"));
            Assert.That(reopened.wallEdges.Single().from, Is.EqualTo("a"));
        }

        [Test]
        public void CollinearAndCrossingSegmentsKeepTheirGenericGeometrySemantics()
        {
            Assert.That(SegmentGeometry.Intersects(0, 0, 4, 4, 0, 4, 4, 0), Is.True);
            Assert.That(SegmentGeometry.Intersects(0, 0, 4, 0, 2, 0, 6, 0), Is.True);
            Assert.That(SegmentGeometry.Intersects(0, 0, 1, 0, 2, 0, 3, 0), Is.False);
            Assert.That(SegmentGeometry.DistanceToSegment(2, 1, 0, 0, 4, 0), Is.EqualTo(1));
        }

        [Test]
        public void ReusableRenderingAssemblyDoesNotDependOnBattleOrInput()
        {
            var references = typeof(MapWorldView).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Assert.That(references.Any(n => n.StartsWith("Game.Battle") || n == "Unity.InputSystem" || n == "Game.Configuration"), Is.False);
        }
    }
}
