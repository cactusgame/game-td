using System;
using System.Collections.Generic;
using System.Linq;
using Game.Construction.Domain.Definitions;
using Game.MapEngine.Domain;
using Game.Maps.Domain;
using Game.Maps.Unity.Assets;
using Game.Tools.MapPlaytest.Application.Sessions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Game.Tools.MapPlaytest.Unity.Views
{
    /// <summary>建设操作状态机。只保存选择与预览；正式变更统一交给 MapPlaySession。</summary>
    public sealed class BattleConstructionView : MonoBehaviour
    {
        private enum Stage { Select, Building, WallStart, WallEnd, WallConfirm, Gate, Bastion, SaleStart, SaleEnd, SaleConfirm }
        private MapPlaySession _session;
        private MapContentCatalog _catalog;
        private MapVisualCatalog _visuals;
        private Camera _camera;
        private UIDocument _hud;
        private ConstructionPreviewView _preview;
        private Action _refresh;
        private Action<string> _feedback;
        private VisualElement _menu;
        private Label _hint;
        private Button _toggle;
        private Stage _stage;
        private string _definitionId;
        private string _selectedBuilding;
        private string _edgeId;
        private MapPoint _bastionPoint;
        private MapPoint _wallFrom, _wallTo;
        private int _x, _y;
        private int _gateStart;
        private float _saleStart, _saleEnd;
        private int _gateWidth = 4, _openingWidth = 2;
        private double _nextPreview;
        private bool _valid;
        private string _reason;
        // 首次点击固定建设锚点；菜单和预览都不能通过鼠标移动重新选址。
        private MapPoint _contextPoint;
        private Vector2 _contextScreen;
        private bool _tileMenuOpen;
        private Button _confirmBuild;
        private readonly List<(Button Button, string DefinitionId, string Label)> _tileChoices =
            new List<(Button, string, string)>();
        public bool Active { get; private set; }

        public void Initialize(MapPlaySession session, MapContentCatalog catalog, MapVisualCatalog visuals, Camera camera,
            UIDocument hud, Action refresh, Action<string> feedback)
        {
            _session = session; _catalog = catalog; _visuals = visuals; _camera = camera;
            _hud = hud; _refresh = refresh; _feedback = feedback;
            _preview = gameObject.AddComponent<ConstructionPreviewView>();
            if (hud == null) return;
            var controls = hud.rootVisualElement.Q<VisualElement>("mapControls");
            foreach (string name in new[] { "build", "melee-barracks", "ranged-barracks", "upgrade-barracks", "remove", "gate" })
            {
                var old = controls.Q<Button>(name);
                if (old != null) old.style.display = DisplayStyle.None;
            }
            _toggle = new Button(() => SetActive(!Active)) { name = "construction-mode", text = "建设模式 [B]" };
            controls.Q<VisualElement>(className: "buttons").Insert(0, _toggle);
            _hint = new Label { name = "construction-hint" };
            _hint.AddToClassList("construction-hint");
            controls.Add(_hint);
            _menu = new ScrollView { name = "constructionMenu", pickingMode = PickingMode.Position };
            _menu.AddToClassList("construction-menu");
            _menu.style.display = DisplayStyle.None;
            hud.rootVisualElement.Add(_menu);
        }

        public void SetVisuals(MapVisualCatalog visuals) => _visuals = visuals;
        public void ResetMode() => SetActive(false);
        public void SetActive(bool value)
        {
            Active = value && _session.Outcome == "Running";
            _session.CancelPlayerOperations();
            _stage = Stage.Select;
            _selectedBuilding = null;
            HideMenu();
            _preview.Clear();
            if (_toggle != null) _toggle.text = Active ? "退出建设 [B]" : "建设模式 [B]";
            SetHint(Active ? "建设中 · 战斗继续 | 点空地选择建筑，点建筑/墙管理；右键/Esc 返回" : "");
        }

        public void Tick()
        {
            var keys = Keyboard.current;
            var mouse = Mouse.current;
            if (!UnityEngine.Application.isFocused) return;
            if (keys != null && keys.bKey.wasPressedThisFrame) SetActive(!Active);
            if (!Active) return;
            if (_session.Outcome != "Running") { SetActive(false); return; }
            if (keys != null && keys.escapeKey.wasPressedThisFrame || mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                Back();
                return;
            }
            if (_tileMenuOpen && Time.unscaledTimeAsDouble >= _nextPreview)
                RefreshTileChoices();
            _preview.Clear();
            var map = _session.Snapshot;
            _preview.Grid(map, _camera);
            DrawSelection(map);
            if (mouse == null) return;
            var screen = mouse.position.ReadValue();
            bool overHud = PointerOverHud(screen);
            var world = _camera.ScreenToWorldPoint(screen);
            var point = new MapPoint(world.x, world.y);
            if (!overHud) UpdatePointer(map, point);
            DrawPlacement(map, point);
            if (!overHud && mouse.leftButton.wasPressedThisFrame) Click(point, screen);
        }

        private void UpdatePointer(MapLayout map, MapPoint point)
        {
            int x = Mathf.FloorToInt((point.X - map.originX) / map.cellWidth);
            int y = Mathf.FloorToInt((point.Y - map.originY) / map.cellHeight);
            if (_stage == Stage.Select && !_tileMenuOpen)
            {
                if (x != _x || y != _y) _nextPreview = 0;
                _x = x;
                _y = y;
            }
            if (_stage == Stage.WallEnd) { _wallTo = _session.SnapWallPoint(point); _nextPreview = 0; }
            if (_stage == Stage.SaleEnd)
                _saleEnd = Mathf.Clamp(WallTile(map, point) + 1, _saleStart + 1, _session.WallLengthTiles(_edgeId));
        }

        private void DrawPlacement(MapLayout map, MapPoint pointer)
        {
            if (_tileMenuOpen)
                _preview.Rectangle(new Rect(map.originX + _x * map.cellWidth, map.originY + _y * map.cellHeight,
                    map.cellWidth, map.cellHeight), Color.white);
            if (_stage == Stage.Building)
            {
                if (Time.unscaledTimeAsDouble >= _nextPreview)
                {
                    _valid = _session.PreviewBuild(_definitionId, _x, _y, out _reason);
                    _nextPreview = Time.unscaledTimeAsDouble + 0.15;
                }
                UpdateConfirmButton(_valid, _reason);
                var size = _catalog.Find(_definitionId);
                int width = size.width;
                int height = size.height;
                _preview.Footprint(map, _x, _y, width, height, _valid, _visuals.Facility(_definitionId));
                _preview.Range(new Vector2(map.originX + (_x + width * 0.5f) * map.cellWidth,
                    map.originY + (_y + height * 0.5f) * map.cellHeight), size.attackRange);
                if (_definitionId.StartsWith("barracks."))
                    _preview.Range(new Vector2(map.originX + (_x + width * 0.5f) * map.cellWidth,
                        map.originY + _y * map.cellHeight - 0.5f), 0.4f);
                SetHint($"{size.name} · {width}×{height} 格 · 固定地块 ({_x}, {_y}) · {(_valid ? "点击菜单确认建设" : _reason)}");
            }
            else if (_stage == Stage.WallEnd || _stage == Stage.WallConfirm)
            {
                _valid = _session.PreviewWall(_wallFrom, _wallTo, out _reason);
                _preview.Segment(ToVector(_wallFrom), ToVector(_wallTo), _valid ? Color.green : Color.red, _catalog.Wall.Width);
                SetHint($"城墙 · {_session.WallBuildCost(_wallFrom, _wallTo)} 金 · {(_valid ? "选择终点后确认" : _reason)}");
            }
            else if (_stage == Stage.Gate)
            {
                var edge = map.wallEdges.Find(item => item.id == _edgeId);
                _valid = edge != null &&
                    _session.PreviewGate(_edgeId, _gateStart, _gateWidth, _openingWidth, out _reason);
                UpdateConfirmButton(_valid, _reason);
                if (edge != null && WallGateRules.TryGetSpan(map, edge, new WallGate { startTile = _gateStart, widthTiles = _gateWidth, openingTiles = _openingWidth }, out var span))
                {
                    _preview.Segment(ToVector(span.At(span.Start)), ToVector(span.At(span.End)), _valid ? Color.green : Color.red, _catalog.Wall.Width + 0.15f);
                    _preview.Segment(ToVector(span.At(span.OpeningStart)), ToVector(span.At(span.OpeningEnd)), Color.cyan, 0.1f);
                }
                SetHint($"城门 · 门框{_gateWidth}格/净宽{_openingWidth}格 · {(_valid ? "原位确认建设" : _reason ?? "墙段已移除")}");
            }
            else if (_stage == Stage.Bastion)
            {
                _valid = _session.PreviewBastionAt(_bastionPoint, out var preview, out _reason);
                UpdateConfirmButton(_valid, _reason);
                if (preview != null)
                    _preview.Footprint(map, preview.x, preview.y, _catalog.Find("bastion").width, _catalog.Find("bastion").height, _valid,
                        _visuals.Facility("bastion") ?? _visuals.Facility("tower"));
                SetHint("建设敌台 · " + (_valid ? "原位确认建设" : _reason));
            }
            else if (_stage == Stage.SaleEnd || _stage == Stage.SaleConfirm) DrawWallSale(map);
        }

        private void Click(MapPoint point, Vector2 screen)
        {
            switch (_stage)
            {
                case Stage.Building:
                    SetHint("建设位置已固定，请使用菜单确认；右键取消后可重新选择地块");
                    break;
                case Stage.WallStart:
                    _wallFrom = _wallTo = _session.SnapWallPoint(point);
                    _stage = Stage.WallEnd;
                    HideMenu();
                    break;
                case Stage.WallEnd:
                    if (!_valid) { _feedback(_reason); break; }
                    _stage = Stage.WallConfirm;
                    Menu(screen, "确认建设城墙");
                    AddAction($"建设 · {_session.WallBuildCost(_wallFrom, _wallTo)} 金", () =>
                    {
                        bool success = _session.TryBuildWall(_wallFrom, _wallTo, out var failure);
                        Complete(success, failure);
                    });
                    AddAction("取消", Back);
                    break;
                case Stage.Gate:
                    SetHint("城门位置已固定，请使用菜单确认");
                    break;
                case Stage.Bastion:
                    SetHint("敌台位置已固定，请使用菜单确认");
                    break;
                case Stage.SaleStart:
                    _saleStart = Mathf.Clamp(WallTile(_session.Snapshot, point), 0, Math.Max(0, _session.WallLengthTiles(_edgeId) - 1));
                    _saleEnd = Math.Min(_saleStart + 1, _session.WallLengthTiles(_edgeId));
                    _stage = Stage.SaleEnd;
                    HideMenu();
                    break;
                case Stage.SaleEnd:
                    ConfirmWallSale(screen);
                    break;
                case Stage.Select:
                    OpenContext(point, screen);
                    break;
            }
        }

        private void OpenContext(MapPoint point, Vector2 screen)
        {
            _selectedBuilding = null;
            _contextPoint = point;
            _contextScreen = screen;
            var building = _session.FindBuilding(point.X, point.Y);
            if (building != null)
            {
                _selectedBuilding = building.id;
                var category = _session.BuildingRule(building.definitionId)?.Category;
                string classification = category == BuildingCategory.Decoration ? "地图装饰" : category == BuildingCategory.BuiltIn ? "内置建筑" : "玩家建筑";
                Menu(screen, $"{_catalog.Find(building.definitionId).name} · {classification}" +
                    (category == BuildingCategory.Decoration ? "" : $" · Lv{_session.BuildingLevel(building.id)}"));
                var health = _session.Structures.FirstOrDefault(item => item.Id == building.id);
                AddHealthActions(building.id, health.Health, health.MaxHealth, point, screen);
                foreach (var upgrade in _session.AvailableUpgrades(building.id))
                {
                    var choice = upgrade;
                    AddAction($"{choice.Name} · {choice.Cost} 金\n生命+{choice.MaxHealthBonus} 攻击+{choice.AttackBonus} 射程+{choice.RangeBonus} 编制+{choice.CapacityBonus}", () =>
                    {
                        Complete(_session.TryUpgradeBuilding(building.id, choice.Id, out var reason), reason);
                    });
                }
                if (_session.QuoteBuildingSale(building.id, out int refund, out var saleReason))
                    AddAction($"售卖 · 预计返还 {refund} 金", () =>
                    {
                        Menu(screen, $"确认售卖，预计返还 {refund} 金");
                        if (building.definitionId.StartsWith("barracks.")) _menu.Add(new Label("所属士兵一并撤离；无降级操作。"));
                        AddAction("确认售卖", () => Complete(_session.TrySellBuilding(building.id, out var reason), reason));
                        AddAction("取消", () => OpenContext(point, screen));
                    });
                else _menu.Add(new Label(saleReason));
                if (!string.IsNullOrEmpty(building.wallNodeId))
                    foreach (var wall in _session.Snapshot.wallEdges.Where(item => item.from == building.wallNodeId || item.to == building.wallNodeId))
                    {
                        string wallId = wall.id;
                        AddAction($"售卖相连墙段 · {_session.WallLengthTiles(wallId):F1} Tile", () =>
                        {
                            _selectedBuilding = null;
                            _edgeId = wallId;
                            _saleStart = 0;
                            _saleEnd = _session.WallLengthTiles(wallId);
                            ConfirmWallSale(screen);
                        });
                    }
                return;
            }
            var edge = _session.FindWall(point);
            if (edge != null)
            {
                _edgeId = edge.id;
                Menu(screen, "城墙管理");
                AddHealthActions(edge.id, _session.GetWallHealth(edge.id), _catalog.Wall.WallHealth, point, screen);
                AddAction("沿墙建设城门", () => BeginGate(edge.id));
                AddAction("售卖整段（包含所标城门）", () =>
                { _saleStart = 0; _saleEnd = _session.WallLengthTiles(edge.id); ConfirmWallSale(screen); });
                AddAction("售卖局部（沿墙选择起止点）", () =>
                { _stage = Stage.SaleStart; HideMenu(); SetHint("沿墙点击售卖起点，再点击终点；不能截断半座城门"); });
                foreach (var gate in edge.gates)
                {
                    var selected = gate;
                    _menu.Add(new Label("城门 · " + gate.id));
                    AddHealthActions(gate.id, _session.GetGateHealth(gate.id), _catalog.Wall.GateHealth, point, screen);
                    AddAction($"售卖城门 · 区间 {gate.startTile}–{gate.startTile + gate.widthTiles}", () =>
                    { _saleStart = selected.startTile; _saleEnd = selected.startTile + selected.widthTiles; ConfirmWallSale(screen); });
                }
                return;
            }
            var map = _session.Snapshot;
            if (!map.Geometry.TryWorldToCell(point.X, point.Y, out var cell))
            {
                HideMenu();
                _feedback("请选择地图内的地块");
                return;
            }
            _x = cell.X;
            _y = cell.Y;
            Menu(screen, "选择建设内容");
            foreach (var rule in _session.BuildableBuildings)
            {
                if (rule.PlacementKind != ConstructionPlacementKind.Building && rule.PlacementKind != ConstructionPlacementKind.Wall)
                    continue;
                var choice = rule;
                string name = _catalog.Find(choice.Id)?.name ?? (choice.PlacementKind == ConstructionPlacementKind.Wall ? "城墙（每格）" : "城门");
                var size = _catalog.Find(choice.Id);
                var button = AddAction($"{name} · {choice.BuildCost} 金", () => Begin(choice.Id));
                if (size != null && button != null)
                    _tileChoices.Add((button, choice.Id, $"{name} · {choice.BuildCost} 金"));
            }
            _tileMenuOpen = true;
            RefreshTileChoices();
        }

        private void AddHealthActions(string id, int health, int maximum, MapPoint point, Vector2 screen)
        {
            if (maximum <= 0)
                return;
            _menu.Add(new Label($"生命 {health}/{maximum}" + (health <= 0 ? " · 已损毁" : "")));
            foreach (bool repair in new[] { false, true })
            {
                bool restore = repair;
                var button = AddAction(restore ? "试玩：修复 25% 生命" : "试玩：造成 30% 伤害", () =>
                {
                    bool success = _session.TryChangeStructureHealth(id, restore, restore ? 0.25f : 0.3f, out var reason);
                    if (!success)
                        _feedback(reason);
                    OpenContext(point, screen);
                });
                button?.SetEnabled(health > 0 && (!restore || health < maximum));
            }
        }

        private void Begin(string definitionId)
        {
            _definitionId = definitionId;
            _selectedBuilding = null;
            _nextPreview = 0;
            var rule = _session.BuildableBuildings.First(item => item.Id == definitionId);
            if (rule.PlacementKind == ConstructionPlacementKind.Building &&
                !_session.PreviewBuild(definitionId, _x, _y, out var failure))
            {
                _feedback(failure);
                RefreshTileChoices();
                return;
            }
            _stage = rule.PlacementKind == ConstructionPlacementKind.Wall ? Stage.WallStart :
                rule.PlacementKind == ConstructionPlacementKind.Gate ? Stage.Select :
                rule.PlacementKind == ConstructionPlacementKind.Bastion ? Stage.Bastion : Stage.Building;
            HideMenu();
            if (_stage == Stage.Building)
            {
                Menu(_contextScreen, $"固定地块 ({_x}, {_y}) · 预览完整占地");
                _confirmBuild = AddAction("确认建设", () =>
                    Complete(_session.TryBuild(_definitionId, _x, _y, out var reason), reason));
                AddAction("取消", Back);
            }
            else if (_stage == Stage.Bastion)
            {
                _bastionPoint = _contextPoint;
                Menu(_contextScreen, "在选中的墙体位置建设敌台");
                _confirmBuild = AddAction("确认建设敌台", () =>
                    Complete(_session.TryBuildBastionAt(_bastionPoint, out var reason), reason));
                UpdateConfirmButton(_session.PreviewBastionAt(_bastionPoint, out _, out var previewReason), previewReason);
                AddAction("取消", Back);
            }
            if (rule.PlacementKind == ConstructionPlacementKind.Gate)
                SetHint("请点击已有墙段，选择沿墙建设城门");
            else if (_stage == Stage.WallStart) SetHint("点击城墙起点；吸附网格交点和已有墙节点");
        }

        private void BeginGate(string edgeId)
        {
            _edgeId = edgeId;
            _stage = Stage.Gate;
            _selectedBuilding = null;
            Menu(new Vector2(Screen.width - 310, Screen.height / 2f), "城门规格");
            foreach (var size in new[] { 3, 4, 6 })
            {
                int width = size;
                AddAction($"门框{width}格 / 门洞{width - 2}格", () =>
                {
                    _gateWidth = width;
                    _openingWidth = width - 2;
                    var map = _session.Snapshot;
                    var wall = map.wallEdges.Find(item => item.id == edgeId);
                    if (wall == null) { _feedback("墙段已移除"); Back(); return; }
                    _gateStart = WallGateRules.SnapStartTile(map, wall, _contextPoint, _gateWidth);
                    Menu(_contextScreen, "在选中的墙体位置建设城门");
                    _confirmBuild = AddAction("确认建设城门", () => Complete(
                        _session.TryPlaceGate(_edgeId, _gateStart, _gateWidth, _openingWidth, false, out _, out var reason), reason));
                    UpdateConfirmButton(_session.PreviewGate(_edgeId, _gateStart, _gateWidth, _openingWidth, out var previewReason), previewReason);
                    AddAction("取消", Back);
                });
            }
        }

        private void ConfirmWallSale(Vector2 screen)
        {
            _stage = Stage.SaleConfirm;
            bool valid = _session.QuoteWallSale(_edgeId, _saleStart, _saleEnd, out int refund, out var reason);
            Menu(screen, valid ? $"拆除红色区间 · 返还 {refund} 金" : reason);
            if (valid) AddAction("确认售卖，留下缺口", () => Complete(_session.TrySellWall(_edgeId, _saleStart, _saleEnd, out var failure), failure));
            AddAction("取消", Back);
        }

        private void DrawSelection(MapLayout map)
        {
            if (_selectedBuilding != null)
            {
                var building = map.facilities.Find(item => item.id == _selectedBuilding);
                if (building == null) { _selectedBuilding = null; HideMenu(); return; }
                var size = _catalog.Find(building.definitionId);
                _preview.Footprint(map, building.x, building.y, building.Width(size), building.Height(size), true);
                _preview.Range(new Vector2(map.originX + (building.x + building.Width(size) * 0.5f) * map.cellWidth,
                    map.originY + (building.y + building.Height(size) * 0.5f) * map.cellHeight), _session.BuildingAttackRange(building.id));
            }
        }
        private void DrawWallSale(MapLayout map)
        {
            var edge = map.wallEdges.Find(item => item.id == _edgeId);
            if (edge == null) { Back(); return; }
            if (WallGateRules.TryGetSpan(map, edge, new WallGate(), out var span))
                _preview.Segment(ToVector(span.At(_saleStart * span.TileLength)), ToVector(span.At(_saleEnd * span.TileLength)), Color.red, _catalog.Wall.Width + 0.2f);
            SetHint($"售卖区间 {_saleStart:F0}–{_saleEnd:F1} Tile · 红色部分将移除，剩余方向不变");
        }
        private int WallTile(MapLayout map, MapPoint point)
        {
            var edge = map.wallEdges.Find(item => item.id == _edgeId);
            if (edge == null || !WallGateRules.TryGetSpan(map, edge, new WallGate(), out var span)) return 0;
            return Mathf.FloorToInt(((point.X - span.Origin.X) * span.Direction.X + (point.Y - span.Origin.Y) * span.Direction.Y) / span.TileLength);
        }

        private void Back()
        {
            if (_stage != Stage.Select) { _stage = Stage.Select; _selectedBuilding = null; HideMenu(); SetHint("点击空地、建筑或城墙"); }
            else if (_menu != null && _menu.style.display != DisplayStyle.None) { HideMenu(); _selectedBuilding = null; }
            else SetActive(false);
        }
        private void Complete(bool success, string reason)
        {
            if (!success) { _feedback(reason ?? "操作失败，未提交"); return; }
            _refresh();
            SetActive(false);
            _feedback("建设操作已完成");
        }
        private void RefreshTileChoices()
        {
            foreach (var choice in _tileChoices)
            {
                var size = _catalog.Find(choice.DefinitionId);
                int width = size.width;
                int height = size.height;
                bool canBuild = _session.PreviewBuild(choice.DefinitionId, _x, _y, out var reason);
                choice.Button.text = $"{choice.Label} · {width}×{height}" + (canBuild ? "" : $"\n{reason}");
                choice.Button.tooltip = reason ?? $"固定建在地块 ({_x}, {_y})";
                choice.Button.SetEnabled(canBuild);
            }
            _nextPreview = Time.unscaledTimeAsDouble + 0.15;
        }

        private void UpdateConfirmButton(bool valid, string reason)
        {
            if (_confirmBuild == null) return;
            _confirmBuild.SetEnabled(valid);
            _confirmBuild.tooltip = reason ?? "确认后返回普通战斗模式";
        }
        private void Menu(Vector2 screen, string title)
        {
            if (_menu == null) return;
            _tileMenuOpen = false;
            _tileChoices.Clear();
            _confirmBuild = null;
            _menu.Clear();
            _menu.Add(new Label(title));
            _menu.style.display = DisplayStyle.Flex;
            var root = _hud.rootVisualElement;
            if (root.panel == null) return;
            var position = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
            float width = root.panel.visualTree.worldBound.width;
            float height = root.panel.visualTree.worldBound.height;
            _menu.style.left = Mathf.Clamp(position.x + 14, 6, Mathf.Max(6, width - 310));
            _menu.style.top = Mathf.Clamp(position.y + 12, 6, Mathf.Max(6, height - 360));
            _menu.style.maxHeight = Mathf.Max(100, Mathf.Min(350, height - 12));
        }
        private Button AddAction(string title, Action action)
        {
            if (_menu == null) return null;
            var button = new Button(action) { text = title };
            _menu.Add(button);
            return button;
        }
        private void HideMenu()
        {
            if (_menu != null) _menu.style.display = DisplayStyle.None;
            _tileMenuOpen = false;
            _tileChoices.Clear();
            _confirmBuild = null;
        }
        private void SetHint(string text) { if (_hint != null) _hint.text = text; }
        private bool PointerOverHud(Vector2 screen)
        {
            if (_hud == null || _hud.rootVisualElement.panel == null) return false;
            var panel = _hud.rootVisualElement.panel;
            var point = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
            for (var element = panel.Pick(point); element != null; element = element.parent)
                if (element.name == "mapControls" || element.name == "constructionMenu") return true;
            return false;
        }
        private static Vector2 ToVector(MapPoint point) => new Vector2(point.X, point.Y);
    }
}
