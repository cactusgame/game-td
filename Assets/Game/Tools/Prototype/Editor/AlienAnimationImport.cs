using System;
using System.IO;
using Game.Presentation.Views;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Game.Tools.Prototype
{
    /// <summary>导入已切好的 Alien 帧；保留源画布和固定脚底锚点，不逐帧裁剪或重定位。</summary>
    public static class AlienAnimationImport
    {
        public const string SetPath = "Assets/Content/Units/Heroes/Alien/alien-actions.asset";
        private const string Folder = "Assets/Content/Units/Heroes/Alien/Sprites";

        public static UnitAnimationSet Load()
        {
            return AssetDatabase.LoadAssetAtPath<UnitAnimationSet>(SetPath)
                ?? throw new InvalidOperationException("Run AlienAnimationImport.Import before applying animations.");
        }

        public static void Import()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var paths = new string[94];
            for (int index = 0; index < paths.Length; index++)
            {
                paths[index] = $"{Folder}/alien_{index + 1:000}.png";
                if (!File.Exists(paths[index]))
                    throw new FileNotFoundException("Missing Alien source frame", paths[index]);
            }

            // 两次批量提交：先建立 Single Sprite，再通过能力受检的数据提供器写 pivot。
            // 每帧单独 SaveAndReimport 会导致共享图集反复打包。
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in paths)
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 100;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in paths)
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
                    if (provider == null)
                        throw new InvalidOperationException("Missing Sprite provider: " + path);
                    provider.InitSpriteEditorDataProvider();
                    var capability = provider.GetDataProvider<ISpriteFrameEditCapability>();
                    if (capability == null || !capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))
                        throw new InvalidOperationException("Importer cannot edit pivot: " + path);
                    var rects = provider.GetSpriteRects();
                    foreach (var rect in rects)
                    {
                        rect.alignment = SpriteAlignment.Custom;
                        rect.pivot = new Vector2(0.5f, 68f / 224f);
                    }
                    provider.SetSpriteRects(rects);
                    provider.Apply();
                    importer.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            var frames = new Sprite[paths.Length];
            for (int index = 0; index < paths.Length; index++)
                frames[index] = AssetDatabase.LoadAssetAtPath<Sprite>(paths[index]);
            var set = AssetDatabase.LoadAssetAtPath<UnitAnimationSet>(SetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<UnitAnimationSet>();
                AssetDatabase.CreateAsset(set, SetPath);
            }
            set.Frames = frames;
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
        }
    }
}
