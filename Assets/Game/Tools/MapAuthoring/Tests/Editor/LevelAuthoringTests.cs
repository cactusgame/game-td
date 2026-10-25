using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Encounter.Domain.Definitions;
using Game.Infrastructure.Maps.Serialization;
using Game.Maps.Domain;
using Game.Tools.MapAuthoring.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Tools.MapAuthoring.Tests
{
    public sealed class LevelAuthoringTests
    {
        [Test]
        public void History_MapAndLevelShareUndoRedoAndReload_WithoutSharingDraftReferences()
        {
            var level = new LevelLayout();
            var session = new MapEditSession(new MapLayout(), true, level);
            level.initialGold = 999;
            var changed = session.Level; changed.initialGold = 200; session.CommitLevel(changed);
            var map = session.Document; map.id = "changed"; session.Commit(map);
            var restored = MapEditSession.Restore(JsonUtility.FromJson<MapEditSession.History>(JsonUtility.ToJson(session.CaptureHistory())));
            Assert.That(restored.Level.initialGold, Is.EqualTo(200));
            restored.Undo(); Assert.That(restored.Document.id, Is.Not.EqualTo("changed"));
            restored.Undo(); Assert.That(restored.Level.initialGold, Is.EqualTo(1000));
            Assert.That(restored.IsDirty, Is.False);
            restored.Redo(); Assert.That(restored.Level.initialGold, Is.EqualTo(200));
        }

        [Test]
        public void LevelJson_RoundTripsZeroOverrideStableIdsAndMap_AndRejectsUnknownVersion()
        {
            var original = LevelJson.ReadDocument(File.ReadAllText("Assets/Content/Levels/Example/citadel.level.json"));
            original.level.waves[0].goldOverrides.Add(new EnemyGoldOverride { enemyId = "enemy.skeleton", gold = 0 });
            string json = LevelJson.WriteDocument(original.map, original.level);
            var restored = LevelJson.ReadDocument(json);
            Assert.That(restored.level.waves[0].goldOverrides[0].gold, Is.Zero);
            Assert.That(restored.level.waves[1].groups[0].id, Is.EqualTo(original.level.waves[1].groups[0].id));
            Assert.That(restored.map.markers[0].id, Is.EqualTo(original.map.markers[0].id));
            Assert.That(restored.map.terrain, Is.EqualTo(original.map.terrain));
            Assert.Throws<System.FormatException>(() => LevelJson.ReadDocument(json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99")));
        }

        [Test]
        public void Window_LoadsLevelAndEditsGold_ThroughSharedHistory()
        {
            var window = ScriptableObject.CreateInstance<MapEditorWindow>();
            try
            {
                window.CreateGUI();
                window.OpenMapAsset("Assets/Content/Levels/Example/citadel.level.json");
                typeof(MapEditorWindow).GetField("_showLevel", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(window, true);
                typeof(MapEditorWindow).GetMethod("Refresh", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, new object[] { true });
                window.Show();
                Assert.That(window.rootVisualElement.Q<Button>("addWave"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<Button>("addSpawnGroup"), Is.Not.Null);
                var initialGold = window.rootVisualElement.Query<IntegerField>().ToList().Single(field => field.label == "初始金币");
                initialGold.value = 123;
                var session = (MapEditSession)typeof(MapEditorWindow).GetField("_session", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                Assert.That(session.Level.initialGold, Is.EqualTo(123));
                session.Undo();
                Assert.That(session.Level.initialGold, Is.EqualTo(1000));
                Assert.That(session.Level.waves.Count, Is.EqualTo(3));
            }
            finally { Object.DestroyImmediate(window); }
        }
    }
}
