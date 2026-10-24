using System.Collections.Generic;
using Game.Maps.Domain;
using UnityEngine;

namespace Game.Tools.MapPlaytest.Unity.Views
{
    /// <summary>建设网格与幽灵只表达预览，不修改占地、墙网、账户或战斗生命。</summary>
    public sealed class ConstructionPreviewView : MonoBehaviour
    {
        private readonly List<LineRenderer> _lines = new List<LineRenderer>();
        private Material _material;
        private int _used;
        private SpriteRenderer _ghost;
        public void Clear()
        {
            foreach (var line in _lines) line.enabled = false;
            if (_ghost != null) _ghost.enabled = false;
            _used = 0;
        }
        public void Grid(MapLayout map, Camera camera)
        {
            var low = camera.ScreenToWorldPoint(Vector3.zero);
            var high = camera.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height));
            int x0 = Mathf.Max(0, Mathf.FloorToInt((low.x - map.originX) / map.cellWidth));
            int x1 = Mathf.Min(map.columns, Mathf.CeilToInt((high.x - map.originX) / map.cellWidth));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((low.y - map.originY) / map.cellHeight));
            int y1 = Mathf.Min(map.rows, Mathf.CeilToInt((high.y - map.originY) / map.cellHeight));
            var color = new Color(0.85f, 0.95f, 0.9f, 0.18f);
            for (int x = x0; x <= x1; x++)
                Segment(new Vector2(map.originX + x * map.cellWidth, map.originY + y0 * map.cellHeight),
                    new Vector2(map.originX + x * map.cellWidth, map.originY + y1 * map.cellHeight), color, 0.015f);
            for (int y = y0; y <= y1; y++)
                Segment(new Vector2(map.originX + x0 * map.cellWidth, map.originY + y * map.cellHeight),
                    new Vector2(map.originX + x1 * map.cellWidth, map.originY + y * map.cellHeight), color, 0.015f);
        }
        public void Footprint(MapLayout map, int x, int y, int width, int height, bool valid, Sprite sprite = null)
        {
            var color = valid ? new Color(0.3f, 1, 0.55f, 0.95f) : new Color(1, 0.25f, 0.25f, 0.95f);
            float minX = map.originX + x * map.cellWidth;
            float minY = map.originY + y * map.cellHeight;
            for (int dy = 0; dy < height; dy++)
                for (int dx = 0; dx < width; dx++)
                    Rectangle(new Rect(minX + dx * map.cellWidth, minY + dy * map.cellHeight, map.cellWidth, map.cellHeight), color);
            // 十字是固定左下角锚点，矩形覆盖所有实际占地格。
            Segment(new Vector2(minX - 0.12f, minY), new Vector2(minX + 0.12f, minY), Color.white, 0.065f);
            Segment(new Vector2(minX, minY - 0.12f), new Vector2(minX, minY + 0.12f), Color.white, 0.065f);
            if (sprite == null) return;
            if (_ghost == null)
            {
                var child = new GameObject("Building ghost");
                child.transform.SetParent(transform, false);
                _ghost = child.AddComponent<SpriteRenderer>();
                _ghost.sortingOrder = 9998;
            }
            _ghost.enabled = true;
            _ghost.sprite = sprite;
            _ghost.color = new Color(color.r, color.g, color.b, 0.5f);
            float scale = width * map.cellWidth / sprite.bounds.size.x;
            _ghost.transform.localScale = Vector3.one * scale;
            _ghost.transform.position = new Vector3(minX + width * map.cellWidth * 0.5f,
                minY + map.cellHeight * 0.5f + sprite.bounds.extents.y * scale, -0.12f);
        }
        public void Range(Vector2 center, float radius)
        {
            if (radius <= 0) return;
            var line = Next(new Color(0.55f, 0.9f, 1, 0.65f), 0.03f);
            line.loop = true;
            line.positionCount = 64;
            for (int i = 0; i < 64; i++)
            {
                float angle = i * Mathf.PI * 2 / 64;
                line.SetPosition(i, new Vector3(center.x + Mathf.Cos(angle) * radius, center.y + Mathf.Sin(angle) * radius, -0.15f));
            }
        }
        public void Rectangle(Rect rect, Color color)
        {
            var line = Next(color, 0.045f);
            line.loop = true;
            line.positionCount = 4;
            line.SetPositions(new[] { new Vector3(rect.xMin, rect.yMin, -0.15f), new Vector3(rect.xMax, rect.yMin, -0.15f),
                new Vector3(rect.xMax, rect.yMax, -0.15f), new Vector3(rect.xMin, rect.yMax, -0.15f) });
        }
        public void Segment(Vector2 a, Vector2 b, Color color, float width = 0.08f)
        {
            var line = Next(color, width);
            line.loop = false;
            line.positionCount = 2;
            line.SetPosition(0, new Vector3(a.x, a.y, -0.15f));
            line.SetPosition(1, new Vector3(b.x, b.y, -0.15f));
        }
        private LineRenderer Next(Color color, float width)
        {
            if (_material == null) _material = new Material(Shader.Find("Sprites/Default"));
            if (_used == _lines.Count)
            {
                var child = new GameObject("Construction outline");
                child.transform.SetParent(transform, false);
                var created = child.AddComponent<LineRenderer>();
                created.sharedMaterial = _material;
                created.useWorldSpace = true;
                created.sortingOrder = 10000;
                _lines.Add(created);
            }
            var line = _lines[_used++];
            line.enabled = true;
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;
            return line;
        }
        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
