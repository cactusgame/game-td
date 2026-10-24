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
    /// <summary>已逐帧确认的兵营素材动作映射，复用已有图集构建流程。</summary>
    public static class SoldierAnimationImport
    {
        public static void Import()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var soldier = ImportSet("Soldier", "soldier", 43, 1, 6, 7, 9, 10, 11, 15, 13f / 104, 46f / 104, false);
            // 指定的mage shooter素材没有行走和倒地序列，使用站立移动反馈和图像倾倒收尾。
            var archer = ImportSet("Archer", "mage shooter", 19, 1, 1, 3, 10, 1, 1, 1, 0, 47f / 68, true);
            var catalog = AssetDatabase.LoadAssetAtPath<UnitVisualCatalog>("Assets/Content/Units/Resources/UnitVisualCatalog.asset");
            catalog.Animations = catalog.Animations.Where(a => a != null && a.name != soldier.name && a.name != archer.name)
                .Concat(new[] { soldier, archer }).ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            BattleAtlasPrebuild.Generate();
        }

        private static UnitAnimationSet ImportSet(string folder, string source, int count, int walkFirst, int walkLast,
            int attackFirst, int attackLast, int hurt, int deathFirst, int deathLast, float pivotY, float fraction, bool fallbackDeath)
        {
            string root = "Assets/Content/Units/Allies/" + folder;
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var frames = new Sprite[count];
            for (int i = 1; i <= count; i++)
            {
                string path = $"{root}/Sprites/{source}_{i}.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing soldier sprite: " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
                var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
                provider.InitSpriteEditorDataProvider();
                var capability = provider.GetDataProvider<ISpriteFrameEditCapability>();
                if (capability == null || !capability.GetEditCapability().HasCapability(EEditCapability.EditPivot))
                    throw new InvalidOperationException("Sprite pivot editing unavailable: " + path);
                var rects = provider.GetSpriteRects();
                foreach (var rect in rects)
                {
                    rect.alignment = SpriteAlignment.Custom;
                    rect.pivot = new Vector2(0.5f, pivotY);
                }
                provider.SetSpriteRects(rects);
                provider.Apply();
                importer.SaveAndReimport();
                frames[i - 1] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            string assetPath = root + "/" + folder.ToLowerInvariant() + "-actions.asset";
            var set = AssetDatabase.LoadAssetAtPath<UnitAnimationSet>(assetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<UnitAnimationSet>();
                AssetDatabase.CreateAsset(set, assetPath);
            }
            set.name = folder.ToLowerInvariant() + "-actions";
            set.Frames = frames;
            set.BodyHeightFraction = set.DeathBodyHeightFraction = fraction;
            set.FacesLeft = false;
            set.UseFallbackDeath = fallbackDeath;
            set.Idle = Clip(1, 1, 6, true);
            set.Walk = Clip(walkFirst, walkLast, 10, true);
            set.Attack = Clip(attackFirst, attackLast, 12, false);
            set.Hurt = Clip(hurt, hurt, 8, false);
            set.Death = Clip(deathFirst, deathLast, fallbackDeath ? 2 : 10, false);
            EditorUtility.SetDirty(set);
            return set;
        }
        private static UnitActionSettings Clip(int first, int last, float fps, bool loop) =>
            new UnitActionSettings { FirstFrame = first, LastFrame = last, Fps = fps, Loop = loop };
    }
}
