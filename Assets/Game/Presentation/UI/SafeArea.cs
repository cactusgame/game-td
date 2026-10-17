using UnityEngine;

namespace Game.Presentation.UI
{
    /// <summary>把屏幕安全区域转换为 Canvas 锚点，避开手机刘海等区域；只影响界面布局。</summary>
    public sealed class SafeArea : MonoBehaviour
    {
        private Rect _last;
        private Vector2Int _size;
        private void Update()
        {
            var safe = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            // 仅在分辨率、方向或安全区域变化时更新，避免每帧重复修改布局。
            if (safe == _last && size == _size) return;
            _last = safe; _size = size;
            if (size.x == 0 || size.y == 0) return;
            var rect = (RectTransform)transform;
            rect.anchorMin = safe.min / new Vector2(size.x, size.y);
            rect.anchorMax = safe.max / new Vector2(size.x, size.y);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
