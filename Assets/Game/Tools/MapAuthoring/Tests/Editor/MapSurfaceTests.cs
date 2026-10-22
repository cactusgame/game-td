using Game.Infrastructure.Maps.Serialization;
using Game.Maps.Unity.Views;
using Game.Maps.Unity.Assets;
using Game.MapEngine.Domain;
using Game.Maps.Domain;
using Game.Tools.MapAuthoring.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tools.MapAuthoring.Tests
{
    public sealed class MapSurfaceTests
    {
        [Test]
        public void AdjacentSurfacePatchesUseIdenticalBoundaryPositionsUvsAndAlpha()
        {
            var visuals = AssetDatabase.LoadAssetAtPath<MapVisualCatalog>(MapEditorWindow.VisualPath);
            var map = new MapLayout();
            map.terrain[2 * map.columns + 2] = TerrainKind.Road;
            map.terrain[2 * map.columns + 3] = TerrainKind.Road;
            map.terrain[3 * map.columns + 3] = TerrainKind.Road;
            var left = MapSurfaceGeometry.Build(map, visuals, 2, 2, 1);
            var right = MapSurfaceGeometry.Build(map, visuals, 3, 2, 1);
            for (int y = 0; y <= 8; y++)
            {
                Assert.That(left.Positions[y * 9 + 8], Is.EqualTo(right.Positions[y * 9]));
                Assert.That(left.Uvs[y * 9 + 8], Is.EqualTo(right.Uvs[y * 9]));
                Assert.That(left.Colors[y * 9 + 8], Is.EqualTo(right.Colors[y * 9]));
            }
        }

        [Test]
        public void OneRoadBrushConnectsHorizontallyVerticallyAndAroundCorners()
        {
            var map = new MapLayout();
            map.terrain[2 * map.columns + 2] = TerrainKind.Road;
            map.terrain[2 * map.columns + 3] = TerrainKind.Road;
            map.terrain[3 * map.columns + 3] = TerrainKind.Road;
            Assert.That(MapSurfaceGeometry.Coverage(map, 3, 2.5f, 1), Is.EqualTo(1));
            Assert.That(MapSurfaceGeometry.Coverage(map, 3.5f, 3, 1), Is.EqualTo(1));
            Assert.That(MapSurfaceGeometry.Coverage(map, 2, 2, 1), Is.Zero);
            Assert.That(MapSurfaceGeometry.Coverage(map, 3.1f, 2.9f, 1), Is.GreaterThan(0));
        }

        [Test]
        public void ShowcaseHasFiveBuildingKindsAndIndependentValidContent()
        {
            var visuals = AssetDatabase.LoadAssetAtPath<MapVisualCatalog>(MapEditorWindow.VisualPath);
            var map = MapArtShowcase.Create(FacilityCatalogJson.Read(visuals.facilityRules.text));
            Assert.That(map.columns * map.rows, Is.EqualTo(3456));
            Assert.DoesNotThrow(() => new MapDefinition(map, FacilityCatalogJson.Read(visuals.facilityRules.text)));
            Assert.That(map.facilities.Exists(item => item.definitionId == "mage"), Is.True);
            Assert.That(map.facilities.Exists(item => item.definitionId == "farm"), Is.True);
            Assert.That(visuals.facilities.Length, Is.EqualTo(5));
        }
    }
}
