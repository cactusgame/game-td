using System;
using System.IO;
using System.Linq;
using Game.Presentation.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Game.Tools.Prototype
{
    /// <summary>可重复执行的动画资源导入；只更新动画引用，不重建关卡或 UI。</summary>
    public static class BattleAnimationSetup
    {
        public static void Apply()
        {
            var hero = Import("Assets/Content/Units/Heroes/Knight/Sprites/knight-actions.png");
            var goblin = Import("Assets/Content/Units/Enemies/Goblin/Sprites/goblin-actions.png");
            var runner = Import("Assets/Content/Units/Enemies/Runner/Sprites/runner-actions.png");
            var view = UnityEngine.Object.FindAnyObjectByType<BattleView>();
            if (view == null)
                throw new InvalidOperationException("Open the battle scene before applying animations.");
            Undo.RecordObject(view, "Apply unit animations");
            view.ConfigureAnimations(hero, goblin, runner);
            ImportDeathAssets();
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            EditorSceneManager.SaveScene(view.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Applied unit animations and configured delayed monster removal.");
        }

        public static void ImportDeathAssets()
        {
            Import("Assets/Content/Units/Enemies/Goblin/Sprites/goblin-death.png");
            Import("Assets/Content/Units/Enemies/Runner/Sprites/runner-death.png");
        }

        public static UnitAnimationSet Import(string path)
        {
            bool death = path.EndsWith("-death.png", StringComparison.Ordinal);
            int grid = death ? 2 : 4;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Missing action sheet: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 256;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null)
                throw new InvalidOperationException("No sprite data provider: " + path);
            provider.InitSpriteEditorDataProvider();
            var capability = provider.GetDataProvider<ISpriteFrameEditCapability>();
            var required = new[]{EEditCapability.CreateAndDeleteSprite, EEditCapability.EditSpriteName, EEditCapability.EditSpriteRect, EEditCapability.EditPivot};
            if (capability == null || required.Any(c => !capability.GetEditCapability().HasCapability(c)))
                throw new InvalidOperationException("Importer does not support safe animation slicing: " + path);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            float cellWidth = width / (float)grid, cellHeight = height / (float)grid;
            // 生成图的留白可能略有不同。读取源图只用于计算脚底 pivot，不修改图像像素。
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!source.LoadImage(File.ReadAllBytes(path)))
                throw new InvalidOperationException("Cannot inspect action sheet alpha: " + path);
            var pixels = source.GetPixels32();
            UnityEngine.Object.DestroyImmediate(source);
            // 两处伸展攻击跨过数学网格，但仍处于相邻角色之间的透明留白内。
            // 将这段留白划给攻击帧，同时从邻帧排除，保留完整手爪/挥击弧且不串帧。
            Rect FrameRect(int col, int row)
            {
                float left = col * cellWidth, right = (col + 1) * cellWidth;
                if (!death && row == 2 && path.Contains("runner-actions"))
                {
                    if (col == 1)
                        right += cellWidth * 0.08f;
                    if (col == 2)
                        left += cellWidth * 0.08f;
                }

                if (!death && row == 2 && path.Contains("goblin-actions"))
                {
                    if (col == 2)
                        right += cellWidth * 0.08f;
                    if (col == 3)
                        left += cellWidth * 0.08f;
                }

                return new Rect(left, height - (row + 1) * cellHeight, right - left, cellHeight);
            }

            int Bottom(int col, int row)
            {
                var frame = FrameRect(col, row);
                int left = Mathf.CeilToInt(frame.xMin), right = Mathf.FloorToInt(frame.xMax);
                int bottom = Mathf.CeilToInt(height - (row + 1) * cellHeight), top = Mathf.FloorToInt(height - row * cellHeight);
                for (int y = bottom; y < top; y++)
                    for (int x = left; x < right; x++)
                        if (pixels[y * width + x].a > 127)
                            return y;
                throw new InvalidOperationException("Empty animation frame: " + path);
            }

            int idleBottom = Bottom(0, 0), idleTop = idleBottom;
            for (int y = idleBottom; y < height; y++)
                for (int x = 0; x < Mathf.FloorToInt(cellWidth); x++)
                    if (pixels[y * width + x].a > 127)
                        idleTop = y;
            string[] actions = {"Idle", "Move", "Attack", "Hurt"};
            var previous = provider.GetSpriteRects();
            var rects = new SpriteRect[grid * grid];
            for (int row = 0; row < grid; row++)
                for (int col = 0; col < grid; col++)
                {
                    string name = death ? "Death_" + (row * grid + col) : actions[row] + "_" + col;
                    var existing = previous.FirstOrDefault(r => r.name == name);
                    var frame = FrameRect(col, row);
                    rects[row * grid + col] = new SpriteRect{name = name, spriteID = existing != null ? existing.spriteID : GUID.Generate(), rect = frame, alignment = SpriteAlignment.Custom, pivot = new Vector2(((col + 0.5f) * cellWidth - frame.x) / frame.width, (Bottom(col, row) - frame.y) / cellHeight)};
                }

            provider.SetSpriteRects(rects);
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (names == null)
                throw new InvalidOperationException("Missing sprite name/file ID provider.");
            names.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            if (sprites.Length != grid * grid)
                throw new InvalidOperationException("Incorrect sliced frame count: " + path);
            string setPath = death ? path.Replace("-death.png", "-actions.asset") : path.Replace(".png", ".asset");
            var set = AssetDatabase.LoadAssetAtPath<UnitAnimationSet>(setPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<UnitAnimationSet>();
                AssetDatabase.CreateAsset(set, setPath);
            }

            Sprite[] Frames(string action) => sprites.Where(s => s.name.StartsWith(action + "_", StringComparison.Ordinal)).OrderBy(s => s.name).ToArray();
            if (death)
            {
                set.Death = Frames("Death");
            }
            else
            {
                set.Idle = Frames("Idle");
                set.Move = Frames("Move");
                set.Attack = Frames("Attack");
                set.Hurt = Frames("Hurt");
            }

            EditorUtility.SetDirty(set);
            return set;
        }
    }
}
