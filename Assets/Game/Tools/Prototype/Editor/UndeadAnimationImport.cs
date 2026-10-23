using System;
using System.IO;
using System.Linq;
using Game.Battle.Unity.Assets;
using Game.Presentation.Views;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Game.Tools.Prototype
{
    /// <summary>已逐帧验收的原始帧导入与标准动作映射；不以文件编号自动猜测动作。</summary>
    public static class UndeadAnimationImport
    {
        public static void Import()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var skeleton = ImportSet("Skeleton", "skeleton", 65, 13, 1, 12, 14, 22, 23, 33, 34, 65, 53f / 76);
            var fallen = ImportSet("Fallen", "fallen", 91, 37, 1, 12, 38, 48, 49, 59, 60, 91, 0.75f);
            const string path = "Assets/Content/Units/Resources/UnitVisualCatalog.asset";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.Refresh();
            var catalog = AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<UnitVisualCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }
            catalog.Animations = new[] { AlienAnimationImport.Load(), skeleton, fallen };
            catalog.Presentation = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Content/Combat/Prototype/presentation.json");
            var view = UnityEngine.Object.FindAnyObjectByType<BattleView>();
            if (view != null)
            {
                var serialized = new SerializedObject(view);
                catalog.White = serialized.FindProperty("_white").objectReferenceValue as Sprite;
                catalog.Circle = serialized.FindProperty("_circle").objectReferenceValue as Sprite;
            }
            // 几何辅助 Sprite 也属于持久资源，不在每次开局生成纹理。
            if (catalog.White == null)
                catalog.White = FindSprite("white");
            if (catalog.Circle == null)
                catalog.Circle = FindSprite("circle");
            if (catalog.White == null || catalog.Circle == null)
                throw new InvalidOperationException("Missing white/circle helper sprites.");
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            BattleAtlasPrebuild.Generate();
        }

        private static Sprite FindSprite(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Sprite", new[] { "Assets/Content" }))
            {
                var sprite = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)).OfType<Sprite>().FirstOrDefault();
                if (sprite != null && sprite.name.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                    return sprite;
            }
            return null;
        }

        private static UnitAnimationSet ImportSet(string folder, string name, int count, int idle, int walkFirst, int walkLast,
            int attackFirst, int attackLast, int deathFirst, int deathLast, int spawnFirst, int spawnLast, float bodyFraction)
        {
            string root = "Assets/Content/Units/Enemies/" + folder;
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var frames = new Sprite[count];
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 1; i <= count; i++)
                {
                    string path = $"{root}/Sprites/{name}_{i}.png";
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = name == "fallen" && i >= spawnFirst ? 200 : 100;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.SaveAndReimport();
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 1; i <= count; i++)
                {
                    string path = $"{root}/Sprites/{name}_{i}.png";
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
                    provider.InitSpriteEditorDataProvider();
                    var capability = provider.GetDataProvider<ISpriteFrameEditCapability>();
                    if (capability == null || !capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))
                        throw new InvalidOperationException("Sprite pivot editing unavailable: " + path);
                    var rects = provider.GetSpriteRects();
                    foreach (var rect in rects)
                    {
                        rect.alignment = SpriteAlignment.Custom;
                        float bottom = name == "skeleton" ? 11 : 6;
                        rect.pivot = new Vector2(0.5f, bottom / rect.rect.height);
                    }
                    provider.SetSpriteRects(rects);
                    provider.Apply();
                    importer.SaveAndReimport();
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            for (int i = 1; i <= count; i++)
                frames[i - 1] = AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/Sprites/{name}_{i}.png");
            var set = ScriptableObject.CreateInstance<UnitAnimationSet>();
            set.name = name + "-actions";
            set.Frames = frames;
            set.BodyHeightFraction = set.DeathBodyHeightFraction = bodyFraction;
            set.FacesLeft = false;
            set.Idle = Clip(idle, idle, 6, true);
            set.Walk = Clip(walkFirst, walkLast, 12, true);
            set.Attack = Clip(attackFirst, attackLast, 16, false);
            set.Hurt = Clip(idle, idle, 8, false);
            set.Death = Clip(deathFirst, deathLast, 12, false);
            set.Spawn = Clip(spawnFirst, spawnLast, 16, false);
            string assetPath = root + "/" + name + "-actions.asset";
            var existing = AssetDatabase.LoadAssetAtPath<UnitAnimationSet>(assetPath);
            if (existing == null)
                AssetDatabase.CreateAsset(set, assetPath);
            else
            {
                EditorUtility.CopySerialized(set, existing);
                UnityEngine.Object.DestroyImmediate(set);
                set = existing;
                EditorUtility.SetDirty(set);
            }
            return set;
        }
        private static UnitActionSettings Clip(int first, int last, float fps, bool loop) =>
            new UnitActionSettings { FirstFrame = first, LastFrame = last, Fps = fps, Loop = loop };
    }
}
