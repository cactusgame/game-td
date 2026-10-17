using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Battle.Domain;
using Game.Bootstrap;
using Game.Presentation.Input;
using Game.Presentation.UI;
using Game.Presentation.Views;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.LowLevel;

namespace Game.Tools.Prototype
{
    /// <summary>
    /// 编辑器一次性搭建工具：设置素材导入、准备字体/图集、创建并保存场景引用。
    /// 它不是运行时游戏入口；点击 Play 后的入口是场景中的 BattleLifetimeScope/BattleEntryPoint。
    /// </summary>
    public static class BattlePrototypeSetup
    {
        public const string ScenePath = "Assets/Scenes/Gameplay/BattlePrototype.unity";
        private const string Shared = "Assets/Content/Shared/Prototype/";
        private static TMP_FontAsset _font;

        public static void Create()
        {
            // 不覆盖已有场景，也不丢弃当前未保存工作；生成的内容保存后不需要每次启动重建。
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before creating the prototype.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Prototype scene already exists. Open it instead of overwriting edits.");
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the current scene before creating the prototype.");
            Directory.CreateDirectory(Shared);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            AssetDatabase.Refresh();
            ImportCharacter("Assets/Content/Units/Heroes/Knight/Sprites/knight.png");
            ImportCharacter("Assets/Content/Units/Enemies/Goblin/Sprites/goblin.png");
            ImportCharacter("Assets/Content/Units/Enemies/Runner/Sprites/runner.png");
            var white = CreateShape("white", false);
            var circle = CreateShape("circle", true);
            _font = EnsureFont();
            BattleAtlasPrebuild.Generate();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("BattleCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 9.2f;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.075f, 0.13f, 0.115f);

            var config = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Content/Combat/Prototype/battle.json");
            var definition = Game.Infrastructure.Configuration.JsonBattleDefinitionSource.Parse(config.text);
            CreateArena(definition, white);
            var view = new GameObject("UnitViews").AddComponent<BattleView>();
            view.Configure(LoadSprite("Assets/Content/Units/Heroes/Knight/Sprites/knight.png"),
                LoadSprite("Assets/Content/Units/Enemies/Goblin/Sprites/goblin.png"),
                LoadSprite("Assets/Content/Units/Enemies/Runner/Sprites/runner.png"), white, circle, camera);

            var canvas = new GameObject("BattleCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 0.5f;
            var safe = Rect("SafeArea", canvas.transform, Vector2.zero, Vector2.zero, Vector2.zero);
            safe.anchorMax = Vector2.one; safe.offsetMin = safe.offsetMax = Vector2.zero;
            safe.gameObject.AddComponent<SafeArea>();

            var card = Panel("HeroCard", safe, new Vector2(0, 1), new Vector2(24, -24), new Vector2(370, 210));
            Text("Eyebrow", card, "BATTLE LAB  /  01", new Vector2(18, -13), new Vector2(330, 25), 14, new Color(0.88f, 0.77f, 0.49f));
            Text("Title", card, "THE TRAINING GROUNDS", new Vector2(18, -43), new Vector2(340, 30), 24, Color.white);
            var status = Text("Status", card, "INITIALIZING", new Vector2(18, -86), new Vector2(335, 78), 16, new Color(0.86f, 0.90f, 0.85f));
            var healthBack = Image("HeroHealthBackground", card, new Vector2(0, 1), new Vector2(18, -78), new Vector2(330, 5), new Color(0.1f, 0.15f, 0.12f));
            var health = Image("HeroHealth", healthBack.transform, new Vector2(0, 1), Vector2.zero, new Vector2(330, 5), new Color(0.37f, 0.77f, 0.95f));
            health.sprite = white; health.type = UnityEngine.UI.Image.Type.Filled;
            health.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; health.fillOrigin = 0;
            var buttonImage = Image("Restart", card, new Vector2(0, 1), new Vector2(18, -168), new Vector2(135, 29), new Color(0.31f, 0.37f, 0.24f));
            buttonImage.raycastTarget = true;
            var restart = buttonImage.gameObject.AddComponent<UnityEngine.UI.Button>();
            var restartText = Text("Label", buttonImage.transform, "RESTART", Vector2.zero, new Vector2(135, 29), 14, Color.white);
            restartText.alignment = TextAlignmentOptions.Center;
            Text("Counts", card, "6 GOBLINS  +  4 RUNNERS", new Vector2(166, -174), new Vector2(185, 22), 12, new Color(0.7f, 0.76f, 0.65f));
            var hud = canvas.AddComponent<BattleHud>(); hud.Configure(status, health, restart);

            var perfCard = Panel("PerformanceCard", safe, Vector2.one, new Vector2(-24, -24), new Vector2(300, 244));
            var perfText = Text("Metrics", perfCard, "SAMPLING...", new Vector2(17, -12), new Vector2(268, 220), 15, new Color(0.82f, 0.88f, 0.82f));
            var performance = canvas.AddComponent<PerformanceHud>(); performance.Configure(perfText);

            var stickBase = Image("VirtualJoystick", safe, new Vector2(0, 0), new Vector2(40, 38), new Vector2(150, 150), new Color(0.13f, 0.20f, 0.17f, 0.85f));
            stickBase.sprite = circle; stickBase.raycastTarget = true;
            var handle = Image("Handle", stickBase.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54, 54), new Color(0.73f, 0.79f, 0.60f, 0.9f));
            handle.rectTransform.pivot = new Vector2(0.5f, 0.5f); handle.sprite = circle;
            var joystick = stickBase.gameObject.AddComponent<VirtualJoystick>(); joystick.Configure(handle.rectTransform);
            var hint = Rect("Controls", safe, new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(550, 45));
            var hintText = Text("Hint", hint, "ARROW KEYS / WASD / LEFT STICK\nDrag the pad to test touch controls", Vector2.zero, new Vector2(550, 45), 15, new Color(0.79f, 0.84f, 0.70f));
            hintText.alignment = TextAlignmentOptions.Center;

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            // 最后添加依赖组装入口，再保存所有引用；实际 Play 时容器会连接这些已有组件。
            var scope = new GameObject("BattleLifetimeScope").AddComponent<BattleLifetimeScope>();
            scope.ConfigureScene(config, joystick, view, hud, performance);
            EditorUtility.SetDirty(scope);
            EditorSceneManager.SaveScene(scene, ScenePath);
            var oldScenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(oldScenes).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Battle prototype ready: " + ScenePath);
        }

        private static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        // 通过 Unity 导入器与 Sprite DataProvider 设置元数据，不手改 .meta；能力检查失败就终止。
        private static void ImportCharacter(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new FileNotFoundException("Generated character sprite is missing.", path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 256;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.isReadable = false; importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null) throw new InvalidOperationException("Sprite data provider is unavailable.");
            provider.InitSpriteEditorDataProvider();
            var capability = provider.GetDataProvider<ISpriteFrameEditCapability>();
            if (capability == null || !capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))
                throw new InvalidOperationException("Importer cannot edit the sprite pivot.");
            var rects = provider.GetSpriteRects();
            foreach (var rect in rects) { rect.alignment = SpriteAlignment.Custom; rect.pivot = new Vector2(0.5f, 0.12f); }
            provider.SetSpriteRects(rects);
            provider.Apply();
            importer.SaveAndReimport();
        }

        private static Sprite CreateShape(string name, bool disc)
        {
            string path = Shared + name + ".png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                var colors = new Color[64 * 64];
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                {
                    float d = new Vector2(x - 31.5f, y - 31.5f).magnitude;
                    colors[y * 64 + x] = new Color(1, 1, 1, disc ? Mathf.Clamp01(31.5f - d) : 1);
                }
                texture.SetPixels(colors); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 64; importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return LoadSprite(path);
        }

        private static TMP_FontAsset EnsureFont()
        {
            string settingsPath = Shared + "Resources/TMP Settings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(settingsPath);
            if (settings == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                settings = ScriptableObject.CreateInstance<TMP_Settings>();
                AssetDatabase.CreateAsset(settings, settingsPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(settingsPath, ImportAssetOptions.ForceSynchronousImport);
            }
            string path = Shared + "PrototypeFont.asset";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null)
            {
                font = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>("Assets/Content/Shared/Fonts/Inter-Regular.otf"),
                    80, 8, GlyphRenderMode.SDF16, 1024, 1024, AtlasPopulationMode.Dynamic, false);
                if (font == null) throw new InvalidOperationException("Could not create the Inter font asset.");
                font.name = "PrototypeFont";
                font.TryAddCharacters(string.Concat(Enumerable.Range(32, 95).Select(i => (char)i)));
                font.atlasPopulationMode = AtlasPopulationMode.Static;
                AssetDatabase.CreateAsset(font, path);
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
                EditorUtility.SetDirty(font);
            }
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("m_defaultFontAsset").objectReferenceValue = font;
            serialized.FindProperty("m_defaultFontSize").floatValue = 18;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return font;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor; rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
            => Image(name, parent, anchor, position, size, new Color(0.06f, 0.11f, 0.095f, 0.94f)).rectTransform;

        private static UnityEngine.UI.Image Image(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var rect = Rect(name, parent, anchor, position, size);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false;
            return image;
        }

        private static TMP_Text Text(string name, Transform parent, string value, Vector2 position, Vector2 size, float fontSize, Color color)
        {
            var rect = Rect(name, parent, new Vector2(0, 1), position, size);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = _font; label.text = value; label.fontSize = fontSize; label.color = color;
            label.enableAutoSizing = false; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        private static void CreateArena(BattleDefinition definition, Sprite white)
        {
            // A combined visual mesh; ground colors and grid lines never determine collision truth.
            var vertices = new List<Vector3>(); var colors = new List<Color>(); var triangles = new List<int>();
            void Quad(float x, float y, float w, float h, Color color)
            {
                int n = vertices.Count;
                vertices.Add(new Vector3(x, y)); vertices.Add(new Vector3(x + w, y));
                vertices.Add(new Vector3(x + w, y + h)); vertices.Add(new Vector3(x, y + h));
                colors.AddRange(new[] { color, color, color, color });
                triangles.AddRange(new[] { n, n + 2, n + 1, n, n + 3, n + 2 });
            }
            float left = -definition.Width / 2, bottom = -definition.Height / 2;
            for (int y = 0; y < Mathf.CeilToInt(definition.Height); y++)
                for (int x = 0; x < Mathf.CeilToInt(definition.Width); x++)
                    Quad(left + x, bottom + y, Mathf.Min(1, definition.Width - x), Mathf.Min(1, definition.Height - y),
                        (x + y) % 2 == 0 ? new Color(0.30f, 0.40f, 0.245f) : new Color(0.31f, 0.41f, 0.25f));
            var border = new Color(0.53f, 0.56f, 0.35f);
            Quad(left - 0.08f, bottom - 0.08f, definition.Width + 0.16f, 0.08f, border);
            Quad(left - 0.08f, -bottom, definition.Width + 0.16f, 0.08f, border);
            Quad(left - 0.08f, bottom, 0.08f, definition.Height, border);
            Quad(-left, bottom, 0.08f, definition.Height, border);
            var mesh = new Mesh { name = "TrainingGround" };
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Shared + "TrainingGround.asset");
            var material = new Material(Shader.Find("Sprites/Default")) { name = "TrainingGround" };
            material.mainTexture = white.texture;
            AssetDatabase.CreateAsset(material, Shared + "TrainingGround.mat");
            var ground = new GameObject("TrainingGround", typeof(MeshFilter), typeof(MeshRenderer));
            ground.GetComponent<MeshFilter>().sharedMesh = mesh;
            ground.GetComponent<MeshRenderer>().sharedMaterial = material;
            ground.GetComponent<MeshRenderer>().sortingOrder = -100;
        }
    }
}
