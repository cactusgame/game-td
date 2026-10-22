using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Game.MapEngine.Domain;
using Game.Maps.Unity.Assets;

// Run with Unity Pipeline run_script; kept outside Assets to avoid a runtime/tool assembly dependency.
// Importer metadata belongs to Unity. Re-running preserves existing named Sprite IDs.
public static class ImportEnvironment
{
    private const string Folder = "Assets/Content/Maps/ArtV3/";

    public static string Main()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string path in Directory.GetFiles(Folder, "*.png"))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
            importer.textureType = TextureImporterType.Sprite;
            bool sheet = path.EndsWith("woodland-obstacles.png", StringComparison.Ordinal) ||
                path.EndsWith("fortification-components.png", StringComparison.Ordinal) ||
                path.EndsWith("gate-closed.png", StringComparison.Ordinal) ||
                path.EndsWith("gate-open.png", StringComparison.Ordinal);
            importer.spriteImportMode = sheet ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            // Preserve source pixels (including the non-power-of-two material canvases).
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        // Image generation does not produce a reliable equal-cell grid: these measured bounds
        // isolate each complete silhouette instead of clipping trees at an assumed 512px row.
        Slice("woodland-obstacles.png", new[]
        {
            Frame("oak-mature", 0, 20, 565, 560),
            Frame("fir", 590, 10, 420, 570),
            Frame("boulders-moss", 1020, 160, 506, 430),
            Frame("oak-young", 15, 575, 520, 435),
            Frame("rubble-moss", 550, 725, 480, 285),
            Frame("fallen-log", 1040, 690, 490, 320)
        });
        Slice("fortification-components.png", new[]
        {
            Frame("wall-horizontal", 55, 105, 830, 335),
            Frame("bastion-round", 930, 35, 545, 445),
            Frame("bastion-square", 200, 490, 625, 465),
            Frame("wall-end-pier", 1010, 555, 375, 375)
        });
        // Same rect and anchor for both states: never crop each door independently.
        Slice("gate-closed.png", new[] { Frame("gate-closed", 205, 90, 1120, 820) });
        Slice("gate-open.png", new[] { Frame("gate-open", 205, 90, 1120, 820) });

        var catalog = AssetDatabase.LoadAssetAtPath<MapVisualCatalog>(
            "Assets/Content/Maps/Shared/MapVisualCatalog.asset");
        if (catalog == null)
            throw new InvalidOperationException("Map visual catalog is missing.");
        Undo.RecordObject(catalog, "Apply environment art V3");
        foreach (var entry in catalog.terrain)
        {
            entry.ground = entry.kind == TerrainKind.Water ? Sprite("river-water.png") :
                entry.kind == TerrainKind.Dirt || entry.kind == TerrainKind.Road ? Sprite("earth-road.png") :
                Sprite("meadow.png");
            if (entry.kind == TerrainKind.Tree)
                entry.obstacle = Sprite("woodland-obstacles.png", "oak-mature");
            if (entry.kind == TerrainKind.Rock)
                entry.obstacle = Sprite("woodland-obstacles.png", "boulders-moss");
        }
        catalog.treeVariants = new[]
        {
            Sprite("woodland-obstacles.png", "oak-mature"),
            Sprite("woodland-obstacles.png", "fir"),
            Sprite("woodland-obstacles.png", "oak-young")
        };
        catalog.rockVariants = new[]
        {
            Sprite("woodland-obstacles.png", "boulders-moss"),
            Sprite("woodland-obstacles.png", "rubble-moss")
        };
        catalog.wallMasonry = Sprite("wall-masonry.png");
        catalog.wallWalkway = Sprite("wall-walkway.png");
        catalog.gateClosed = Sprite("gate-closed.png");
        catalog.gateOpen = Sprite("gate-open.png");
        catalog.wall = Sprite("fortification-components.png", "wall-horizontal");
        catalog.wallBodyRegion = new Rect(0, 0, 1, 1);
        catalog.wallCorner = Sprite("fortification-components.png", "bastion-square");
        catalog.wallEnd = Sprite("fortification-components.png", "wall-end-pier");
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        foreach (var window in Resources.FindObjectsOfTypeAll<Game.Tools.MapAuthoring.Editor.MapEditorWindow>())
        {
            var canvas = UnityEngine.UIElements.UQueryExtensions.Q<Game.Tools.MapAuthoring.Editor.MapCanvas>(window.rootVisualElement);
            if (canvas == null)
                continue;
            canvas.Layout = canvas.Layout;
            canvas.MarkDirtyRepaint();
        }
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        return "V3 imported: 5 material sprites, 6 obstacles, 4 fortification studies, 2 gateway states. Existing wall dimensions, facilities and map documents preserved.";
    }

    private static Sprite Sprite(string file, string name = null)
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(Folder + file).OfType<Sprite>();
        return (name == null ? sprites.FirstOrDefault() : sprites.FirstOrDefault(item => item.name == name))
            ?? throw new InvalidOperationException("Missing Sprite: " + file + "/" + name);
    }

    private static SpriteRect Frame(string name, int x, int top, int width, int height)
    {
        return new SpriteRect
        {
            name = name,
            rect = new Rect(x, 1024 - top - height, width, height),
            alignment = SpriteAlignment.Custom,
            // MapSceneSprites places a centered sprite 0.45 heights above its ground anchor.
            // Keep the importer centered so Editor quads and runtime SpriteRenderers agree.
            pivot = new Vector2(0.5f, 0.5f)
        };
    }

    private static void Slice(string file, SpriteRect[] frames)
    {
        string path = Folder + file;
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.SaveAndReimport();
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null)
            throw new InvalidOperationException("No Sprite provider: " + path);
        provider.InitSpriteEditorDataProvider();
        var capability = provider.GetDataProvider<ISpriteFrameEditCapability>();
        var needed = new[] { EEditCapability.CreateAndDeleteSprite, EEditCapability.EditSpriteName,
            EEditCapability.EditSpriteRect, EEditCapability.EditPivot };
        if (capability == null || needed.Any(value => !capability.GetEditCapability().HasCapability(value)))
            throw new InvalidOperationException("Sprite editing capability unavailable: " + path);
        var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (names == null)
            throw new InvalidOperationException("Sprite name/ID provider unavailable: " + path);
        var previous = provider.GetSpriteRects();
        foreach (var frame in frames)
        {
            var old = previous.FirstOrDefault(item => item.name == frame.name);
            frame.spriteID = old == null ? GUID.Generate() : old.spriteID;
        }
        provider.SetSpriteRects(frames);
        names.SetNameFileIdPairs(frames.Select(item => new SpriteNameFileIdPair(item.name, item.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
    }
}
