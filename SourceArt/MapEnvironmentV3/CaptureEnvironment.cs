using System;
using System.IO;
using System.Linq;
using Game.Infrastructure.Maps.Serialization;
using Game.MapEngine.Domain;
using Game.Maps.Domain;
using Game.Maps.Unity.Assets;
using Game.Maps.Unity.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Actual map renderer in an isolated temporary scene. Gates/log are art staging only:
// this does not register a gameplay door, change navigation or edit the user's map.
public static class CaptureEnvironment
{
    public static string Main()
    {
        var map = CreateMap();
        File.WriteAllText("Assets/Content/Maps/ArtV3/environment-showcase.map.json", MapJson.WriteDocument(map));
        Render(map, "Docs/Validation/environment-v3-overview.png", new Vector2(0, 0), 12.3f);
        Render(map, "Docs/Validation/environment-v3-detail.png", new Vector2(-0.5f, -1.5f), 5.6f);
        AssetDatabase.Refresh();
        return "Captured actual MapWorldView with V3 materials/obstacles and staged open gateway; active scene and editor map untouched.";
    }

    private static MapLayout CreateMap()
    {
        var map = new MapLayout
        {
            id = "environment-v3", columns = 36, rows = 24,
            originX = -18, originY = -12, terrain = new TerrainKind[36 * 24]
        };
        for (int y = 0; y < map.rows; y++)
        {
            float river = 29 + Mathf.Sin(y * 0.25f) * 1.6f;
            for (int x = 0; x < map.columns; x++)
            {
                var kind = TerrainKind.Grass;
                if (Mathf.Abs(x - river) < 1.65f)
                    kind = TerrainKind.Water;
                else if (x < 27 && (Mathf.Abs(y - (8 + Mathf.Sin(x * 0.18f))) < 0.85f ||
                    x >= 16 && x <= 18 && y >= 8 && y <= 16))
                    kind = TerrainKind.Road;
                else if ((x < 8 || y > 21 || x > 32) && (x * 7 + y * 11) % 5 == 0)
                    kind = TerrainKind.Tree;
                else if ((x == 25 && y % 5 == 1) || (y == 3 && x % 8 == 0))
                    kind = TerrainKind.Rock;
                map.terrain[y * map.columns + x] = kind;
            }
        }
        Node(map, "sw", -8, -3);
        Node(map, "nw", -8, 6);
        Node(map, "n-mid", -1, 7);
        Node(map, "ne", 6, 5);
        Node(map, "se", 6, -3);
        Node(map, "gate-w", -2, -3);
        Node(map, "gate-e", 0, -3);
        Edge(map, "sw", "nw");
        Edge(map, "nw", "n-mid");
        Edge(map, "n-mid", "ne");
        Edge(map, "ne", "se");
        Edge(map, "se", "gate-e");
        Edge(map, "gate-w", "sw");
        return map;
    }

    private static void Node(MapLayout map, string id, float x, float y)
    {
        map.wallNodes.Add(new WallNode { id = id, x = x, y = y });
    }

    private static void Edge(MapLayout map, string from, string to)
    {
        map.wallEdges.Add(new WallEdge { id = from + "-" + to, from = from, to = to });
    }

    private static void Render(MapLayout map, string destination, Vector2 focus, float size)
    {
        var active = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var previous = RenderTexture.active;
        RenderTexture target = null;
        Texture2D pixels = null;
        try
        {
            SceneManager.SetActiveScene(scene);
            var visuals = AssetDatabase.LoadAssetAtPath<MapVisualCatalog>(
                "Assets/Content/Maps/Shared/MapVisualCatalog.asset");
            var root = new GameObject("Environment V3 actual map");
            root.AddComponent<MapWorldView>().Rebuild(map, visuals, FacilityCatalogJson.Read(visuals.facilityRules.text));
            Stage(root, "gate-open.png", "gate-open", new Vector2(-1, -3), 2.5f);
            Stage(root, "woodland-obstacles.png", "fallen-log", new Vector2(8, -6), 1.8f);
            foreach (var transform in root.GetComponentsInChildren<Transform>())
                transform.gameObject.layer = 31;
            var camera = new GameObject("Environment capture camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = size;
            camera.transform.position = new Vector3(focus.x, focus.y, -10);
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.2f, 0.14f);
            target = RenderTexture.GetTemporary(2400, 1600, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.WriteAllBytes(destination, pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (pixels != null)
                UnityEngine.Object.DestroyImmediate(pixels);
            if (target != null)
                RenderTexture.ReleaseTemporary(target);
            EditorSceneManager.CloseScene(scene, true);
            if (active.IsValid())
                SceneManager.SetActiveScene(active);
        }
    }

    private static void Stage(GameObject root, string file, string name, Vector2 ground, float width)
    {
        var sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Content/Maps/ArtV3/" + file)
            .OfType<Sprite>().Single(item => item.name == name);
        float height = width * sprite.rect.height / sprite.rect.width;
        var go = new GameObject("Art staging only " + name);
        go.transform.SetParent(root.transform, false);
        go.transform.position = new Vector3(ground.x, ground.y + height * 0.45f, -0.05f);
        go.transform.localScale = Vector3.one * (width / sprite.bounds.size.x);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = MapWorldView.Sort(ground.y) + 1;
    }
}
