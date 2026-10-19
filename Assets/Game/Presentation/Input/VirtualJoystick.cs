using UnityEngine.Serialization;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Presentation.Input
{
    /// <summary>UI 指针适配：鼠标与触摸都能操作，只输出方向，具体位移仍由模拟计算。</summary>
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [FormerlySerializedAs("handle")]
        [SerializeField]
        private RectTransform _handle;
        // 只接受最先按下的指针，防止第二根手指改变或释放第一根手指控制的摇杆。
        private int _pointerId = int.MinValue;
        public Vector2 Direction { get; private set; }

        public void Configure(RectTransform value)
        {
            _handle = value;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (_pointerId != int.MinValue)
                return;
            _pointerId = data.pointerId;
            OnDrag(data);
        }

        public void OnDrag(PointerEventData data)
        {
            if (data.pointerId != _pointerId)
                return;
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, data.position, data.pressEventCamera, out var local))
                return;
            // 把屏幕坐标转为摇杆中心附近的局部坐标，再归一到长度最多为 1 的输入范围。
            Direction = Vector2.ClampMagnitude(local / (rect.rect.width * 0.35f), 1);
            _handle.anchoredPosition = Direction * rect.rect.width * 0.35f;
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (data.pointerId == _pointerId)
                Clear();
        }

        // 松开、禁用和失焦都归零，避免丢失 PointerUp 后英雄一直移动。
        private void Clear()
        {
            _pointerId = int.MinValue;
            Direction = Vector2.zero;
            if (_handle != null)
                _handle.anchoredPosition = Vector2.zero;
        }

        private void OnDisable() => Clear();
        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
                Clear();
        }
    }
}
