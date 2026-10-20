using UnityEngine.Serialization;
using System.Collections.Generic;
using Game.Battle.Application.Sessions;
using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.ValueObjects;
using UnityEngine;
using VContainer;

namespace Game.Presentation.Views
{
    /// <summary>画面入口：读取应用层快照，把业务单位 ID 关联到 Sprite 视图，不访问 ECS 组件。</summary>
    public sealed class BattleView : MonoBehaviour
    {
        [FormerlySerializedAs("white")]
        [SerializeField]
        private Sprite _white;
        [FormerlySerializedAs("circle")]
        [SerializeField]
        private Sprite _circle;
        [FormerlySerializedAs("battleCamera")]
        [SerializeField]
        private Camera _battleCamera;
        [SerializeField]
        private UnitAnimationSet[] _animationSets;
        private readonly Dictionary<string, UnitAnimationSet> _resources = new Dictionary<string, UnitAnimationSet>();
        private readonly Dictionary<string, UnitAnimationPlayer> _players = new Dictionary<string, UnitAnimationPlayer>();
        [FormerlySerializedAs("presentationConfig")]
        [SerializeField]
        private TextAsset _presentationConfig;
        private BattlePresentationSettings _settings;
        private BattleMapViewSettings MapViewSettings => _settings.Map;
        [FormerlySerializedAs("arenaView")]
        [SerializeField]
        private BattleArenaView _arenaView;
        public void ConfigureMapView(TextAsset settings, BattleArenaView arena)
        {
            _presentationConfig = settings;
            _arenaView = arena;
        }

        public void ConfigureAnimations(params UnitAnimationSet[] resources)
        {
            _animationSets = resources;
        }

        /// <summary>在创建视图前验证全部映射，避免资源错误到战斗中途才暴露。</summary>
        private void ValidateAnimations()
        {
            _resources.Clear();
            _players.Clear();
            if (_animationSets == null)
                throw new System.FormatException("Animation resource catalog is missing.");
            foreach (var resource in _animationSets)
            {
                if (resource == null || !_resources.TryAdd(resource.name, resource))
                    throw new System.FormatException("Animation resources must have unique names and valid references.");
            }
            foreach (var profile in _settings.Units)
            {
                if (!_resources.TryGetValue(profile.ResourceId, out var resource))
                    throw new System.FormatException("Missing animation resource: " + profile.ResourceId);
                var player = new UnitAnimationPlayer(resource);
                player.Require(UnitAction.Idle);
                player.Require(UnitAction.Walk);
                _players.Add(profile.UnitKind, player);
            }
            foreach (var unit in _session.Units)
                _settings.AnimationFor(unit.Kind);
        }

        private BattleSession _session;
        private int _generation;
        private readonly Dictionary<ulong, UnitView> _views = new Dictionary<ulong, UnitView>();
        // ECS 保留死亡快照直到重启；记录已移除 ID，避免尸体视图下一帧被重新创建。
        private readonly HashSet<ulong> _removedViews = new HashSet<ulong>();
        public void Configure(Sprite solid, Sprite disc, Camera camera)
        {
            _white = solid;
            _circle = disc;
            _battleCamera = camera;
        }

        // VContainer 注入逻辑入口；图片与 Camera 则由场景引用提供。
        [Inject]
        public void Initialize(BattleSession session)
        {
            _session = session;
        }

        private void LateUpdate()
        {
            // 模拟由入口的 Tick 推进；这里在 LateUpdate 读取最近一次快照并显示。
            if (_session?.Definition == null)
                return;
            if (_generation != _session.Generation)
            {
                _settings = BattlePresentationSettings.Parse(_presentationConfig != null ? _presentationConfig.text : throw new System.InvalidOperationException("Presentation JSON is missing."));
                ValidateAnimations();
                // 重启后单位 ID 可以重新使用，因此先清除上一局的显示对象，避免复用旧配置或残留单位。
                foreach (Transform child in transform)
                    Destroy(child.gameObject);
                _views.Clear();
                _removedViews.Clear();
                _generation = _session.Generation;
                _arenaView.Configure(_session.Definition.Width, _session.Definition.Height, MapViewSettings);
            }

            Vector2 heroPosition = Vector2.zero;
            foreach (var unit in _session.Units)
            {
                if (unit.Kind == UnitKind.Hero)
                    heroPosition = new Vector2(unit.X, unit.Y);
                if (_removedViews.Contains(unit.Id))
                    continue;
                if (!_views.TryGetValue(unit.Id, out var view))
                {
                    // 首次看到某个 ID 才创建显示对象；普通帧复用。
                    view = CreateUnitView(unit);
                    _views.Add(unit.Id, view);
                }

                // 传递的是数据副本。显示位置、血条和朝向不会反向修改模拟的真实状态。
                view.Render(unit, _session.ElapsedTime, Time.deltaTime);
                if (view.IsReadyForRemoval)
                {
                    view.Destroy();
                    _views.Remove(unit.Id);
                    _removedViews.Add(unit.Id);
                }
            }

            // 相机只读取英雄地面快照，移动镜头不修改 ECS 位置或寻路地图。
            float visibleHeight = Mathf.Max(1, MapViewSettings.VisibleWorldHeight);
            float margin = Mathf.Max(MapViewSettings.BorderThickness, MapViewSettings.EdgeMargin);
            var center = BattleCameraFraming.Center(heroPosition, _session.Definition.Width, _session.Definition.Height, visibleHeight, _battleCamera.aspect, margin, MapViewSettings.TopEdgeMargin);
            _battleCamera.orthographicSize = visibleHeight * 0.5f;
            _battleCamera.transform.position = new Vector3(center.x, center.y, _battleCamera.transform.position.z);
        }

        /// <summary>根据已验证的领域分类选取表现资源；此处只创建视图，不创建或修改模拟单位。</summary>
        private UnitView CreateUnitView(UnitSnapshot unit)
        {
            var profile = _settings.AnimationFor(unit.Kind);
            var sprite = _players[profile.UnitKind].Require(UnitAction.Idle).Frame(0);
            var definition = _session.Definition.GetUnit(unit.Kind);
            var teamColor = unit.Kind == UnitKind.Hero
                ? new Color(0.38f, 0.78f, 0.98f)
                : new Color(0.83f, 0.38f, 0.29f);
            return new UnitView(
                transform, sprite, _white, _circle, definition.VisualHeight, definition.Radius,
                teamColor, _players[profile.UnitKind], _settings.Death);
        }
    }
}
