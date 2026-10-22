using System.IO;
using Game.Infrastructure.Maps.Serialization;
using Game.Maps.Domain;
using Game.Maps.Unity.Assets;
using Game.Maps.Unity.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>实际共用渲染器的开关门对照；仅在临时场景绘制，不修改源文件。</summary>
public static class CaptureGates
{
    public static string Main()
    {
        var map = MapJson.ReadDocument(File.ReadAllText("Assets/Content/Maps/Example/gates.map.json"));
        var second = map.wallEdges[0].Copy();
        second.id = "open-gate";
        second.from = "west-open";
        second.to = "east-open";
        second.gate.isOpen = true;
        map.wallNodes.Add(new WallNode { id = second.from, x = -10.5f, y = -4.5f });
        map.wallNodes.Add(new WallNode { id = second.to, x = 10.5f, y = -4.5f });
        map.wallEdges.Add(second);
        var active = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var previous = RenderTexture.active;
        RenderTexture target = null;
        Texture2D pixels = null;
        try
        {
            SceneManager.SetActiveScene(scene);
            var visuals = AssetDatabase.LoadAssetAtPath<MapVisualCatalog>("Assets/Content/Maps/Shared/MapVisualCatalog.asset");
            var root = new GameObject("Gate renderer verification");
            root.AddComponent<MapWorldView>().Rebuild(map, visuals, FacilityCatalogJson.Read(visuals.facilityRules.text));
            foreach (var child in root.GetComponentsInChildren<Transform>())
                child.gameObject.layer = 31;
            var camera = new GameObject("Gate camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6;
            camera.transform.position = new Vector3(0.5f, -0.4f, -10);
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            target = RenderTexture.GetTemporary(1800, 1200, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            const string path = "Docs/Validation/gates-v3.png";
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            return path;
        }
        finally
        {
            RenderTexture.active = previous;
            if (pixels != null)
                Object.DestroyImmediate(pixels);
            if (target != null)
                RenderTexture.ReleaseTemporary(target);
            EditorSceneManager.CloseScene(scene, true);
            if (active.IsValid())
                SceneManager.SetActiveScene(active);
        }
    }
}
