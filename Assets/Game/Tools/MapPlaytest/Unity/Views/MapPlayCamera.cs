using System;
using Game.Presentation.Views;
using UnityEngine;

namespace Game.Tools.MapPlaytest.Unity.Views
{
    /// <summary>只负责固定比例的画面跟随，不筛选或暂停镜头外的模拟单位。</summary>
    public sealed class MapPlayCamera
    {
        private readonly Camera _camera;
        private readonly Settings _settings;
        private Rect _mapBounds;

        public MapPlayCamera(Camera camera, TextAsset configuration)
        {
            _camera = camera;
            if (configuration == null)
                throw new InvalidOperationException("Missing MapPlayCamera.json");
            _settings = JsonUtility.FromJson<Settings>(configuration.text);
            if (_settings == null || _settings.schemaVersion != 1 || !Finite(_settings.visibleHeight) ||
                _settings.visibleHeight <= 0 || !Finite(_settings.edgeMargin) || _settings.edgeMargin < 0 ||
                !Finite(_settings.topEdgeMargin) || _settings.topEdgeMargin < 0)
                throw new FormatException("Invalid map play camera settings.");
            _camera.orthographic = true;
        }

        public void SetMap(Rect bounds) => _mapBounds = bounds;

        public void Follow(Vector2 hero, float hudHeightFraction)
        {
            float viewportHeight = 1 - Mathf.Clamp(hudHeightFraction, 0, 0.8f);
            _camera.rect = new Rect(0, 0, 1, viewportHeight);
            _camera.orthographicSize = _settings.visibleHeight * 0.5f;
            // 使用实际可操作视口的宽高比，不能把工具栏下方也算进游戏视野。
            float aspect = _camera.pixelWidth / Mathf.Max(1f, _camera.pixelHeight);
            Vector2 center = BattleCameraFraming.CenterOnMap(hero, _mapBounds, _settings.visibleHeight,
                aspect, _settings.edgeMargin, _settings.topEdgeMargin);
            _camera.transform.position = new Vector3(center.x, center.y, -10);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        [Serializable]
        private sealed class Settings
        {
            public int schemaVersion;
            public float visibleHeight;
            public float edgeMargin;
            public float topEdgeMargin;
        }
    }
}
