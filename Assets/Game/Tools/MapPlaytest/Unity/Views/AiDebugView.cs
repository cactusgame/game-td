using System;
using System.Collections.Generic;
using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.ValueObjects;
using Game.MapEngine.Domain;
using Game.Tools.MapPlaytest.Application.Sessions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Game.Tools.MapPlaytest.Unity.Views
{
    /// <summary>P 键控制的只读 AI 投影。仅开启时采样诊断快照，文字与图形全部穿透鼠标。</summary>
    public sealed class AiDebugView : IDisposable
    {
        private readonly MapPlaySession _session;
        private readonly Camera _camera;
        private readonly VisualElement _layer;
        private readonly Label _legend;
        private readonly Label _details;
        private readonly List<Label> _labels = new List<Label>();
        private readonly List<UnitAiDebugSnapshot> _data = new List<UnitAiDebugSnapshot>();
        private readonly List<UnitAiDebugSnapshot> _visible = new List<UnitAiDebugSnapshot>();
        private readonly List<Rect> _labelBounds = new List<Rect>();
        private ulong _focusedUnitId;
        private double _nextSampleAt;
        private int _generation = -1;
        public bool IsVisible { get; private set; }

        public AiDebugView(MapPlaySession session, Camera camera, UIDocument hud)
        {
            _session = session;
            _camera = camera;
            if (hud == null) return;
            _layer = new VisualElement { name = "aiDebugOverlay", pickingMode = PickingMode.Ignore };
            _layer.AddToClassList("ai-debug-overlay");
            _layer.generateVisualContent += Draw;
            hud.rootVisualElement.Insert(0, _layer);
            _legend = new Label("AI 调试 · P 关闭\n青：路径　橙：目标　绿：警戒圈") { pickingMode = PickingMode.Ignore };
            _legend.AddToClassList("ai-debug-legend");
            _layer.Add(_legend);
            _details = new Label { name = "aiDebugDetails", pickingMode = PickingMode.Ignore, enableRichText = false };
            _details.AddToClassList("ai-debug-details");
            _layer.Add(_details);
        }

        public void SetVisible(bool visible)
        {
            IsVisible = visible;
            _layer?.EnableInClassList("ai-debug-visible", visible);
            _nextSampleAt = 0;
            if (!visible) { _data.Clear(); _visible.Clear(); }
        }

        public void Synchronize()
        {
            if (!IsVisible || _layer?.panel == null || _camera == null) return;
            if (_generation != _session.Generation || Time.unscaledTimeAsDouble >= _nextSampleAt)
            {
                if (_generation != _session.Generation) _focusedUnitId = 1;
                _session.CopyAiDebug(_data);
                _generation = _session.Generation;
                _nextSampleAt = Time.unscaledTimeAsDouble + 0.1;
            }
            _visible.Clear();
            foreach (var item in _data)
            {
                Vector3 viewport = _camera.WorldToViewportPoint(new Vector3(item.Position.X, item.Position.Y, 0));
                if (viewport.z < 0 || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1) continue;
                _visible.Add(item);
            }
            float nearestPointer = 32 * 32;
            Vector2 pointer = Mouse.current?.position.ReadValue() ?? new Vector2(-1000, -1000);
            bool focusStillVisible = false;
            foreach (var item in _visible)
            {
                if (item.UnitId == _focusedUnitId) focusStillVisible = true;
                Vector3 screen = _camera.WorldToScreenPoint(new Vector3(item.Position.X, item.Position.Y, 0));
                float distance = Vector2.SqrMagnitude(new Vector2(screen.x, screen.y) - pointer);
                if (distance <= nearestPointer) { nearestPointer = distance; _focusedUnitId = item.UnitId; focusStillVisible = true; }
            }
            if (!focusStillVisible && _visible.Count > 0)
            {
                _focusedUnitId = _visible[0].UnitId;
                foreach (var item in _visible) if (item.Kind == UnitKind.Hero) { _focusedUnitId = item.UnitId; break; }
            }
            _labelBounds.Clear();
            while (_labels.Count < _visible.Count)
            {
                var label = new Label { pickingMode = PickingMode.Ignore, enableRichText = false };
                label.AddToClassList("ai-debug-label");
                _layer.Add(label);
                _labels.Add(label);
                _details.BringToFront();
                _legend.BringToFront();
            }
            for (int i = 0; i < _labels.Count; i++)
            {
                var label = _labels[i];
                label.EnableInClassList("ai-debug-hidden", i >= _visible.Count);
                if (i >= _visible.Count) continue;
                var item = _visible[i];
                label.EnableInClassList("ai-debug-enemy", UnitKinds.IsEnemy(item.Kind));
                label.text = $"#{item.UnitId} {BehaviorText(item.Behavior)}";
                Vector2 point = Project(item.Position);
                var bounds = new Rect(Mathf.Clamp(point.x + 7, 4, Mathf.Max(4, _layer.layout.width - 112)),
                    Mathf.Clamp(point.y - 20, 4, Mathf.Max(4, _layer.layout.height - 20)), 108, 17);
                for (int attempt = 0; attempt < _visible.Count; attempt++)
                {
                    bool overlaps = false;
                    foreach (var used in _labelBounds) if (bounds.Overlaps(used)) { overlaps = true; break; }
                    if (!overlaps) break;
                    bounds.y -= 18;
                    if (bounds.y < 4) { bounds.x = Mathf.Min(bounds.x + 112, _layer.layout.width - 112); bounds.y = point.y + 18; }
                }
                _labelBounds.Add(bounds);
                // 仅变换随相机投影的位置；所有外观与固定布局由 USS 控制。
                label.transform.position = new Vector3(bounds.x, bounds.y, 0);
            }
            _details.text = "视野内没有单位";
            foreach (var item in _visible)
                if (item.UnitId == _focusedUnitId)
                    _details.text = "当前单位 · 鼠标悬停单位切换\n" + Describe(item, _session.IsConstructionTime || _session.Outcome != "Running");
            string phase = _session.IsConstructionTime ? " · 建设暂停" : _session.Outcome != "Running" ? " · 对局已结束" : "";
            _legend.text = $"AI 调试 · P 关闭 · 视野内 {_visible.Count}/{_data.Count} 单位 · 模拟 {_session.SimulationTime:F2}s{phase}\n青：路径　橙：目标　绿：警戒圈 · 悬停看详情（10Hz）";
            _layer.MarkDirtyRepaint();
        }

        public static string Describe(UnitAiDebugSnapshot item, bool simulationPaused = false)
        {
            string command = UnitKinds.IsSoldier(item.Kind) ? item.Command switch
            {
                SoldierCommand.FollowHero => "跟随英雄",
                SoldierCommand.MoveToPosition => "前往固定位置",
                _ => "出生点警戒"
            } : item.Kind == UnitKind.Hero ? "玩家输入" : "自主战斗";
            string stance = UnitKinds.IsSoldier(item.Kind) ? (item.Stance == CombatStance.HoldGround ? "死守" : "警戒") : "—";
            string reason = simulationPaused ? "模拟暂停" : StopText(item.StopReason);
            return $"#{item.UnitId} {item.Kind} · {stance}\n命令 {command}｜行为 {BehaviorText(item.Behavior)}\n目标 {item.Target}｜停止 {reason}\n普攻CD {item.AttackCooldownSeconds:F2}s｜路径 {item.Path?.Count ?? 0} 点";
        }

        private void Draw(MeshGenerationContext context)
        {
            if (!IsVisible || _layer.panel == null || _camera == null) return;
            var painter = context.painter2D;
            painter.lineWidth = 1.5f;
            foreach (var item in _visible)
            {
                bool focused = item.UnitId == _focusedUnitId;
                painter.lineWidth = focused ? 2.2f : 1;
                if (item.AlertRadius > 0)
                {
                    painter.strokeColor = new Color(0.2f, 1, 0.55f, focused ? 0.8f : 0.12f);
                    painter.BeginPath();
                    for (int i = 0; i <= 64; i++)
                    {
                        float angle = i * Mathf.PI * 2 / 64;
                        Vector2 point = Project(new MapPoint(item.AlertCenter.X + Mathf.Cos(angle) * item.AlertRadius,
                            item.AlertCenter.Y + Mathf.Sin(angle) * item.AlertRadius));
                        if (i == 0) painter.MoveTo(point); else painter.LineTo(point);
                    }
                    painter.Stroke();
                }
                if (item.Path != null && item.Path.Count > 0)
                {
                    painter.strokeColor = new Color(0.1f, 0.85f, 1, focused ? 1 : 0.3f);
                    painter.BeginPath(); painter.MoveTo(Project(item.Position));
                    foreach (var point in item.Path) painter.LineTo(Project(point));
                    painter.Stroke();
                }
                if (item.HasTarget)
                {
                    painter.strokeColor = new Color(1, 0.55f, 0.2f, focused ? 1 : 0.25f);
                    Vector2 target = Project(item.TargetPosition);
                    painter.BeginPath(); painter.MoveTo(Project(item.Position)); painter.LineTo(target); painter.Stroke();
                    painter.BeginPath(); painter.MoveTo(target + new Vector2(-5, -5)); painter.LineTo(target + new Vector2(5, 5));
                    painter.MoveTo(target + new Vector2(-5, 5)); painter.LineTo(target + new Vector2(5, -5)); painter.Stroke();
                }
            }
        }

        private Vector2 Project(MapPoint point)
        {
            Vector3 screen = _camera.WorldToScreenPoint(new Vector3(point.X, point.Y, 0));
            Vector2 panel = RuntimePanelUtils.ScreenToPanel(_layer.panel, new Vector2(screen.x, Screen.height - screen.y));
            return _layer.WorldToLocal(panel);
        }

        private static string BehaviorText(UnitBehavior behavior) => behavior switch
        {
            UnitBehavior.Moving => "移动", UnitBehavior.Regrouping => "归队", UnitBehavior.Returning => "返回",
            UnitBehavior.Chasing => "追敌", UnitBehavior.Attacking => "攻击", UnitBehavior.Dead => "死亡", _ => "待机"
        };

        private static string StopText(MovementStopReason reason) => reason switch
        {
            MovementStopReason.Dead => "死亡", MovementStopReason.Stunned => "晕眩", MovementStopReason.Rooted => "定身",
            MovementStopReason.HoldGround => "死守", MovementStopReason.HeroUnavailable => "英雄死亡",
            MovementStopReason.InAttackRange => "攻击范围内", MovementStopReason.Arrived => "已到达",
            MovementStopReason.NoTarget => "无可用目标", MovementStopReason.NoPath => "无可达路径",
            MovementStopReason.WaitingForPath => "等待重新寻路", MovementStopReason.BlockedByTerrain => "地形阻挡",
            MovementStopReason.NoMoveInput => "没有移动输入", _ => "无"
        };

        public void Dispose()
        {
            if (_layer != null) { _layer.generateVisualContent -= Draw; _layer.RemoveFromHierarchy(); }
            _data.Clear(); _visible.Clear(); _labels.Clear();
        }
    }
}
