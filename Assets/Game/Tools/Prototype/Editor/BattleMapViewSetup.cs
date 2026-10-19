using System;
using Game.Infrastructure.Configuration;
using Game.Presentation.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tools.Prototype
{
    /// <summary>更新现有地图预览、相机和场景引用，不重建单位/UI 或覆盖整个场景。</summary>
    public static class BattleMapViewSetup
    {
        public const string SettingsPath = "Assets/Content/Combat/Prototype/presentation.json";
        public static TextAsset LoadSettingsAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(SettingsPath);
            if (asset == null)
                throw new InvalidOperationException("Missing presentation JSON: " + SettingsPath);
            return asset;
        }

        public static BattleMapViewSettings EnsureSettings()
        {
            return BattlePresentationSettings.Parse(LoadSettingsAsset().text).Map;
        }

        public static void Apply()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before updating the map preview.");
            BattleSortingSetup.EnsureLayers();
            var view = UnityEngine.Object.FindAnyObjectByType<BattleView>();
            var ground = GameObject.Find("TrainingGround");
            if (view == null || ground == null)
                throw new InvalidOperationException("Open the battle prototype scene before configuring its map.");
            var config = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Content/Combat/Prototype/battle.json");
            var definition = JsonBattleDefinitionSource.Parse(config.text);
            var settings = EnsureSettings();
            var arena = ground.GetComponent<BattleArenaView>();
            if (arena == null)
                arena = Undo.AddComponent<BattleArenaView>(ground);
            var mesh = ground.GetComponent<MeshFilter>().sharedMesh;
            if (mesh == null)
                throw new InvalidOperationException("The existing ground mesh is missing.");
            Undo.RecordObject(mesh, "Resize battle ground");
            BattleArenaMesh.Build(mesh, definition.Width, definition.Height, settings);
            EditorUtility.SetDirty(mesh);
            var groundRenderer = ground.GetComponent<MeshRenderer>();
            Undo.RecordObject(groundRenderer, "Set ground sorting layer");
            groundRenderer.sortingLayerName = BattleRenderSorting.GroundLayer;
            groundRenderer.sortingOrder = 0;
            EditorUtility.SetDirty(groundRenderer);
            Undo.RecordObject(view, "Configure map view");
            view.ConfigureMapView(LoadSettingsAsset(), arena);
            EditorUtility.SetDirty(view);
            var camera = Camera.main;
            if (camera == null)
                throw new InvalidOperationException("The battle camera is missing.");
            Undo.RecordObjects(new UnityEngine.Object[]{camera, camera.transform}, "Configure following camera");
            camera.orthographicSize = settings.VisibleWorldHeight * 0.5f;
            camera.transform.position = new Vector3(0, 0, camera.transform.position.z);
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            EditorSceneManager.SaveScene(view.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Battle map preview updated to {definition.Width} x {definition.Height}; hero starts at the center.");
        }
    }
}
