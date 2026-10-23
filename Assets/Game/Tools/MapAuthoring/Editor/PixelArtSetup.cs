using System;
using System.IO;
using System.Linq;
using Game.MapEngine.Domain;
using Game.Maps.Domain;
using Game.Maps.Unity.Assets;
using Game.Infrastructure.Maps.Serialization;
using UnityEditor;
using UnityEngine;

namespace Game.Tools.MapAuthoring.Editor
{
    /// <summary>像素套装制作入口。仅创建独立资源；通过现有共享墙体与地图渲染管线显示。</summary>
    public static class PixelArtSetup
    {
        public const string Root = "Assets/Content/Maps/Styles/PixelKingdom";
        public const string MapPath = Root + "/reference-citadel.map.json";
        public const string CatalogPath = Root + "/PixelKingdom.asset";

        [MenuItem("Game/地图工具/安装像素城镇套装")]
        public static void Install()
        {
            if (AssetDatabase.LoadAssetAtPath<MapVisualCatalog>(CatalogPath) != null)
            {
                MapArtSelection.Select(AssetDatabase.LoadAssetAtPath<MapVisualCatalog>(CatalogPath));
                MapEditorWindow.Open();
                return;
            }
            var sheetPath = Root + "/kingdom-sheet.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(sheetPath);
            if (importer == null)
                throw new InvalidOperationException("像素材质源文件尚未生成：" + sheetPath);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = true;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath);
            var visual = ScriptableObject.CreateInstance<MapVisualCatalog>();
            visual.name = "Pixel Kingdom";
            AssetDatabase.CreateAsset(visual, CatalogPath);
            var sprites = new Sprite[16];
            int[] xs = { 0, 345, 630, 950, 1254 };
            int[] ys = { 0, 375, 670, 975, 1254 };
            for (int i = 0; i < sprites.Length; i++)
            {
                int col = i % 4, row = i / 4;
                var rect = new Rect(xs[col], sheet.height - ys[row + 1], xs[col + 1] - xs[col], ys[row + 1] - ys[row]);
                // 单元内透明边界裁剪：建筑尺寸按有效画幅，不把留白算入占地比例。
                if (i < 12)
                {
                    int minX = (int)rect.width, minY = (int)rect.height, maxX = 0, maxY = 0;
                    for (int y = 0; y < rect.height; y++)
                    for (int x = 0; x < rect.width; x++)
                    {
                        if (sheet.GetPixel((int)rect.x + x, (int)rect.y + y).a < 0.2f) continue;
                        minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                    }
                    if (maxX >= minX && maxY >= minY)
                        rect = new Rect(rect.x + minX, rect.y + minY, maxX - minX + 1, maxY - minY + 1);
                }
                if (i >= 12)
                    rect = new Rect(xs[col] + (col == 0 ? 75 : 45), sheet.height - 1190, 190, 170);
                sprites[i] = Sprite.Create(sheet, rect, Vector2.one * 0.5f, 64, 0, SpriteMeshType.FullRect);
                sprites[i].name = "kingdom-" + i;
                AssetDatabase.AddObjectToAsset(sprites[i], visual);
            }
            visual.facilityRules = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Content/Configuration/Resources/GameConfig/facilities.json");
            string[] ids = { "hall", "house", "church", "market", "foundry", "tower", "farm", "well", "mage", "rubble" };
            int[] indices = { 0, 1, 2, 3, 4, 5, 6, 7, 2, 11 };
            visual.facilities = ids.Select((id, i) => new MapVisualCatalog.FacilityVisual { definitionId = id, sprite = sprites[indices[i]] }).ToArray();
            visual.terrain = Enum.GetValues(typeof(TerrainKind)).Cast<TerrainKind>().Select(kind =>
                new MapVisualCatalog.TerrainVisual
                {
                    kind = kind,
                    ground = sprites[kind == TerrainKind.Dirt || kind == TerrainKind.Road ? 13 : 12],
                    obstacle = kind == TerrainKind.Tree ? sprites[8] : kind == TerrainKind.Rock ? sprites[10] : null
                }).ToArray();
            // 水面沿用既有语义和资源；不把草地误画成可通行水面。
            var original = AssetDatabase.LoadAssetAtPath<MapVisualCatalog>(MapEditorWindow.VisualPath);
            visual.Terrain(TerrainKind.Water).ground = original.Terrain(TerrainKind.Water).ground;
            visual.treeVariants = new[] { sprites[8], sprites[9] };
            visual.rockVariants = new[] { sprites[10], sprites[11] };
            visual.wallJunction = sprites[5];
            visual.facilityWidthScale = 2.5f;
            visual.wall = sprites[14];
            visual.wallBodyRegion = new Rect(0, 0, 1, 1);
            visual.wallMasonry = sprites[14];
            visual.wallWalkway = sprites[15];
            visual.blendTerrain = false;
            EditorUtility.SetDirty(visual);
            File.WriteAllText(MapPath, MapJson.WriteDocument(CreateMap()));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            MapArtSelection.Select(visual);
            MapEditorWindow.Open();
        }

        [MenuItem("Game/地图工具/更新参考图城镇布局")]
        public static void ReviseReference()
        {
            var path = Root + "/open-bastion.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            var visual = AssetDatabase.LoadAssetAtPath<MapVisualCatalog>(CatalogPath);
            visual.wallJunction = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            visual.wallJunctionWidth = 2.2f;
            visual.wallJunctionHeight = 5.2f;
            visual.blendTerrain = true;
            visual.wallHeightScale = 5;
            visual.wallWidthScale = 2.4f;
            visual.keepWidthScale = 2.1f;
            visual.keepHeightScale = 0.6f;
            visual.facilityWidthScale = 2.5f;
            visual.facilities.First(item => item.definitionId == "tower").sprite = visual.wallJunction;
            EditorUtility.SetDirty(visual);
            var map = CreateMap();
            MapJson.LoadDefinition(MapJson.WriteDocument(map), FacilityCatalogJson.Read(visual.facilityRules.text));
            File.WriteAllText(MapPath, MapJson.WriteDocument(map));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            MapArtSelection.Select(visual);
        }

        public static MapLayout CreateMap()
        {
            return ReferenceCitadelLayout.Create();
        }
    }
}


