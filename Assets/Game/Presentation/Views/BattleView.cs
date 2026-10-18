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
        [SerializeField] private UnitAnimationSet knightAnimations, goblinAnimations, runnerAnimations;
        [SerializeField] private UnitDeathSettings deathSettings;
        [SerializeField] private BattleMapViewSettings mapViewSettings;
        [SerializeField] private BattleArenaView arenaView;
        public void ConfigureMapView(BattleMapViewSettings settings, BattleArenaView arena)
        {
            mapViewSettings = settings;
            arenaView = arena;
        }
        public void ConfigureDeath(UnitDeathSettings settings) { deathSettings = settings; }
        public void ConfigureAnimations(UnitAnimationSet hero, UnitAnimationSet basic, UnitAnimationSet fast)
        { knightAnimations = hero; goblinAnimations = basic; runnerAnimations = fast; }
        private BattleSession _session;
        private int _generation;
        private readonly Dictionary<ulong, UnitView> _views = new Dictionary<ulong, UnitView>();
        // ECS 保留死亡快照直到重启；记录已移除 ID，避免尸体视图下一帧被重新创建。
        private readonly HashSet<ulong> _removedViews = new HashSet<ulong>();
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
                _removedViews.Clear();
                _generation = _session.Generation;
                arenaView.Configure(_session.Definition.Width, _session.Definition.Height, mapViewSettings);
            }
            Vector2 heroPosition = Vector2.zero;
            foreach (var unit in _session.Units)
            {
                if (unit.Kind == UnitKind.Hero) heroPosition = new Vector2(unit.X, unit.Y);
                if (_removedViews.Contains(unit.Id)) continue;
                if (!_views.TryGetValue(unit.Id, out var view))
                {
                    // 首次看到某个 ID 才创建显示对象；普通帧复用。
                    var definition = _session.Definition.GetUnit(unit.Kind);
                    var sprite = unit.Kind == UnitKind.Hero ? knight : unit.Kind == UnitKind.Goblin ? goblin : runner;
                    var animations = unit.Kind == UnitKind.Hero ? knightAnimations : unit.Kind == UnitKind.Goblin ? goblinAnimations : runnerAnimations;
                    view = new UnitView(transform, sprite, white, circle, definition.VisualHeight, definition.Radius,
                        unit.Kind == UnitKind.Hero ? new Color(0.38f, 0.78f, 0.98f) : new Color(0.83f, 0.38f, 0.29f), animations, deathSettings);
                    _views.Add(unit.Id, view);
                }
                // 传递的是数据副本。显示位置、血条和朝向不会反向修改模拟的真实状态。
                view.Render(unit, _session.ElapsedTime, Time.deltaTime);
                if (view.ReadyForRemoval)
                {
                    view.Destroy();
                    _views.Remove(unit.Id);
                    _removedViews.Add(unit.Id);
                }
            }
            // 相机只读取英雄地面快照，移动镜头不修改 ECS 位置或寻路地图。
            float visibleHeight = Mathf.Max(1, mapViewSettings.VisibleWorldHeight);
            float margin = Mathf.Max(mapViewSettings.BorderThickness, mapViewSettings.EdgeMargin);
            var center = BattleCameraFraming.Center(heroPosition, _session.Definition.Width,
                _session.Definition.Height, visibleHeight, battleCamera.aspect, margin, mapViewSettings.TopEdgeMargin);
            battleCamera.orthographicSize = visibleHeight * 0.5f;
            battleCamera.transform.position = new Vector3(center.x, center.y, battleCamera.transform.position.z);
        }
    }
}
