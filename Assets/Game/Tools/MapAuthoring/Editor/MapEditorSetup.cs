using Game.Tools.MapPlaytest.Unity;
using Game.Infrastructure.Maps.Serialization;
using Game.Maps.Unity.Assets;
using System;
using System.IO;
using System.Linq;
using Game.MapEngine.Domain;
using Game.Maps.Domain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.Tools.MapAuthoring.Editor
{
    /// <summary>示例资源和独立试玩场景的制作入口；不修改现有 BattlePrototype 场景。</summary>
    [InitializeOnLoad]
    public static class MapEditorSetup
    {
        public const string SamplePath = "Assets/Content/Maps/Example/example.map.json";
        public const string ScenePath = "Assets/Scenes/Development/MapPlayground.unity";
        private const string ArtPath = "Assets/Content/Maps/Shared/Sprites/";
        private const string RulesPath = "Assets/Content/Maps/Shared/facilities.json";
        private const string PanelPath = "Assets/Content/Maps/Shared/MapPlayPanel.asset";
        private const string PreviewPath = "Assets/Generated/MapPlaytest/preview.map.json";
        private const string PreviewScene = "Assets/Generated/MapPlaytest/MapPlaytest.unity";

        static MapEditorSetup()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool("MapAuthoring.RestorePlayScene", false))
                    return;
                string previous = SessionState.GetString("MapAuthoring.PreviousPlayScene", "");
                EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
                SessionState.SetBool("MapAuthoring.RestorePlayScene", false);
                MapEditorWindow.Open();
            };
        }

        [MenuItem("Game/地图工具/安装示例内容")]
        public static void InstallExamples()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先退出 Play Mode。");
            Directory.CreateDirectory(ArtPath);
            Directory.CreateDirectory("Assets/Content/Maps/Example");
            Directory.CreateDirectory("Assets/Scenes/Development");
            AssetDatabase.Refresh();
            foreach (var file in Directory.GetFiles(ArtPath, "*.png"))
            {
                string path = file.Replace('\\', '/');
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 256;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
            var catalog = AssetDatabase.LoadAssetAtPath<MapVisualCatalog>(MapEditorWindow.VisualPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MapVisualCatalog>();
                AssetDatabase.CreateAsset(catalog, MapEditorWindow.VisualPath);
            }
            if (catalog.blendTerrain)
            {
                Debug.Log("Map art V2 is active; existing material and sprite mappings are preserved.");
                MapEditorWindow.Open();
                return;
            }
            catalog.terrain = new[]
            {
                Terrain(TerrainKind.Grass, "01_grass"), Terrain(TerrainKind.Dirt, "02_dirt"),
                Terrain(TerrainKind.Road, "03_dirt_road"), Terrain(TerrainKind.Water, "06_river_water"),
                Terrain(TerrainKind.Rock, "01_grass", "08_rock_boulder"), Terrain(TerrainKind.Tree, "01_grass", "01_tree_deciduous")
            };
            catalog.facilities = new[]
            {
                new MapVisualCatalog.FacilityVisual { definitionId = "tower", sprite = Sprite("12_garrison_tower") },
                new MapVisualCatalog.FacilityVisual { definitionId = "hall", sprite = Sprite("13_tower_wall_connected") }
            };
            catalog.wall = Sprite("01_wall_horizontal");
            catalog.facilityRules = AssetDatabase.LoadAssetAtPath<TextAsset>(RulesPath);
            EditorUtility.SetDirty(catalog);

            if (!File.Exists(SamplePath))
                File.WriteAllText(SamplePath, MapJson.WriteDocument(CreateExample()));
            AssetDatabase.Refresh();
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1280, 720);
                AssetDatabase.CreateAsset(panel, PanelPath);
            }
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/Game/Tools/MapPlaytest/Unity/UI/MapPlayTheme.tss");
            EditorUtility.SetDirty(panel);
            if (!File.Exists(ScenePath))
                CreateScene(ScenePath, AssetDatabase.LoadAssetAtPath<TextAsset>(SamplePath), catalog, panel);
            AssetDatabase.SaveAssets();
            Debug.Log("Map editor example catalog and standalone playground are ready.");
            MapEditorWindow.Open();
        }

        private static MapVisualCatalog.TerrainVisual Terrain(TerrainKind kind, string ground, string obstacle = null)
        {
            return new MapVisualCatalog.TerrainVisual { kind = kind, ground = Sprite(ground), obstacle = obstacle == null ? null : Sprite(obstacle) };
        }
        private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath + name + ".png");

        private static MapLayout CreateExample()
        {
            var map = new MapLayout { id = "example-valley" };
            for (int y = 0; y < map.rows; y++)
            {
                for (int x = 0; x < map.columns; x++)
                {
                    var terrain = TerrainKind.Grass;
                    if (y == 7 || y == 8)
                        terrain = TerrainKind.Road;
                    if (x == 14 && y != 7 && y != 8)
                        terrain = TerrainKind.Water;
                    if ((x == 3 || x == 4) && y >= 11 && y <= 13)
                        terrain = TerrainKind.Tree;
                    if (x >= 18 && x <= 20 && y == 3)
                        terrain = TerrainKind.Rock;
                    map.terrain[y * map.columns + x] = terrain;
                }
            }
            map.markers.Add(new MapMarker { id = "hero-start", name = "英雄出生", kind = MarkerKind.HeroSpawn, x = -3.5f, y = -0.5f });
            map.markers.Add(new MapMarker { id = "west-entry", name = "西侧入口", kind = MarkerKind.EnemyEntry, x = -10.5f, y = -0.5f });
            map.markers.Add(new MapMarker { id = "east-entry", name = "东侧入口", kind = MarkerKind.EnemyEntry, x = 10.5f, y = 0.5f });
            map.facilities.Add(new InitialFacility { id = "tower-a", definitionId = "tower", x = 10, y = 11 });
            map.facilities.Add(new InitialFacility { id = "hall-a", definitionId = "hall", x = 6, y = 3 });
            map.wallNodes.Add(new WallNode { id = "wall-a", x = -5.5f, y = 2.5f });
            map.wallNodes.Add(new WallNode { id = "wall-b", x = -2.5f, y = 2.5f });
            map.wallEdges.Add(new WallEdge { id = "wall-ab", from = "wall-a", to = "wall-b" });
            return map;
        }

        private static void CreateScene(string path, TextAsset map, MapVisualCatalog visuals, PanelSettings panel)
        {
            var active = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var camera = new GameObject("Map camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.orthographic = true;
                camera.orthographicSize = 10;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.07f, 0.1f, 0.13f);
                var controller = new GameObject("Map playground").AddComponent<MapPlayController>();
                controller.mapJson = map;
                controller.visuals = visuals;
                controller.battleJson = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Content/Combat/Prototype/battle.json");
                controller.mapCamera = camera;
                controller.heroSprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Content/Units/Heroes/Knight/Sprites/knight.png").OfType<Sprite>().FirstOrDefault();
                string enemyPath = "Assets/Content/Units/Enemies/Goblin/Sprites/goblin.png";
                controller.enemySprite = enemyPath == null ? controller.heroSprite : AssetDatabase.LoadAllAssetsAtPath(enemyPath).OfType<Sprite>().FirstOrDefault();
                var document = new GameObject("Map HUD").AddComponent<UIDocument>();
                document.panelSettings = panel;
                document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Game/Tools/MapPlaytest/Unity/UI/MapPlayHud.uxml");
                controller.hud = document;
                EditorSceneManager.SaveScene(scene, path);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (active.IsValid())
                    SceneManager.SetActiveScene(active);
            }
        }

        public static void StartPlaytest(string json)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("试玩已在运行，请先退出。");
            Directory.CreateDirectory("Assets/Generated/MapPlaytest");
            File.WriteAllText(PreviewPath, json);
            AssetDatabase.Refresh();
            var visuals = AssetDatabase.LoadAssetAtPath<MapVisualCatalog>(MapEditorWindow.VisualPath);
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            CreateScene(PreviewScene, AssetDatabase.LoadAssetAtPath<TextAsset>(PreviewPath), visuals, panel);
            SessionState.SetString("MapAuthoring.PreviousPlayScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool("MapAuthoring.RestorePlayScene", true);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewScene);
            EditorApplication.isPlaying = true;
        }
    }
}
