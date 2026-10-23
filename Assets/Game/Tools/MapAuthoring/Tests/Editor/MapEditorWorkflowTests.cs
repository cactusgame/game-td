using Game.Infrastructure.Maps.Serialization;
using Game.Maps.Unity.Assets;
using System.Linq;
using Game.MapEngine.Domain;
using Game.Maps.Domain;
using Game.Tools.MapAuthoring.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Tools.MapAuthoring.Tests
{
    /// <summary>沿实际编辑器入口验证工具意图，不依赖屏幕坐标或鼠标速度。</summary>
    public sealed class MapEditorWorkflowTests
    {
        [Test]
        public void ObstaclesPreserveGroundMoveReplaceAndRoundTrip()
        {
            var window = ScriptableObject.CreateInstance<MapEditorWindow>();
            try
            {
                window.CreateGUI();
                var fixture = new MapLayout();
                System.IO.File.WriteAllText("Temp/map-editor-obstacle-test.json", MapJson.WriteDocument(fixture));
                window.OpenMapAsset("Temp/map-editor-obstacle-test.json");
                var canvas = window.rootVisualElement.Q<MapCanvas>();
                var palette = window.rootVisualElement.Q<MapPalette>();
                palette.SelectTerrain(TerrainKind.Tree);
                canvas.Pressed(new Vector2(-7.5f, -3.5f), 0);
                canvas.Released();
                palette.SelectTerrain(TerrainKind.Water);
                canvas.Pressed(new Vector2(-7.5f, -3.5f), 0);
                canvas.Released();
                int source = 4 * fixture.columns + 4;
                Assert.That(canvas.Layout.terrain[source], Is.EqualTo(TerrainKind.Tree));
                Assert.That(canvas.Layout.GroundAt(source), Is.EqualTo(TerrainKind.Water));
                var reopened = MapJson.ReadDocument(MapJson.WriteDocument(canvas.Layout));
                Assert.That(reopened.GroundAt(source), Is.EqualTo(TerrainKind.Water));
                palette.SelectTool(MapEditorTool.Select);
                canvas.Pressed(new Vector2(-7.5f, -3.5f), 0);
                canvas.Dragged(new Vector2(-6.5f, -3.5f));
                canvas.Released();
                Assert.That(canvas.Layout.terrain[source], Is.EqualTo(TerrainKind.Water));
                Assert.That(canvas.Layout.terrain[source + 1], Is.EqualTo(TerrainKind.Tree));
                palette.SelectTerrain(TerrainKind.Rock);
                canvas.Pressed(new Vector2(-6.5f, -3.5f), 0);
                canvas.Released();
                Assert.That(canvas.Layout.terrain[source + 1], Is.EqualTo(TerrainKind.Rock));
                palette.SelectFacility("tower");
                canvas.Pressed(new Vector2(-6.5f, -3.5f), 0);
                canvas.Released();
                Assert.That(canvas.Layout.terrain[source + 1], Is.EqualTo(TerrainKind.Grass));
                palette.SelectTerrain(TerrainKind.Water);
                canvas.Pressed(new Vector2(-6.5f, -3.5f), 0);
                canvas.Released();
                Assert.That(canvas.Layout.facilities.Count, Is.EqualTo(1));
                Assert.That(window.rootVisualElement.Q<VisualElement>("issues").Query<Button>().ToList()
                    .Any(button => button.text.Contains("格子 (5, 4)")), Is.True);
                palette.SelectTerrain(TerrainKind.Grass);
                canvas.Pressed(new Vector2(-6.5f, -3.5f), 0);
                canvas.Released();
                palette.SelectFacility("tower");
                string oldId = canvas.Layout.facilities.Single().id;
                canvas.Pressed(new Vector2(-6.5f, -3.5f), 0);
                canvas.Released();
                Assert.That(canvas.Layout.facilities.Single().id, Is.Not.EqualTo(oldId));
                palette.SelectTerrain(TerrainKind.Dirt);
                canvas.Pressed(new Vector2(-6.5f, -3.5f), 0);
                canvas.Released();
                Assert.That(canvas.Layout.facilities.Count, Is.EqualTo(1));
                palette.SelectTerrain(TerrainKind.Tree);
                canvas.Pressed(new Vector2(-6.5f, -3.5f), 0);
                canvas.Released();
                Assert.That(canvas.Layout.facilities, Is.Empty);
                Assert.That(canvas.Layout.GroundAt(source + 1), Is.EqualTo(TerrainKind.Dirt));
            }
            finally
            {
                window.DiscardChanges();
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void EditorToolsPaintPlaceMoveAndExportOperateOnTheDocument()
        {
            var window = ScriptableObject.CreateInstance<MapEditorWindow>();
            try
            {
                window.CreateGUI();
                // 测试使用独立空地图，不能依赖产品人员可能编辑过的示例文件。
                var fixture = new MapLayout { terrain = new TerrainKind[24 * 16] };
                fixture.markers.Add(new MapMarker { id = "hero", name = "Hero", kind = MarkerKind.HeroSpawn, x = 0.5f, y = 0.5f });
                fixture.markers.Add(new MapMarker { id = "entry", name = "Entry", kind = MarkerKind.EnemyEntry, x = 10.5f, y = 0.5f });
                System.IO.File.WriteAllText("Temp/map-editor-workflow-test.json", MapJson.WriteDocument(fixture));
                window.OpenMapAsset("Temp/map-editor-workflow-test.json");
                var root = window.rootVisualElement;
                var canvas = root.Q<MapCanvas>();
                var palette = root.Q<MapPalette>();
                Assert.That(root.Query<DropdownField>().ToList(), Is.Empty);
                var liveCatalog = FacilityCatalogJson.Read(AssetDatabase.LoadAssetAtPath<MapVisualCatalog>(MapEditorWindow.VisualPath).facilityRules.text);
                Assert.That(root.Query<Button>(className: "palette-card").ToList().Count, Is.EqualTo(6 + 6 + liveCatalog.FacilityIds.Count));
                var before = canvas.Layout.Copy();
                palette.SelectTerrain(TerrainKind.Water);
                Assert.That(palette.Tool, Is.EqualTo(MapEditorTool.Terrain));
                Assert.That(root.Q<Button>("terrain-Water").ClassListContains("palette-selected"), Is.True);
                canvas.Pressed(new Vector2(-11.5f, -7.5f), 0);
                canvas.Dragged(new Vector2(-9.5f, -7.5f));
                canvas.Released();
                Assert.That(canvas.Layout.terrain.Take(3).All(item => item == TerrainKind.Water), Is.True);

                palette.SelectFacility("tower");
                Assert.That(root.Q<Button>("facility-tower").ClassListContains("palette-selected"), Is.True);
                Assert.That(root.Q<Button>("terrain-Water").ClassListContains("palette-selected"), Is.False);
                canvas.Pressed(new Vector2(-7.5f, -3.5f), 0);
                canvas.Released();
                Assert.That(canvas.Layout.facilities.Count, Is.EqualTo(before.facilities.Count + 1));
                string id = canvas.Layout.facilities.Last().id;

                palette.SelectTool(MapEditorTool.Select);
                canvas.Pressed(new Vector2(-7.5f, -3.5f), 0);
                canvas.Dragged(new Vector2(-6.5f, -3.5f));
                canvas.Released();
                Assert.That(canvas.Layout.facilities.Find(item => item.id == id).x, Is.EqualTo(5));
                // 无效移动不提交，也不把设施留在水面。
                canvas.Pressed(new Vector2(-6.5f, -3.5f), 0);
                canvas.Dragged(new Vector2(-11.5f, -7.5f));
                canvas.Released();
                Assert.That(canvas.Layout.facilities.Find(item => item.id == id).x, Is.EqualTo(5));

                palette.SelectTool(MapEditorTool.Wall);
                canvas.Pressed(new Vector2(-8.5f, 3.5f), 0);
                canvas.Released();
                canvas.Pressed(new Vector2(8.5f, 3.5f), 0);
                canvas.Released();
                palette.SelectTool(MapEditorTool.Gate);
                canvas.Pressed(new Vector2(0.5f, 3.5f), 0);
                canvas.Released();
                Assert.That(canvas.Layout.wallEdges.Single().gates.Count > 0, Is.True);
                Assert.That(WallGateRules.Center(canvas.Layout, canvas.Layout.wallEdges.Single(), canvas.Layout.wallEdges.Single().gates[0]).X, Is.EqualTo(0.5f).Within(0.001f));
                root.Q<Toggle>("gateOpen").value = true;
                window.ApplyGate();
                Assert.That(WallGateRules.Center(canvas.Layout, canvas.Layout.wallEdges.Single(), canvas.Layout.wallEdges.Single().gates[0]).X, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(canvas.Layout.wallEdges.Single().gates[0].isOpen, Is.True);
                palette.SelectTool(MapEditorTool.Select);
                canvas.Pressed(new Vector2(0.5f, 3.7f), 0);
                canvas.Dragged(new Vector2(1.5f, 3.7f));
                canvas.Released();
                Assert.That(WallGateRules.Center(canvas.Layout, canvas.Layout.wallEdges.Single(), canvas.Layout.wallEdges.Single().gates[0]).X, Is.EqualTo(1.5f).Within(0.001f));

                palette.SelectTool(MapEditorTool.Gate);
                canvas.Pressed(new Vector2(-4.5f, 3.5f), 0);
                canvas.Released();
                Assert.That(canvas.Layout.wallEdges.Single().gates.Count, Is.EqualTo(2));
                palette.SelectTool(MapEditorTool.Select);
                canvas.Pressed(new Vector2(1.5f, 3.7f), 0);
                canvas.Dragged(new Vector2(-3.5f, 3.7f));
                canvas.Released();
                // 与另一门框重叠的拖动整笔拒绝，原门仍留在原位。
                Assert.That(WallGateRules.Center(canvas.Layout, canvas.Layout.wallEdges.Single(), canvas.Layout.wallEdges.Single().gates[0]).X,
                    Is.EqualTo(1.5f).Within(0.001f));

                var export = MapJson.WriteDocument(canvas.Layout);
                var reopened = MapJson.ReadDocument(export);
                Assert.That(reopened.facilities.Find(item => item.id == id).x, Is.EqualTo(5));
                Assert.That(reopened.terrain[0], Is.EqualTo(TerrainKind.Water));
                // 原始导出定义仍可独立加载，工作中的水面没有改写它。
                var catalog = FacilityCatalogJson.Read(AssetDatabase.LoadAssetAtPath<MapVisualCatalog>(MapEditorWindow.VisualPath).facilityRules.text);
                Assert.DoesNotThrow(() => new MapDefinition(reopened, catalog));
            }
            finally
            {
                window.DiscardChanges();
                Object.DestroyImmediate(window);
            }
        }
    }
}
