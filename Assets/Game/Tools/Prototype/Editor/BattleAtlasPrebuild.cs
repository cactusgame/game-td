// [UNITY-SKILL:SPRITEATLAS]
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Game.Presentation.Views;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.U2D;
using UnityEngine;

namespace Game.Tools.Prototype
{
    /// <summary>仅在编辑器执行的图集准备流程：构建前自动更新，运行时不会生成或重新打包图集。</summary>
    public sealed class BattleAtlasPrebuild : IPreprocessBuildWithReport
    {
        public const string AtlasPath = "Assets/Content/Shared/Atlases/BattleUnits.spriteatlasv2";
        public int callbackOrder => -100;
        public void OnPreprocessBuild(BuildReport report) => Generate();
        // 场景搭建时也调用以准备编辑器预览；正式构建由 OnPreprocessBuild 自动调用。
        public static void Generate()
        {
            EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            if (EditorSettings.spritePackerMode == SpritePackerMode.Disabled)
                throw new BuildFailedException("Sprite packing could not be enabled.");
            Directory.CreateDirectory(Path.GetDirectoryName(AtlasPath));
            // V2 的编辑器创作使用 SpriteAtlasAsset；运行时 SpriteAtlas 只负责读取打包结果。
            var asset = new SpriteAtlasAsset();
            var sprites = new HashSet<Object>();
            foreach (string guid in AssetDatabase.FindAssets("t:UnitAnimationSet", new[] { "Assets/Content/Units" }))
            {
                var set = AssetDatabase.LoadAssetAtPath<UnitAnimationSet>(AssetDatabase.GUIDToAssetPath(guid));
                // 内容制作错误在构建前暴露；缺少可选 Skill 不影响构建。
                _ = new UnitAnimationPlayer(set);
                foreach (var frame in set.Frames)
                {
                    if (frame == null || !AssetDatabase.GetAssetPath(frame).StartsWith("Assets/", System.StringComparison.Ordinal))
                        throw new BuildFailedException("Animation frames must reference valid project Sprite assets: " + set.name);
                    sprites.Add(frame);
                }
            }
            if (sprites.Count == 0)
                throw new BuildFailedException("No authored unit animations were found.");
            // 顺序稳定，反复构建不会因为资产扫描顺序不同而改写整个图集引用列表。
            var packables = sprites.OrderBy(frame => AssetDatabase.GetAssetPath(frame), System.StringComparer.Ordinal)
                .ThenBy(frame => frame.name, System.StringComparer.Ordinal).ToArray();
            asset.Add(packables);
            SpriteAtlasAsset.Save(asset, AtlasPath);
            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(AtlasPath);
            // 使用内置交付：图集随 Player 构建包含，场景仍引用源 Sprite，由 Unity 绑定打包纹理。
            importer.includeInBuild = true;
            var packing = importer.packingSettings;
            // 保留间隔避免采样串色；不旋转，方便角色素材及后续动画帧使用统一方向。
            packing.padding = 4;
            packing.enableRotation = false;
            packing.enableTightPacking = false;
            importer.packingSettings = packing;
            var texture = importer.textureSettings;
            texture.generateMipMaps = false;
            texture.readable = false;
            texture.filterMode = FilterMode.Bilinear;
            importer.textureSettings = texture;
            importer.SetPlatformSettings(new TextureImporterPlatformSettings{name = "DefaultTexturePlatform", maxTextureSize = 2048, format = TextureImporterFormat.Automatic});
            importer.SetPlatformSettings(new TextureImporterPlatformSettings{name = "Android", overridden = true, maxTextureSize = 2048, format = TextureImporterFormat.ASTC_6x6});
            importer.SetPlatformSettings(new TextureImporterPlatformSettings{name = "iPhone", overridden = true, maxTextureSize = 2048, format = TextureImporterFormat.ASTC_6x6});
            importer.SaveAndReimport();
            Debug.Log($"Battle atlas prepared. Sprite packer: {EditorSettings.spritePackerMode}; built-in delivery.");
        }
    }
}
