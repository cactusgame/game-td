using Game.Infrastructure.Maps.Serialization;
using System.Collections;
using Game.Tools.MapAuthoring.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tools.MapAuthoring.Tests
{
    public sealed class MapCanvasNavigationTests
    {
        [UnityTest]
        public IEnumerator ToolOrderAndShortcuts_PanFirstMoveSecond_IgnoreTextInput()
        {
            var window = ScriptableObject.CreateInstance<MapEditorWindow>();
            try
            {
                window.Show();
                yield return null;
                var root = window.rootVisualElement;
                var tools = root.Q(className: "palette-tools");
                Assert.That(tools[0].name, Is.EqualTo("tool-Pan"));
                Assert.That(tools[1].name, Is.EqualTo("tool-Select"));
                var canvas = root.Q<MapCanvas>();
                var palette = root.Q<MapPalette>();
                canvas.Focus();
                SendKey(canvas, KeyCode.Q);
                Assert.That(palette.Tool, Is.EqualTo(MapEditorTool.Pan));
                SendKey(canvas, KeyCode.W);
                Assert.That(palette.Tool, Is.EqualTo(MapEditorTool.Select));
                Assert.That(canvas.PanTool, Is.False);
                SendKey(canvas, KeyCode.Q);
                var field = root.Q<TextField>("mapId");
                field.Focus();
                SendKey(field, KeyCode.W);
                Assert.That(palette.Tool, Is.EqualTo(MapEditorTool.Pan));
            }
            finally
            {
                window.DiscardChanges();
                window.Close();
            }
        }

        [UnityTest]
        public IEnumerator RightClick_CancelsUncommittedBrushAndPlacement_WithoutRemovingCompletedEdits()
        {
            var window = ScriptableObject.CreateInstance<MapEditorWindow>();
            try
            {
                window.Show();
                yield return null;
                string path = "Temp/map-editor-right-click-test.json";
                System.IO.File.WriteAllText(path, MapJson.WriteDocument(new Game.Maps.Domain.MapLayout()));
                window.OpenMapAsset(path);
                var root = window.rootVisualElement;
                var canvas = root.Q<MapCanvas>();
                var palette = root.Q<MapPalette>();
                string before = MapJson.WriteDocument(canvas.Layout);
                palette.SelectTerrain(Game.MapEngine.Domain.TerrainKind.Water);
                Vector2 position = canvas.LocalToWorld(canvas.contentRect.center);
                using (var down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = position }))
                    canvas.SendEvent(down);
                Assert.That(MapJson.WriteDocument(canvas.Layout), Is.Not.EqualTo(before));
                RightClick(canvas);
                Assert.That(palette.Tool, Is.EqualTo(MapEditorTool.Select));
                Assert.That(canvas.SelectedId, Is.Null);
                Assert.That(MapJson.WriteDocument(canvas.Layout), Is.EqualTo(before));
                Assert.That(window.hasUnsavedChanges, Is.False);
                using (var up = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = position }))
                    canvas.SendEvent(up);
                Assert.That(MapJson.WriteDocument(canvas.Layout), Is.EqualTo(before));

                palette.SelectFacility("tower");
                canvas.Pressed(new Vector2(0.5f, 0.5f), 0);
                canvas.Released();
                string placed = MapJson.WriteDocument(canvas.Layout);
                Assert.That(canvas.Layout.facilities.Count, Is.EqualTo(1));
                // 在工具栏上右键也能退出素材选择，不会撤销已经放下的建筑。
                RightClick(root.Q<Button>("facility-tower"));
                Assert.That(palette.Tool, Is.EqualTo(MapEditorTool.Select));
                Assert.That(canvas.SelectedId, Is.Null);
                Assert.That(root.Q<Button>("facility-tower").ClassListContains("palette-selected"), Is.False);
                Assert.That(MapJson.WriteDocument(canvas.Layout), Is.EqualTo(placed));
                palette.SelectTool(MapEditorTool.Wall);
                RightClick(canvas);
                Assert.That(palette.Tool, Is.EqualTo(MapEditorTool.Select));
                Assert.That(MapJson.WriteDocument(canvas.Layout), Is.EqualTo(placed));
            }
            finally
            {
                window.DiscardChanges();
                window.Close();
            }
        }

        private static void SendKey(VisualElement element, KeyCode code)
        {
            using (var key = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = code }))
                element.SendEvent(key);
        }

        private static void RightClick(VisualElement element)
        {
            Vector2 position = element.LocalToWorld(element.contentRect.center);
            using (var down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 1, mousePosition = position }))
                element.SendEvent(down);
        }

        [UnityTest]
        public IEnumerator PanToolMovesTheViewWithoutEditingTheDocumentAndGridIsIndependent()
        {
            var window = ScriptableObject.CreateInstance<MapEditorWindow>();
            try
            {
                window.Show();
                yield return null;
                var root = window.rootVisualElement;
                var canvas = root.Q<MapCanvas>();
                var palette = root.Q<MapPalette>();
                string before = MapJson.WriteDocument(canvas.Layout);
                canvas.Focus();
                using (var key = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Q }))
                    canvas.SendEvent(key);
                Assert.That(palette.Tool, Is.EqualTo(MapEditorTool.Pan));
                Assert.That(canvas.PanTool, Is.True);
                Vector2 initial = canvas.ViewPan;
                Vector2 start = canvas.LocalToWorld(canvas.contentRect.center);
                using (var down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = start }))
                    canvas.SendEvent(down);
                using (var move = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseDrag, button = 0, mousePosition = start + new Vector2(50, 35) }))
                    canvas.SendEvent(move);
                using (var up = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = start + new Vector2(50, 35) }))
                    canvas.SendEvent(up);
                Assert.That(Vector2.Distance(canvas.ViewPan - initial, new Vector2(50, 35)), Is.LessThan(0.001f));
                Assert.That(MapJson.WriteDocument(canvas.Layout), Is.EqualTo(before));
                var grid = root.Q<Button>("toggleGrid");
                Assert.That(grid, Is.Not.Null);
                Assert.That(grid.GetFirstAncestorOfType<ScrollView>(), Is.Null);
                Click(grid);
                yield return null;
                Assert.That(canvas.ShowGrid, Is.True);
                Assert.That(grid.text, Is.EqualTo("隐藏 Tile 网格"));
                Assert.That(grid.ClassListContains("view-active"), Is.True);
                Click(grid);
                yield return null;
                Assert.That(canvas.ShowGrid, Is.False);
                Assert.That(grid.ClassListContains("view-active"), Is.False);
                var rules = root.Q<Button>("toggleRules");
                Assert.That(rules.parent, Is.SameAs(grid.parent));
                Assert.That(root.Q<Toggle>("showRules"), Is.Null);
                Click(rules);
                yield return null;
                Assert.That(canvas.ShowRules, Is.True);
                Assert.That(canvas.ShowGrid, Is.False);
                Assert.That(rules.ClassListContains("view-active"), Is.True);
                Assert.That(rules.text, Is.EqualTo("隐藏实际通行"));
                Click(rules);
                yield return null;
                Assert.That(canvas.ShowRules, Is.False);
                Assert.That(rules.ClassListContains("view-active"), Is.False);
                Assert.That(MapJson.WriteDocument(canvas.Layout), Is.EqualTo(before));
                Assert.That(window.hasUnsavedChanges, Is.False);
            }
            finally
            {
                window.DiscardChanges();
                window.Close();
            }
        }

        private static void Click(Button button)
        {
            Vector2 position = button.LocalToWorld(button.contentRect.center);
            using (var down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = position }))
                button.SendEvent(down);
            using (var up = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = position }))
                button.SendEvent(up);
        }
    }
}
