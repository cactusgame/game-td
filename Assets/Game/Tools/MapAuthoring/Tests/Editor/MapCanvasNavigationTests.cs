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
