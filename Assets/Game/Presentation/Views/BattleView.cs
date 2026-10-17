using System.Collections.Generic;
using Game.Battle.Application;
using Game.Battle.Domain;
using UnityEngine;
using VContainer;

namespace Game.Presentation.Views
{
    /// <summary>画面入口：读取应用层快照，把业务单位 ID 关联到 Sprite 视图，不访问 ECS 组件。</summary>
    public sealed class BattleView : MonoBehaviour
    {
        [SerializeField] private Sprite knight, goblin, runner, white, circle;
        [SerializeField] private Camera battleCamera;
        private BattleSession _session;
        private int _generation;
        private readonly Dictionary<ulong, UnitView> _views = new Dictionary<ulong, UnitView>();
        public void Configure(Sprite hero, Sprite basic, Sprite fast, Sprite solid, Sprite disc, Camera camera)
        { knight = hero; goblin = basic; runner = fast; white = solid; circle = disc; battleCamera = camera; }
        // VContainer 注入逻辑入口；图片与 Camera 则由场景引用提供。
        [Inject] public void Initialize(BattleSession session) { _session = session; }

        private void LateUpdate()
        {
            // 模拟由入口的 Tick 推进；这里在 LateUpdate 读取最近一次快照并显示。
            if (_session?.Definition == null) return;
            if (_generation != _session.Generation)
            {
                // 重启后单位 ID 可以重新使用，因此先清除上一局的显示对象，避免复用旧配置或残留单位。
                foreach (Transform child in transform) Destroy(child.gameObject);
                _views.Clear();
                _generation = _session.Generation;
            }
            // 根据窗口比例调整正交相机，让完整地图在桌面缩放和手机旋转后仍可见。
            float vertical = _session.Definition.Height * 0.5f + 2.2f;
            float horizontal = (_session.Definition.Width * 0.5f + 1) / Mathf.Max(0.1f, battleCamera.aspect);
            battleCamera.orthographicSize = Mathf.Max(vertical, horizontal);
            foreach (var unit in _session.Units)
            {
                if (!_views.TryGetValue(unit.Id, out var view))
                {
                    // 首次看到某个 ID 才创建显示对象；普通帧复用，死亡状态由 Render 隐藏。
                    var definition = _session.Definition.GetUnit(unit.Kind);
                    var sprite = unit.Kind == UnitKind.Hero ? knight : unit.Kind == UnitKind.Goblin ? goblin : runner;
                    view = new UnitView(transform, sprite, white, circle, definition.VisualHeight, definition.Radius,
                        unit.Kind == UnitKind.Hero ? new Color(0.38f, 0.78f, 0.98f) : new Color(0.83f, 0.38f, 0.29f));
                    _views.Add(unit.Id, view);
                }
                // 传递的是数据副本。显示位置、血条和朝向不会反向修改模拟的真实状态。
                view.Render(unit, _session.ElapsedTime, Time.deltaTime);
            }
        }
    }
}
