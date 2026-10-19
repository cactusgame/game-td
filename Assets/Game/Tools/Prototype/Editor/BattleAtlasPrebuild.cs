// [UNITY-SKILL:SPRITEATLAS]
using System.IO;
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
            asset.Add(new Object[]{AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/Content/Units")});
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
