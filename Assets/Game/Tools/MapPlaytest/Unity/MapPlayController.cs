using System;
using Game.Battle.Simulation.Bridges;
using Game.Infrastructure.Configuration;
using Game.Infrastructure.Maps.Serialization;
using Game.Maps.Domain;
using Game.Maps.Unity.Assets;
using Game.Maps.Unity.Views;
using Game.Tools.MapPlaytest.Application.Sessions;
using Game.Tools.MapPlaytest.Unity.Input;
using Game.Tools.MapPlaytest.Unity.Views;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Game.Tools.MapPlaytest.Unity
{
    /// <summary>
    /// 试玩场景的 Unity 组装入口：加载资源、转换输入并刷新画面。
    /// 本局布置、建设提交、重启和模拟生命周期由 MapPlaySession 组织。
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Game.Maps.Unity", "Game.MapContent.Unity", "MapPlayController")]
    public sealed class MapPlayController : MonoBehaviour
    {
        // 保留序列化字段与脚本 GUID，已有试玩场景无需重新配置引用。
        public TextAsset mapJson;
        public TextAsset battleJson;
        public MapVisualCatalog visuals;
        public Camera mapCamera;
        public UIDocument hud;
        public Sprite heroSprite;
        public Sprite enemySprite;
        public static string PendingPlaytestJson;

        private MapPlaySession _session;
        private MapWorldView _view;
        private MapPlayUnitView _unitView;
        private MapContentCatalog _catalog;
        private string _tool = "Inspect";
        private float _feedbackUntil;
        private Label _status;

        public double SimulationTime => _session?.SimulationTime ?? 0;
        public int MapVersion => _session?.MapVersion ?? 0;
        public MapLayout Snapshot => _session?.Snapshot;

        private void Start()
        {
            try
            {
                string json = string.IsNullOrEmpty(PendingPlaytestJson) ? mapJson.text : PendingPlaytestJson;
                PendingPlaytestJson = null;
                _catalog = FacilityCatalogJson.Read(visuals.facilityRules.text);
                var definition = MapJson.LoadDefinition(json, _catalog);
                var rules = JsonBattleDefinitionSource.Parse(battleJson.text);
                _session = new MapPlaySession(definition, _catalog, rules, new KeyboardHeroInput(), () => new EcsBattleSimulation());
                _view = gameObject.AddComponent<MapWorldView>();
                _unitView = gameObject.AddComponent<MapPlayUnitView>();
                _unitView.Initialize(heroSprite, enemySprite);
                if (hud != null)
                {
                    var root = hud.rootVisualElement;
                    _status = root.Q<Label>("status");
                    root.Q<Button>("inspect").clicked += () => _tool = "Inspect";
                    root.Q<Button>("build").clicked += () => _tool = "Build";
                    root.Q<Button>("remove").clicked += () => _tool = "Remove";
                    root.Q<Button>("gate").clicked += () => _tool = "Gate";
                    root.Q<Button>("restart").clicked += Restart;
                    root.Q<Button>("exit").clicked += Exit;
                }
                Restart();
            }
            catch (Exception error)
            {
                _session?.Dispose();
                Debug.LogException(error);
                if (_status != null)
                    _status.text = error.Message;
                enabled = false;
            }
        }

        public void Restart()
        {
            _session.Restart();
            _unitView.Clear();
            RefreshMapView();
            var layout = _session.Snapshot;
            float height = layout.rows * layout.cellHeight;
            mapCamera.transform.position = new Vector3(layout.originX + layout.columns * layout.cellWidth * 0.5f,
                layout.originY + height * 0.5f + height * 0.12f, -10);
            mapCamera.orthographicSize = Mathf.Max(height * 0.72f,
                layout.columns * layout.cellWidth / Mathf.Max(0.1f, mapCamera.aspect) * 0.5f + 1);
        }

        private void Update()
        {
            if (_session == null)
                return;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerOverHud(mouse.position.ReadValue()))
            {
                Vector3 point = mapCamera.ScreenToWorldPoint(mouse.position.ReadValue());
                if (_tool == "Build" && _session.Navigation.TryWorldToCell(point.x, point.y, out var cell))
                    TryBuild(cell.X, cell.Y);
                else if (_tool == "Remove")
                    TryRemove(point.x, point.y);
                else if (_tool == "Gate")
                    TryToggleGate(point.x, point.y);
            }
            // 命令已由应用会话完整提交，本帧后续模拟读取一致的新空间。
            _session.Advance(Time.deltaTime);
            _unitView.Synchronize(_session.Units, _session.Definition);
            if (_status != null && Time.unscaledTime >= _feedbackUntil && Time.frameCount % 15 == 0)
                _status.text = $"{_tool} | {_session.SimulationTime:F1}s | map revision {_session.MapVersion} | Hero {_session.HeroHealth} HP";
        }

        public bool TryBuild(int x, int y)
        {
            if (!_session.TryBuild("tower", x, y, out var reason))
            {
                if (_status != null)
                {
                    _status.text = reason;
                    _feedbackUntil = Time.unscaledTime + 2;
                }
                return false;
            }
            RefreshMapView();
            return true;
        }

        public bool TryRemove(float x, float y)
        {
            if (!_session.TryRemove(x, y, out _))
                return false;
            RefreshMapView();
            return true;
        }

        public bool TryToggleGate(float x, float y)
        {
            var map = _session.Snapshot;
            string id = MapSceneSprites.PickGate(map, visuals, new Vector2(x, y));
            var edge = map.wallEdges.Find(item => item.id == id);
            string reason = "该位置没有城门";
            if (edge == null || !_session.TrySetGateOpen(edge.id, !edge.gate.isOpen, out reason))
            {
                if (_status != null)
                {
                    _status.text = reason;
                    _feedbackUntil = Time.unscaledTime + 2;
                }
                return false;
            }
            RefreshMapView();
            return true;
        }

        private void RefreshMapView()
        {
            _view.Rebuild(_session.Snapshot, visuals, _catalog);
        }

        private bool PointerOverHud(Vector2 screen)
        {
            if (hud == null || hud.rootVisualElement.panel == null)
                return false;
            var panel = hud.rootVisualElement.panel;
            var point = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
            var element = panel.Pick(point);
            while (element != null)
            {
                if (element.name == "mapControls")
                    return true;
                element = element.parent;
            }
            return false;
        }

        private void Exit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy() => _session?.Dispose();
    }
}
