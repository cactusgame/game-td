using System;
using System.Collections.Generic;
using Game.Battle.Application.Sessions;
using Game.Battle.Unity.Assets;
using Game.MapEngine.Domain;
using Game.Presentation.Views;
using Game.Tools.MapPlaytest.Application.Sessions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Game.Tools.MapPlaytest.Unity.Views
{
    /// <summary>出生口波次预览和确认输入适配；关卡阶段与波次调度仍由应用会话裁决。</summary>
    public sealed class WavePreviewView : IDisposable
    {
        private readonly MapPlaySession _session;
        private readonly Camera _camera;
        private readonly Action _onConstructionCompleted;
        private readonly VisualElement _layer;
        private readonly Label _hint;
        private readonly ProgressBar _progress;
        private readonly PressGesture _confirm = new PressGesture(2);
        private readonly List<(VisualElement Card, MapPoint Position)> _cards = new List<(VisualElement, MapPoint)>();
        private readonly UnitVisualCatalog _visuals;
        private readonly BattlePresentationSettings _presentation;
        private int _waveIndex = -1;
        private int _generation = -1;
        private double _holdStarted = -1;
        private bool _consumeSpace;
        private bool _requireSpaceRelease;
        public bool SuppressSpaceSkills => _session.IsConstructionTime || _consumeSpace;

        public WavePreviewView(MapPlaySession session, Camera camera, UIDocument hud, Action onConstructionCompleted)
        {
            _session = session;
            _camera = camera;
            _onConstructionCompleted = onConstructionCompleted;
            _visuals = Resources.Load<UnitVisualCatalog>("UnitVisualCatalog");
            if (_visuals != null)
                _presentation = BattlePresentationSettings.Parse(_visuals.Presentation.text);
            if (hud == null)
                return;

            hud.rootVisualElement.AddToClassList("map-play-document");
            _layer = new VisualElement { name = "wavePreviews", pickingMode = PickingMode.Ignore };
            _layer.AddToClassList("wave-previews");
            // 预览在调试工具之下，建设提示独立于 P 键显隐。
            hud.rootVisualElement.Insert(0, _layer);
            _hint = new Label { pickingMode = PickingMode.Ignore };
            _hint.AddToClassList("wave-hint");
            hud.rootVisualElement.Add(_hint);
            _progress = new ProgressBar { lowValue = 0, highValue = 2, title = "确认建设" , pickingMode = PickingMode.Ignore };
            _progress.AddToClassList("wave-confirm-progress");
            hud.rootVisualElement.Add(_progress);
        }

        public void Reset()
        {
            _confirm.Reset();
            _holdStarted = -1;
            _consumeSpace = Keyboard.current?.spaceKey.isPressed ?? false;
            _requireSpaceRelease = _consumeSpace;
            _waveIndex = -1;
            _layer?.Clear();
            _cards.Clear();
        }

        public void ReadInput()
        {
            var keyboard = Keyboard.current;
            bool down = keyboard != null && keyboard.spaceKey.isPressed;
            if (!UnityEngine.Application.isFocused || keyboard == null)
            {
                _confirm.Reset();
                _holdStarted = -1;
                _requireSpaceRelease = true;
                return;
            }
            if (!down)
                _consumeSpace = false;
            if (_requireSpaceRelease)
            {
                if (down)
                    return;
                _requireSpaceRelease = false;
            }
            if (!_session.IsConstructionTime)
                return;

            if (down)
            {
                _consumeSpace = true;
                if (_holdStarted < 0)
                    _holdStarted = Time.unscaledTimeAsDouble;
            }
            int gesture = _confirm.Update(down, Time.unscaledTimeAsDouble);
            if (gesture == 2 || gesture == 4)
                ConfirmConstruction();
            if (!down)
                _holdStarted = -1;
        }

        private void ConfirmConstruction()
        {
            if (_session.TryCompleteConstruction())
                _onConstructionCompleted();
        }

        public void Synchronize()
        {
            if (_layer == null || _layer.panel == null)
                return;

            bool construction = _session.IsConstructionTime;
            _hint.EnableInClassList("wave-hidden", _session.Outcome != "Running");
            _hint.text = construction
                ? "建设时间 · 自由建造 [B] | 长按空格 2 秒，或双击出生口波次图标，完成建设"
                : _session.CanStartNextWave ? "双击出生口波次图标，呼叫下一波" : "按波次配置等待敌军到来";
            _progress.EnableInClassList("wave-hidden", !construction || _holdStarted < 0);
            _progress.value = _holdStarted < 0 ? 0 : (float)(Time.unscaledTimeAsDouble - _holdStarted);

            int waveIndex = _session.Outcome == "Running" ? _session.SpawnedWaves : -1;
            if (_waveIndex != waveIndex || _generation != _session.Generation)
            {
                _waveIndex = waveIndex;
                _generation = _session.Generation;
                Rebuild();
            }
            foreach (var item in _cards)
            {
                item.Card.EnableInClassList("wave-ready", construction || _session.CanStartNextWave);
                Vector3 screen = _camera.WorldToScreenPoint(new Vector3(item.Position.X, item.Position.Y, 0));
                Vector2 point = RuntimePanelUtils.ScreenToPanel(_layer.panel, new Vector2(screen.x, Screen.height - screen.y));
                point = _layer.WorldToLocal(point);
                // 出生口可能处于镜头外：将图标限制在视口边缘，仍可查看和双击。
                float width = float.IsNaN(item.Card.layout.width) ? 150 : item.Card.layout.width;
                float height = float.IsNaN(item.Card.layout.height) ? 90 : item.Card.layout.height;
                item.Card.style.left = Mathf.Clamp(point.x - width / 2, 8, Mathf.Max(8, _layer.layout.width - width - 8));
                item.Card.style.top = Mathf.Clamp(point.y - height - 20, 8, Mathf.Max(8, _layer.layout.height - height - 115));
            }
        }

        private void Rebuild()
        {
            _layer.Clear();
            _cards.Clear();
            var byEntry = new Dictionary<string, VisualElement>();
            foreach (var preview in _session.GetWavePreviews())
            {
                if (!byEntry.TryGetValue(preview.EntryId, out var card))
                {
                    card = new VisualElement { tooltip = "双击确认建设 / 呼叫下一波", focusable = false };
                    card.AddToClassList("wave-preview");
                    card.Add(new Label($"第 {preview.WaveIndex + 1} 波 · ▼") { pickingMode = PickingMode.Ignore });
                    int expectedWave = preview.WaveIndex;
                    card.RegisterCallback<ClickEvent>(evt =>
                    {
                        if (evt.button != 0 || evt.clickCount != 2 || !UnityEngine.Application.isFocused ||
                            _session.SpawnedWaves != expectedWave)
                            return;
                        if (_session.IsConstructionTime)
                            ConfirmConstruction();
                        else
                            _session.TryStartNextWave();
                        evt.StopPropagation();
                    });
                    byEntry.Add(preview.EntryId, card);
                    _layer.Add(card);
                    _cards.Add((card, preview.Position));
                }
                var unit = preview.EnemyId == _session.Definition.Goblin.Id ? _session.Definition.Goblin : _session.Definition.Runner;
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList("wave-enemy");
                var icon = new Image { sprite = Icon(unit.Kind), scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                icon.AddToClassList("wave-enemy-icon");
                row.Add(icon);
                row.Add(new Label($"× {preview.Count}") { pickingMode = PickingMode.Ignore });
                card.Add(row);
            }
        }

        private Sprite Icon(Game.Battle.Domain.ValueObjects.UnitKind kind)
        {
            if (_visuals == null || _presentation == null)
                return null;
            string resource = _presentation.AnimationFor(kind).ResourceId;
            var animation = Array.Find(_visuals.Animations, item => item != null && item.name == resource);
            if (animation == null || animation.Frames == null || animation.Idle == null || animation.Idle.IsEmpty)
                return null;
            int index = animation.Idle.FirstFrame - 1;
            return index >= 0 && index < animation.Frames.Length ? animation.Frames[index] : null;
        }

        public void Dispose()
        {
            _layer?.RemoveFromHierarchy();
            _hint?.RemoveFromHierarchy();
            _progress?.RemoveFromHierarchy();
        }
    }
}
