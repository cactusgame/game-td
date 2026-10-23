using Game.Infrastructure.Maps.Serialization;
using Game.Maps.Unity.Views;
using Game.Maps.Unity.Assets;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tools.MapAuthoring.Editor
{
    /// <summary>在临时场景中渲染实际地图表现用于美术验收，不依赖桌面是否被其他窗口遮挡。</summary>
    public static class MapArtCapture
    {
        public static void Capture(string destination, bool detail = false, Game.Maps.Domain.MapLayout layout = null,
            Vector2? focus = null, float? orthographicSize = null, int outputWidth = 2400, int outputHeight = 1600)
        {
            var active = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            RenderTexture target = null;
            Texture2D pixels = null;
            var previousTarget = RenderTexture.active;
            try
            {
                var visuals = MapArtSelection.Current;
                var map = layout ?? MapJson.ReadDocument(File.ReadAllText(MapArtShowcase.Path));
                var root = new GameObject("Art verification map");
                root.AddComponent<MapWorldView>().Rebuild(map, visuals, FacilityCatalogJson.Read(visuals.facilityRules.text));
                foreach (var item in root.GetComponentsInChildren<Transform>())
                    item.gameObject.layer = 31;
                var camera = new GameObject("Art verification camera").AddComponent<Camera>();
                camera.orthographic = true;
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.11f, 0.16f, 0.15f);
                camera.transform.position = detail ? new Vector3(-5, 6, -10) : new Vector3(0, 0.8f, -10);
                camera.orthographicSize = detail ? 10.5f : 26;
                if (focus.HasValue)
                    camera.transform.position = new Vector3(focus.Value.x, focus.Value.y, -10);
                if (orthographicSize.HasValue)
                    camera.orthographicSize = orthographicSize.Value;
                target = RenderTexture.GetTemporary(outputWidth, outputHeight, 24);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination));
                File.WriteAllBytes(destination, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousTarget;
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (target != null) RenderTexture.ReleaseTemporary(target);
                EditorSceneManager.CloseScene(scene, true);
                if (active.IsValid()) SceneManager.SetActiveScene(active);
            }
        }
    }
}


