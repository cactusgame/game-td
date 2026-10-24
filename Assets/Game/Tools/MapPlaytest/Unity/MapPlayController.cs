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
        public TextAsset encounterJson;
        public MapVisualCatalog visuals;
        public MapVisualCatalog[] artCatalogs;
        public Camera mapCamera;
        public UIDocument hud;
        public Sprite heroSprite;
        public Sprite enemySprite;
        public static string PendingPlaytestJson;

        private MapPlaySession _session;
        private MapWorldView _view;
        private MapPlayUnitView _unitView;
        private MapPlayCamera _cameraView;
        private MapContentCatalog _catalog;
        private string _tool = "Inspect";
        private string _buildDefinitionId = "tower";
        private float _feedbackUntil;
        private Label _status;
        private Label _barracksStatus;

        public double SimulationTime => _session?.SimulationTime ?? 0;
        public int MapVersion => _session?.MapVersion ?? 0;
        public MapLayout Snapshot => _session?.Snapshot;

        private void Start()
        {
            // 校验失败也能在 HUD 展示原因，而不是停留在 Loading。
            _status = hud != null ? hud.rootVisualElement.Q<Label>("status") : null;
            try
            {
                string json = string.IsNullOrEmpty(PendingPlaytestJson) ? mapJson.text : PendingPlaytestJson;
                PendingPlaytestJson = null;
                _catalog = FacilityCatalogJson.Read(visuals.facilityRules.text);
                var definition = MapJson.LoadDefinition(json, _catalog);
                var rules = JsonBattleDefinitionSource.Parse(battleJson.text);
                var combat = JsonCombatContentSource.Parse(Resources.Load<TextAsset>("GameConfig/combat-content").text);
                var construction = JsonConstructionContentSource.Parse(Resources.Load<TextAsset>("GameConfig/construction-content").text);
                var encounterAsset = encounterJson != null ? encounterJson : Resources.Load<TextAsset>("GameConfig/DefenseEncounter");
                if (encounterAsset == null)
                    throw new InvalidOperationException("缺少防守关卡配置 DefenseEncounter.json");
                var encounter = JsonDefenseEncounterSource.Parse(encounterAsset.text);
                int initialGold = JsonEconomySource.InitialGold(Resources.Load<TextAsset>("GameConfig/economy").text);
                _session = new MapPlaySession(definition, _catalog, rules, new KeyboardHeroInput(), () => new EcsBattleSimulation(), encounter, construction, initialGold);
                _view = gameObject.AddComponent<MapWorldView>();
                _unitView = gameObject.AddComponent<MapPlayUnitView>();
                _unitView.Initialize();
                if (hud != null)
                {
                    var root = hud.rootVisualElement;
                    if (artCatalogs != null && artCatalogs.Length > 0)
                    {
                        var names = new System.Collections.Generic.List<string>();
                        foreach (var catalog in artCatalogs)
                            names.Add(catalog.name);
                        var picker = new DropdownField("美术套装", names, Math.Max(0, Array.IndexOf(artCatalogs, visuals)));
                        picker.RegisterValueChangedCallback(evt => SwitchArt(artCatalogs[picker.index]));
                        root.Add(picker);
                    }
                    _status = root.Q<Label>("status");
                    _barracksStatus = root.Q<Label>("barracks-status");
                    root.Q<Button>("melee-barracks").text = $"近战兵营（{construction.Buildings["barracks.melee"].BuildCost}金）";
                    root.Q<Button>("ranged-barracks").text = $"远程兵营（{construction.Buildings["barracks.ranged"].BuildCost}金）";
                    root.Q<Button>("inspect").clicked += () => _tool = "Inspect";
                    root.Q<Button>("build").clicked += () => { _tool = "Build"; _buildDefinitionId = "tower"; };
                    root.Q<Button>("melee-barracks").clicked += () => { _tool = "Build"; _buildDefinitionId = "barracks.melee"; };
                    root.Q<Button>("ranged-barracks").clicked += () => { _tool = "Build"; _buildDefinitionId = "barracks.ranged"; };
                    root.Q<Button>("upgrade-barracks").clicked += () => _tool = "Upgrade";
                    root.Q<Button>("remove").clicked += () => _tool = "Remove";
                    root.Q<Button>("gate").clicked += () => _tool = "Gate";
                    root.Q<Button>("restart").clicked += Restart;
                    root.Q<Button>("exit").clicked += Exit;
                }
                _cameraView = new MapPlayCamera(mapCamera, Resources.Load<TextAsset>("GameConfig/MapPlayCamera"));
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

        /// <summary>只替换表现；本局规则、墙门状态、单位和模拟时钟继续由现有会话拥有。</summary>
        public void SwitchArt(MapVisualCatalog catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            if (catalog.facilityRules != visuals.facilityRules)
                throw new InvalidOperationException("美术套装必须引用同一份地图规则。");
            visuals = catalog;
            if (_session != null)
                _view.Rebuild(_session.Snapshot, visuals, _catalog);
            _gateViewVersion = _session.GateViewVersion;
        }

        public void Restart()
        {
            _session.Restart();
            _unitView.Clear();
            RefreshMapView();
            var layout = _session.Snapshot;
            _cameraView.SetMap(new Rect(layout.originX, layout.originY,
                layout.columns * layout.cellWidth, layout.rows * layout.cellHeight));
            FollowHero();
        }

        private void LateUpdate()
        {
            if (_session != null && _cameraView != null)
                FollowHero();
        }

        private void FollowHero()
        {
            foreach (var unit in _session.Units)
            {
                if (unit.Kind != Game.Battle.Domain.ValueObjects.UnitKind.Hero)
                    continue;
                float hudFraction = 0;
                if (hud != null && hud.rootVisualElement.panel != null)
                {
                    var controls = hud.rootVisualElement.Q<VisualElement>("mapControls");
                    float panelHeight = hud.rootVisualElement.panel.visualTree.worldBound.height;
                    if (controls != null && panelHeight > 0 && !float.IsNaN(controls.worldBound.yMax))
                        hudFraction = controls.worldBound.yMax / panelHeight;
                }
                _cameraView.Follow(new Vector2(unit.X, unit.Y), hudFraction);
                break;
            }
        }

        private int _gateViewVersion = -1;

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
                else if (_tool == "Upgrade")
                    TryUpgradeBarracks(point.x, point.y);
                else if (_tool == "Inspect")
                    InspectBuilding(point.x, point.y);
            }
            // 命令已由应用会话完整提交，本帧后续模拟读取一致的新空间。
            _session.Advance(Time.deltaTime);
            if (_gateViewVersion != _session.GateViewVersion)
            {
                _gateViewVersion = _session.GateViewVersion;
                _view.SynchronizeGates(_session.Snapshot);
            }
            _unitView.Synchronize(_session.Units, _session.Definition, _session.SimulationTime, Time.deltaTime, _session.Outcome != "Running");
            if (_status != null && Time.unscaledTime >= _feedbackUntil && Time.frameCount % 15 == 0)
                _status.text = $"{OutcomeText()} | Gold {_session.Gold} | Waves {_session.SpawnedWaves}/2 | Enemies {AliveEnemies()} | Castle {CastleHealth()} HP | {_tool} {_buildDefinitionId} | {_session.SimulationTime:F1}s | Hero {_session.HeroHealth} HP";
            if (_barracksStatus != null && Time.frameCount % 6 == 0)
            {
                var lines = new System.Collections.Generic.List<string>();
                foreach (var barracks in _session.GetBarracksStatuses())
                {
                    string state = barracks.IsDestroyed ? "已毁，停止补兵" :
                        barracks.Training > 0 ? $"训练{barracks.Training}人（{barracks.NextReadySeconds:F1}秒）" :
                        barracks.WaitingAtExit > 0 ? $"待出营{barracks.WaitingAtExit}人，请保持营门通畅" : "编制已满";
                    lines.Add($"{_catalog.Find(barracks.BuildingId).name} Lv{barracks.Level}：士兵 {barracks.Alive}/{barracks.Capacity}，{state}");
                }
                _barracksStatus.text = lines.Count > 0 ? string.Join("   |   ", lines) : "地图尚无兵营，请选择近战兵营或远程兵营建造。";
            }
        }

        private void InspectBuilding(float x, float y)
        {
            var map = _session.Snapshot;
            if (_status == null || !map.Geometry.TryWorldToCell(x, y, out var cell)) return;
            foreach (var building in map.facilities)
            {
                var definition = _catalog.Find(building.definitionId);
                if (cell.X < building.x || cell.X >= building.x + definition.width || cell.Y < building.y || cell.Y >= building.y + definition.height) continue;
                bool isBarracks = _session.BarracksCapacity(building.id) > 0;
                _status.text = isBarracks ? definition.name + "：编制和训练进度见下方兵营状态。" : definition.name + "：该建筑不训练士兵。";
                _feedbackUntil = Time.unscaledTime + 3;
                break;
            }
        }

        private string OutcomeText() => _session.Outcome == "Victory" ? "Victory" : _session.Outcome == "Defeat" ? "Defeat" : "Defending";
        private int AliveEnemies()
        {
            int count = 0;
            foreach (var unit in _session.Units)
                if (Game.Battle.Domain.ValueObjects.UnitKinds.IsEnemy(unit.Kind) && unit.Health > 0)
                    count++;
            return count;
        }
        private int CastleHealth()
        {
            int health = 0;
            foreach (var structure in _session.Structures)
                if (structure.IsMainCastle)
                    health += structure.Health;
            return health;
        }

        public bool TryBuild(int x, int y) => TryBuild(_buildDefinitionId, x, y);

        public bool TryBuild(string definitionId, int x, int y)
        {
            if (!_session.TryBuild(definitionId, x, y, out var reason))
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

        public bool TryUpgradeBarracks(float x, float y)
        {
            var map = _session.Snapshot;
            if (!map.Geometry.TryWorldToCell(x, y, out var cell)) return false;
            foreach (var building in map.facilities)
            {
                var size = _catalog.Find(building.definitionId);
                if (cell.X < building.x || cell.X >= building.x + size.width || cell.Y < building.y || cell.Y >= building.y + size.height) continue;
                bool success = _session.TryUpgradeBarracks(building.id, out string reason);
                _status.text = success ? $"兵营升级成功：{_session.BarracksLevel(building.id)}级，编制{_session.BarracksCapacity(building.id)}人。新增士兵正在训练。" : reason;
                _feedbackUntil = Time.unscaledTime + 3;
                return success;
            }
            return false;
        }

        public bool TryToggleGate(float x, float y)
        {
            var map = _session.Snapshot;
            string id = MapSceneSprites.PickGate(map, visuals, new Vector2(x, y), _catalog);
            var gate = WallGateRules.Find(map, id, out _);
            string reason = "该位置没有城门";
            if (gate == null || !_session.TrySetGateOpen(gate.id, !gate.isOpen, out reason))
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

