using Game.Maps.Domain.Mappings;
using Game.Fortifications.Domain.Aggregates.WallNetwork;
using Game.Fortifications.Domain.ValueObjects;
using Game.MapEngine.Domain.Geometry;
using Game.Infrastructure.Maps.Serialization;
using Game.Maps.Unity.Assets;
using System;
using System.IO;
using System.Text;
using Game.MapEngine.Domain;
using Game.Maps.Domain;
using Game.Infrastructure.Configuration;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Tools.MapAuthoring.Editor
{
    /// <summary>地图编辑入口。UI 意图 → 工作副本 → 一次历史提交 → 校验 / 保存 / 导出。</summary>
    public sealed class MapEditorWindow : EditorWindow
    {
        public const string UiPath = "Assets/Game/Tools/MapAuthoring/Editor/MapEditorWindow.uxml";
        public const string VisualPath = "Assets/Content/Maps/Shared/MapVisualCatalog.asset";
        [SerializeField] private string _path;
        [SerializeField] private string _historyJson;
        private MapEditSession _session;
        private MapLayout _gesture;
        private MapLayout _beforeGesture;
        private bool _gestureChanged;
        private MapVisualCatalog _visuals;
        private MapContentCatalog _catalog;
        private MapCanvas _canvas;
        private MapPalette _palette;
        [SerializeField] private MapEditorTool _activeTool;
        [SerializeField] private TerrainKind _activeTerrain;
        [SerializeField] private string _activeFacility = "tower";
        [SerializeField] private bool _showGrid;
        [SerializeField] private bool _showCoordinates;
        [SerializeField] private bool _showRules;
        private string _selectedId;
        private string _wallTail;
        private Vector2 _pressPoint;
        private GridCell? _lastPaintCell;

        [MenuItem("Game/地图编辑器")]
        public static void Open()
        {
            var window = GetWindow<MapEditorWindow>();
            window.titleContent = new GUIContent("地图编辑器");
            window.minSize = new Vector2(960, 600);
            window.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPath);
            template.CloneTree(rootVisualElement);
            _visuals = MapArtSelection.Current;
            if (_visuals == null)
            {
                rootVisualElement.Add(new Label("请运行 Game/地图编辑器/安装示例内容"));
                return;
            }
            var artCatalogs = MapArtSelection.All();
            var artPicker = new PopupField<string>("美术套装", artCatalogs.ConvertAll(item => item.name),
                Mathf.Max(0, artCatalogs.IndexOf(_visuals)));
            artPicker.RegisterValueChangedCallback(evt =>
            {
                MapArtSelection.Select(artCatalogs[artPicker.index]);
                CreateGUI();
            });
            rootVisualElement.Q("paletteHost").parent.Insert(0, artPicker);
            var referenceButton = new Button(() => OpenMapAsset(PixelArtSetup.MapPath)) { text = "参考图城镇地图" };
            rootVisualElement.Q("paletteHost").parent.Insert(1, referenceButton);
            _catalog = FacilityCatalogJson.Read(_visuals.facilityRules.text);
            if (_session == null)
            {
                if (!string.IsNullOrEmpty(_historyJson))
                    _session = MapEditSession.Restore(JsonUtility.FromJson<MapEditSession.History>(_historyJson));
                else
                {
                    var sample = AssetDatabase.LoadAssetAtPath<TextAsset>(MapEditorSetup.SamplePath);
                    _session = sample != null ? new MapEditSession(MapJson.ReadDocument(sample.text), true) : new MapEditSession(NewLayout());
                    _path = sample != null ? MapEditorSetup.SamplePath : null;
                }
            }
            _palette = new MapPalette(_visuals, _catalog, _activeTool, _activeTerrain, _activeFacility);
            Q<VisualElement>("paletteHost").Add(_palette);
            _palette.SelectionChanged += () =>
            {
                _activeTool = _palette.Tool;
                _activeTerrain = _palette.Terrain;
                _activeFacility = _palette.FacilityId;
                _wallTail = null;
                if (_canvas != null)
                    _canvas.PanTool = _palette.Tool == MapEditorTool.Pan;
            };
            _canvas = new MapCanvas { Visuals = _visuals, Catalog = _catalog, ShowGrid = _showGrid, ShowCoordinates = _showCoordinates, ShowRules = _showRules, PanTool = _palette.Tool == MapEditorTool.Pan };
            Q<VisualElement>("canvasHost").Add(_canvas);
            _canvas.Pressed = BeginGesture;
            _canvas.Dragged = ContinueGesture;
            _canvas.Released = EndGesture;
            _canvas.Hovered = ShowHover;
            Bind("newMap", NewDocument);
            Bind("open", OpenDocument);
            Bind("save", () => Save(false));
            Bind("saveAs", () => Save(true));
            Bind("undo", () => { _session.Undo(); Refresh(); });
            Bind("redo", () => { _session.Redo(); Refresh(); });
            Bind("fit", () => _canvas.Fit());
            Bind("applyGeometry", ApplyGeometry);
            Bind("validate", Validate);
            Bind("export", Export);
            Bind("play", Play);
            Bind("delete", DeleteSelection);
            Bind("rename", RenameMarker);
            Bind("applyGate", ApplyGate);
            Bind("removeGate", RemoveGate);
            Bind("artShowcase", () => OpenMapAsset(MapArtShowcase.Path));
            Bind("gateExample", () => OpenMapAsset("Assets/Content/Maps/Example/gates.map.json"));
            Q<Button>("toggleGrid").clicked += () =>
            {
                _showGrid = !_showGrid;
                _canvas.ShowGrid = _showGrid;
                RefreshViewButtons();
                _canvas.MarkDirtyRepaint();
            };
            Q<Button>("toggleCoordinates").clicked += () =>
            {
                _showCoordinates = !_showCoordinates;
                _canvas.ShowCoordinates = _showCoordinates;
                RefreshViewButtons();
                _canvas.MarkDirtyRepaint();
            };
            Q<Button>("toggleRules").clicked += () =>
            {
                _showRules = !_showRules;
                _canvas.ShowRules = _showRules;
                RefreshViewButtons();
                _canvas.MarkDirtyRepaint();
            };
            RefreshViewButtons();
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKey);
            Refresh();
        }

        private T Q<T>(string name) where T : VisualElement => rootVisualElement.Q<T>(name);
        private void RefreshViewButtons()
        {
            SetViewButton(Q<Button>("toggleGrid"), " Tile 网格", _showGrid);
            SetViewButton(Q<Button>("toggleCoordinates"), "坐标", _showCoordinates);
            SetViewButton(Q<Button>("toggleRules"), "实际通行", _showRules);
        }

        private static void SetViewButton(Button button, string label, bool visible)
        {
            button.text = (visible ? "隐藏" : "显示") + label;
            button.EnableInClassList("view-active", visible);
        }

        private void Bind(string name, Action action) => Q<Button>(name).clicked += () => Guard(action);
        private void Guard(Action action)
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                SetStatus(error.Message);
                Debug.LogException(error);
            }
        }
        private void SetStatus(string message) => Q<Label>("status").text = message;

        private static MapLayout NewLayout()
        {
            var layout = new MapLayout();
            layout.markers.Add(new MapMarker { id = MapLayout.NewId(), name = "英雄出生", kind = MarkerKind.HeroSpawn, x = 0.5f, y = 0.5f });
            layout.markers.Add(new MapMarker { id = MapLayout.NewId(), name = "西侧入口", kind = MarkerKind.EnemyEntry, x = -10.5f, y = 0.5f });
            return layout;
        }

        private void Refresh(bool updateFields = true)
        {
            var document = _session.Document;
            _canvas.Layout = document;
            _canvas.SelectedId = _selectedId;
            _canvas.MarkDirtyRepaint();
            _historyJson = JsonUtility.ToJson(_session.CaptureHistory());
            titleContent = new GUIContent("地图编辑器" + (_session.IsDirty ? " *" : ""));
            hasUnsavedChanges = _session.IsDirty;
            saveChangesMessage = "地图存在未保存的修改。";
            Q<Label>("documentPath").text = string.IsNullOrEmpty(_path) ? "未保存的新地图" : _path;
            Q<Button>("undo").SetEnabled(_session.CanUndo);
            Q<Button>("redo").SetEnabled(_session.CanRedo);
            if (updateFields)
            {
                Q<TextField>("mapId").SetValueWithoutNotify(document.id);
                Q<IntegerField>("columns").SetValueWithoutNotify(document.columns);
                Q<IntegerField>("rows").SetValueWithoutNotify(document.rows);
                Q<FloatField>("cellWidth").SetValueWithoutNotify(document.cellWidth);
                Q<FloatField>("cellHeight").SetValueWithoutNotify(document.cellHeight);
                Q<FloatField>("originX").SetValueWithoutNotify(document.originX);
                Q<FloatField>("originY").SetValueWithoutNotify(document.originY);
            }
            UpdateSelection(document);
        }

        public override void SaveChanges()
        {
            if (Save(false))
                base.SaveChanges();
        }

        private bool ConfirmDiscard()
        {
            if (!_session.IsDirty)
                return true;
            int answer = EditorUtility.DisplayDialogComplex("地图尚未保存", "保存当前地图后继续？", "保存", "取消", "不保存");
            return answer == 2 || answer == 0 && Save(false);
        }

        private void NewDocument()
        {
            if (!ConfirmDiscard())
                return;
            _session = new MapEditSession(NewLayout());
            _path = null;
            _selectedId = null;
            _wallTail = null;
            _canvas.Fit();
            Refresh();
        }

        private void OpenDocument()
        {
            if (!ConfirmDiscard())
                return;
            string path = EditorUtility.OpenFilePanel("打开地图 JSON", "Assets/Content/Maps", "json");
            if (string.IsNullOrEmpty(path))
                return;
            LoadDocument(path);
        }

        public void OpenMapAsset(string path)
        {
            if (!ConfirmDiscard())
                return;
            LoadDocument(path);
        }

        private void LoadDocument(string path)
        {
            var document = MapJson.ReadDocument(File.ReadAllText(path));
            _session = new MapEditSession(document, true);
            _path = path;
            _selectedId = null;
            _wallTail = null;
            _canvas.Fit();
            Refresh();
            Validate();
        }

        private bool Save(bool choosePath)
        {
            var path = _path;
            if (choosePath || string.IsNullOrEmpty(path))
                path = EditorUtility.SaveFilePanel("保存地图源文档", "Assets/Content/Maps", _session.Document.id + ".source.json", "json");
            if (string.IsNullOrEmpty(path))
                return false;
            WriteAtomically(path, MapJson.WriteDocument(_session.Document));
            _path = path;
            _session.MarkSaved();
            AssetDatabase.Refresh();
            Refresh(false);
            SetStatus("地图源文档已保存（允许保留待修复的校验问题）");
            return true;
        }

        internal static void WriteAtomically(string path, string json)
        {
            var temporary = path + ".writing";
            File.WriteAllText(temporary, json, new UTF8Encoding(false));
            if (File.Exists(path))
                File.Replace(temporary, path, null);
            else
                File.Move(temporary, path);
        }

        private void ApplyGeometry()
        {
            var document = _session.Document;
            int oldColumns = document.columns;
            int oldRows = document.rows;
            var oldTerrain = document.terrain;
            document.id = Q<TextField>("mapId").value.Trim();
            document.columns = Q<IntegerField>("columns").value;
            document.rows = Q<IntegerField>("rows").value;
            document.cellWidth = Q<FloatField>("cellWidth").value;
            document.cellHeight = Q<FloatField>("cellHeight").value;
            document.originX = Q<FloatField>("originX").value;
            document.originY = Q<FloatField>("originY").value;
            var geometry = document.Geometry;
            document.terrain = new TerrainKind[geometry.CellCount];
            for (int y = 0; y < Math.Min(oldRows, document.rows); y++)
                Array.Copy(oldTerrain, y * oldColumns, document.terrain, y * document.columns, Math.Min(oldColumns, document.columns));
            _session.Commit(document);
            _wallTail = null;
            _canvas.Fit();
            Refresh();
            Validate();
            SetStatus("设置已应用。位置标记保持原地；缩小地图后请检查越界内容。");
        }

        private int Tool => (int)_palette.Tool;

        private void BeginGesture(Vector2 point, int button)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            _beforeGesture = _session.Document;
            _gesture = _beforeGesture.Copy();
            _gestureChanged = false;
            _pressPoint = point;
            _lastPaintCell = null;
            if (Tool == 0)
            {
                _selectedId = Pick(_gesture, point);
                _canvas.SelectedId = _selectedId;
                UpdateSelection(_gesture);
                _canvas.MarkDirtyRepaint();
            }
            else
                EditAt(point, false);
        }

        private void ContinueGesture(Vector2 point)
        {
            if (_gesture == null)
                return;
            if (Tool == 0 && _selectedId != null)
            {
                Vector2 delta = point - _pressPoint;
                if (_selectedId.StartsWith("obstacle/"))
                {
                    var parts = _selectedId.Split('/');
                    int source = int.Parse(parts[2]) * _beforeGesture.columns + int.Parse(parts[1]);
                    if (_beforeGesture.Geometry.TryWorldToCell(point.x, point.y, out var destination))
                    {
                        _gesture = _beforeGesture.Copy();
                        var kind = _gesture.terrain[source];
                        _gesture.terrain[source] = _gesture.GroundAt(source);
                        MapEditSession.Paint(_gesture, _catalog, _gesture.Geometry.IndexOf(destination), kind);
                        _gestureChanged = _gesture.Geometry.IndexOf(destination) != source;
                    }
                }
                var facility = _gesture.facilities.Find(item => item.id == _selectedId);
                var original = _beforeGesture.facilities.Find(item => item.id == _selectedId);
                if (facility != null)
                {
                    facility.x = original.x + Mathf.RoundToInt(delta.x / _gesture.cellWidth);
                    facility.y = original.y + Mathf.RoundToInt(delta.y / _gesture.cellHeight);
                    _gestureChanged = facility.x != original.x || facility.y != original.y;
                }
                var marker = _gesture.markers.Find(item => item.id == _selectedId);
                if (marker != null && _gesture.Geometry.TryWorldToCell(point.x, point.y, out var cell))
                {
                    var center = _gesture.Geometry.CellCenter(cell);
                    marker.x = center.X;
                    marker.y = center.Y;
                    _gestureChanged = true;
                }
                var node = _gesture.wallNodes.Find(item => item.id == _selectedId);
                if (node != null && _gesture.Geometry.TryWorldToCell(point.x, point.y, out var nodeCell))
                {
                    var center = _gesture.Geometry.CellCenter(nodeCell);
                    node.x = center.X;
                    node.y = center.Y;
                    _gestureChanged = true;
                }
                var gate = WallGateRules.Find(_gesture, _selectedId, out var gateEdge);
                if (gate != null)
                {
                    var originalGate = WallGateRules.Find(_beforeGesture, _selectedId, out var originalEdge);
                    var center = WallGateRules.Center(_beforeGesture, originalEdge, originalGate);
                    gate.startTile = WallGateRules.SnapStartTile(_gesture, gateEdge,
                        new Game.MapEngine.Domain.MapPoint(center.X + delta.x, center.Y + delta.y), gate.widthTiles);
                    _gestureChanged = gate.startTile != originalGate.startTile;
                }
                _canvas.Layout = _gesture;
                _canvas.MarkDirtyRepaint();
            }
            else if (Tool == 1)
                EditAt(point, true);
        }

        private void EditAt(Vector2 point, bool dragging)
        {
            var geometry = _gesture.Geometry;
            if (!geometry.TryWorldToCell(point.x, point.y, out var cell))
                return;
            var center = geometry.CellCenter(cell);
            if (Tool == 1)
            {
                var terrain = _palette.Terrain;
                var start = _lastPaintCell ?? cell;
                int steps = Math.Max(Math.Abs(cell.X - start.X), Math.Abs(cell.Y - start.Y));
                for (int i = 0; i <= steps; i++)
                {
                    float t = steps == 0 ? 0 : (float)i / steps;
                    var painted = new GridCell(Mathf.RoundToInt(Mathf.Lerp(start.X, cell.X, t)), Mathf.RoundToInt(Mathf.Lerp(start.Y, cell.Y, t)));
                    int index = geometry.IndexOf(painted);
                    if (_gesture.terrain[index] != terrain || _gesture.GroundAt(index) != terrain)
                    {
                        MapEditSession.Paint(_gesture, _catalog, index, terrain);
                        _gestureChanged = true;
                    }
                }
                _lastPaintCell = cell;
            }
            else if (!dragging && (Tool == 2 || Tool == 3))
            {
                var kind = Tool == 2 ? MarkerKind.HeroSpawn : MarkerKind.EnemyEntry;
                var marker = kind == MarkerKind.HeroSpawn ? _gesture.markers.Find(item => item.kind == kind) : null;
                if (marker == null)
                {
                    marker = new MapMarker { id = MapLayout.NewId(), kind = kind, name = kind == MarkerKind.HeroSpawn ? "英雄出生" : "入口 " + (_gesture.markers.Count + 1) };
                    _gesture.markers.Add(marker);
                }
                marker.x = center.X;
                marker.y = center.Y;
                _selectedId = marker.id;
                _gestureChanged = true;
            }
            else if (!dragging && Tool == 4)
            {
                var facility = new InitialFacility { id = MapLayout.NewId(), definitionId = _palette.FacilityId, x = cell.X, y = cell.Y };
                if (!MapEditSession.TryReplaceFacility(_gesture, _catalog, facility, out var replacement, out var reason))
                {
                    SetStatus(reason);
                    return;
                }
                _gesture = replacement;
                _selectedId = facility.id;
                _gestureChanged = true;
            }
            else if (!dragging && Tool == 5)
            {
                var node = _gesture.wallNodes.Find(item => Vector2.Distance(new Vector2(item.x, item.y), new Vector2(center.X, center.Y)) < 0.05f);
                if (node == null)
                {
                    node = new WallNode { id = MapLayout.NewId(), x = center.X, y = center.Y };
                    _gesture.wallNodes.Add(node);
                    _gestureChanged = true;
                }
                var tail = _gesture.wallNodes.Find(item => item.id == _wallTail);
                if (tail != null && tail.id != node.id && !_gesture.wallEdges.Exists(edge => edge.from == tail.id && edge.to == node.id || edge.to == tail.id && edge.from == node.id))
                {
                    var walls = new WallNetwork(MapWallSnapshotMapper.Read(_gesture));
                    var edge = new WallEdgeSnapshot(MapLayout.NewId(), tail.id, node.id);
                    if (!walls.TryConnect(edge, out var reason))
                    {
                        SetStatus(reason);
                        return;
                    }
                    MapWallSnapshotMapper.Write(walls.Snapshot, _gesture);
                    _gestureChanged = true;
                }
                _wallTail = node.id;
                _selectedId = node.id;
            }
            else if (!dragging && Tool == (int)MapEditorTool.Gate)
            {
                string id = Pick(_gesture, point);
                var selectedGate = WallGateRules.Find(_gesture, id, out _);
                if (selectedGate != null)
                {
                    _selectedId = selectedGate.id;
                }
                else
                {
                    var edge = _gesture.wallEdges.Find(item => item.id == id);
                    if (edge == null)
                    {
                        SetStatus("请点击任意方向墙段的地面线，避开端点和建筑。");
                        return;
                    }
                    int width = Q<IntegerField>("gateWidth").value;
                    var gate = new WallGate
                    {
                        id = MapLayout.NewId(),
                        startTile = WallGateRules.SnapStartTile(_gesture, edge, new Game.MapEngine.Domain.MapPoint(point.x, point.y), width),
                        widthTiles = width,
                        openingTiles = Q<IntegerField>("gateOpening").value,
                        isOpen = Q<Toggle>("gateOpen").value
                    };
                    if (!WallGateRules.TryPlace(_gesture, edge, gate, _catalog.Wall, out var reason))
                    {
                        SetStatus(reason);
                        return;
                    }
                    _selectedId = gate.id;
                    _gestureChanged = true;
                }
            }
            _canvas.Layout = _gesture;
            _canvas.SelectedId = _selectedId;
            _canvas.MarkDirtyRepaint();
        }

        private void EndGesture()
        {
            if (_gesture == null)
                return;
            if (_gestureChanged && Tool == 0)
            {
                foreach (var edge in _gesture.wallEdges)
                {
                    if (!WallGateRules.Validate(_gesture, edge, out var gateReason, _catalog.Wall))
                    {
                        _gesture = null;
                        _beforeGesture = null;
                        Refresh(false);
                        SetStatus("移动未提交：" + gateReason);
                        return;
                    }
                }
                var facility = _gesture.facilities.Find(item => item.id == _selectedId);
                if (facility != null && !MapEditSession.TryReplaceFacility(_gesture, _catalog, facility, out _, out var reason))
                {
                    _gesture = null;
                    _beforeGesture = null;
                    Refresh(false);
                    SetStatus("移动未提交：" + reason);
                    return;
                }
            }
            if (_gestureChanged)
                _session.Commit(_gesture);
            _gesture = null;
            _beforeGesture = null;
            Refresh(false);
            if (_gestureChanged)
                Validate();
        }

        private string Pick(MapLayout document, Vector2 point)
        {
            string gateId = Game.Maps.Unity.Views.MapSceneSprites.PickGate(document, _visuals, point, _catalog);
            if (gateId != null)
                return gateId;
            float tolerance = Mathf.Min(document.cellWidth, document.cellHeight) * 0.4f;
            var marker = document.markers.Find(item => Vector2.Distance(new Vector2(item.x, item.y), point) < tolerance);
            if (marker != null)
                return marker.id;
            var node = document.wallNodes.Find(item => Vector2.Distance(new Vector2(item.x, item.y), point) < tolerance);
            if (node != null)
                return node.id;
            if (document.Geometry.TryWorldToCell(point.x, point.y, out var cell))
            {
                var building = document.facilities.Find(item =>
                {
                    var definition = _catalog.Find(item.definitionId);
                    return definition != null && cell.X >= item.x && cell.X < item.x + definition.width && cell.Y >= item.y && cell.Y < item.y + definition.height;
                });
                if (building != null)
                    return building.id;
            }
            if (document.Geometry.TryWorldToCell(point.x, point.y, out var obstacleCell) &&
                MapLayout.IsTerrainObstacle(document.terrain[document.Geometry.IndexOf(obstacleCell)]))
                return "obstacle/" + obstacleCell.X + "/" + obstacleCell.Y;
            var edge = document.wallEdges.Find(item =>
            {
                var a = document.wallNodes.Find(value => value.id == item.from);
                var b = document.wallNodes.Find(value => value.id == item.to);
                return a != null && b != null && SegmentGeometry.DistanceToSegment(point.x, point.y, a.x, a.y, b.x, b.y) < tolerance;
            });
            return edge?.id;
        }

        private void UpdateSelection(MapLayout document)
        {
            var marker = document.markers.Find(item => item.id == _selectedId);
            var facility = document.facilities.Find(item => item.id == _selectedId);
            var node = document.wallNodes.Find(item => item.id == _selectedId);
            Q<Label>("selection").text = marker != null ? $"{marker.kind}: {marker.name}\n({marker.x}, {marker.y})\n{marker.id}" : facility != null ? $"{facility.definitionId} 格子 ({facility.x}, {facility.y})\n{facility.id}" : node != null ? $"墙节点 ({node.x}, {node.y})\n{node.id}" : _selectedId ?? "尚未选择对象";
            Q<TextField>("markerName").SetValueWithoutNotify(marker?.name ?? "");
            Q<Button>("rename").SetEnabled(marker != null);
            var gate = WallGateRules.Find(document, _selectedId, out _);
            Q<Button>("applyGate").SetEnabled(gate != null);
            Q<Button>("removeGate").SetEnabled(gate != null);
            if (gate != null)
            {
                Q<IntegerField>("gateWidth").SetValueWithoutNotify(gate.widthTiles);
                Q<IntegerField>("gateOpening").SetValueWithoutNotify(gate.openingTiles);
                Q<Toggle>("gateOpen").SetValueWithoutNotify(gate.isOpen);
                Q<Label>("selection").text = $"城门 · {(gate.isOpen ? "初始开启" : "初始关闭")}\n门框 {gate.widthTiles} Tile / 净宽 {gate.openingTiles} Tile\n{gate.id}";
            }
        }

        /// <summary>提交所选城门的整数 Tile 配置；按钮与编辑器自动化共用此入口。</summary>
        public void ApplyGate()
        {
            var document = _session.Document;
            var gate = WallGateRules.Find(document, _selectedId, out var edge);
            if (gate == null)
                return;
            var changed = gate.Copy();
            changed.widthTiles = Q<IntegerField>("gateWidth").value;
            changed.openingTiles = Q<IntegerField>("gateOpening").value;
            changed.isOpen = Q<Toggle>("gateOpen").value;
            if (!WallGateRules.TryPlace(document, edge, changed, _catalog.Wall, out var reason))
            {
                SetStatus(reason);
                return;
            }
            _session.Commit(document);
            Refresh(false);
            Validate();
        }

        private void RemoveGate()
        {
            var document = _session.Document;
            var gate = WallGateRules.Find(document, _selectedId, out var edge);
            if (gate == null)
                return;
            edge.gates.Remove(gate);
            _session.Commit(document);
            _selectedId = null;
            Refresh(false);
            Validate();
        }

        private void RenameMarker()
        {
            var document = _session.Document;
            var marker = document.markers.Find(item => item.id == _selectedId);
            if (marker == null)
                return;
            marker.name = Q<TextField>("markerName").value.Trim();
            _session.Commit(document);
            Refresh(false);
        }

        private void DeleteSelection()
        {
            if (_selectedId == null)
                return;
            var document = _session.Document;
            foreach (var edge in document.wallEdges)
                edge.gates.RemoveAll(gate => gate.id == _selectedId);
            if (_selectedId.StartsWith("obstacle/"))
            {
                var parts = _selectedId.Split('/');
                int index = int.Parse(parts[2]) * document.columns + int.Parse(parts[1]);
                document.terrain[index] = document.GroundAt(index);
            }
            document.markers.RemoveAll(item => item.id == _selectedId);
            document.facilities.RemoveAll(item => item.id == _selectedId);
            document.wallEdges.RemoveAll(item => item.id == _selectedId || item.from == _selectedId || item.to == _selectedId);
            document.wallNodes.RemoveAll(item => item.id == _selectedId);
            _session.Commit(document);
            _selectedId = null;
            _wallTail = null;
            Refresh(false);
        }

        private void ShowHover(Vector2 point)
        {
            var document = _gesture ?? _canvas.Layout;
            if (document.Geometry.TryWorldToCell(point.x, point.y, out var cell))
            {
                var terrain = document.terrain[document.Geometry.IndexOf(cell)];
                SetStatus($"地面 ({point.x:F2}, {point.y:F2}) · 格子 ({cell.X}, {cell.Y}) · {MapPalette.TerrainName(terrain)} · 地形通行 {TerrainRules.IsWalkable(terrain)} · 可建设 {TerrainRules.IsBuildable(terrain)} · 射击阻挡 {TerrainRules.BlocksShot(terrain)}");
            }
        }

        private string IssueText(MapIssue issue)
        {
            if (issue.X >= 0 && issue.Y >= 0)
                return $"格子 ({issue.X}, {issue.Y}) · {issue.Message}";
            var document = _session.Document;
            var facility = document.facilities.Find(item => item.id == issue.ObjectId);
            if (facility != null)
                return $"格子 ({facility.x}, {facility.y}) · {issue.Message}";
            var node = document.wallNodes.Find(item => item.id == issue.ObjectId);
            var marker = document.markers.Find(item => item.id == issue.ObjectId);
            var edge = document.wallEdges.Find(item => item.id == issue.ObjectId || item.gates.Exists(gate => gate.id == issue.ObjectId));
            if (edge != null)
            {
                var a = document.wallNodes.Find(item => item.id == edge.from);
                var b = document.wallNodes.Find(item => item.id == edge.to);
                if (a != null && b != null)
                    return $"地面 ({a.x}, {a.y}) → ({b.x}, {b.y}) · {issue.Message}";
            }
            if (node != null)
                return $"地面 ({node.x}, {node.y}) · {issue.Message}";
            if (marker != null)
                return $"地面 ({marker.x}, {marker.y}) · {issue.Message}";
            return issue.Message;
        }

        private void Validate()
        {
            var issues = ValidateDocument();
            var host = Q<VisualElement>("issues");
            host.Clear();
            foreach (var issue in issues)
            {
                var button = new Button(() =>
                {
                    _selectedId = issue.ObjectId;
                    Refresh(false);
                    if (issue.X >= 0 && issue.Y >= 0)
                        _canvas.Locate(new GridCell(issue.X, issue.Y));
                    else
                        LocateObject(issue.ObjectId);
                    SetStatus(issue.Message);
                }) { text = IssueText(issue) };
                button.AddToClassList("issue");
                host.Add(button);
            }
            if (issues.Count == 0)
                host.Add(new Label("校验通过"));
            SetStatus($"校验完成：{issues.Count} 个问题");
        }

        private MapDefinition RequireValid()
        {
            Validate();
            var error = ValidateDocument().Find(issue => issue.IsError);
            if (error != null)
                throw new InvalidOperationException(error.Message);
            return new MapDefinition(_session.Document, _catalog);
        }

        private void LocateObject(string id)
        {
            var layout = _session.Document;
            var gate = WallGateRules.Find(layout, id, out var owner);
            if (gate != null && WallGateRules.TryGetSpan(layout, owner, gate, out var span))
            {
                _canvas.LocatePoint(span.Center.X, span.Center.Y);
                return;
            }
            var marker = layout.markers.Find(item => item.id == id);
            var node = layout.wallNodes.Find(item => item.id == id);
            var facility = layout.facilities.Find(item => item.id == id);
            var edge = layout.wallEdges.Find(item => item.id == id);
            if (marker != null)
                _canvas.LocatePoint(marker.x, marker.y);
            else if (node != null)
                _canvas.LocatePoint(node.x, node.y);
            else if (facility != null)
                _canvas.LocatePoint(layout.originX + (facility.x + 0.5f) * layout.cellWidth, layout.originY + (facility.y + 0.5f) * layout.cellHeight);
            else if (edge != null)
            {
                var a = layout.wallNodes.Find(item => item.id == edge.from);
                var b = layout.wallNodes.Find(item => item.id == edge.to);
                if (a != null && b != null)
                    _canvas.LocatePoint((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f);
            }
            else
                _canvas.Fit();
        }

        private System.Collections.Generic.List<MapIssue> ValidateDocument()
        {
            var rulesAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Content/Combat/Prototype/battle.json");
            var rules = JsonBattleDefinitionSource.Parse(rulesAsset.text);
            return MapPlayabilityValidator.Validate(_session.Document, _catalog, rules.Hero.Radius, Math.Max(rules.Goblin.Radius, rules.Runner.Radius));
        }

        private void Export()
        {
            var definition = RequireValid();
            var path = EditorUtility.SaveFilePanel("导出游戏地图", "Assets/Content/Maps", definition.Id + ".map.json", "json");
            if (string.IsNullOrEmpty(path))
                return;
            WriteAtomically(path, MapJson.WriteDocument(definition.CreateWorkingCopy()));
            AssetDatabase.Refresh();
            SetStatus("地图已导出：" + path);
        }

        private void Play()
        {
            var definition = RequireValid();
            MapEditorSetup.StartPlaytest(MapJson.WriteDocument(definition.CreateWorkingCopy()), _visuals);
        }

        private void OnKey(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
                _wallTail = null;
            if (_canvas == null || _canvas.focusController?.focusedElement != _canvas)
                return;
            if (!evt.ctrlKey && !evt.commandKey && evt.keyCode == KeyCode.Q)
            {
                _palette.SelectTool(MapEditorTool.Pan);
                evt.StopPropagation();
                return;
            }
            if (!evt.ctrlKey && !evt.commandKey && evt.keyCode == KeyCode.V)
            {
                _palette.SelectTool(MapEditorTool.Select);
                evt.StopPropagation();
                return;
            }
            if ((evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.Z)
            {
                if (evt.shiftKey)
                    _session.Redo();
                else
                    _session.Undo();
                _wallTail = null;
                Refresh();
                evt.StopPropagation();
            }
            else if ((evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.Y)
            {
                _session.Redo();
                Refresh();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
            {
                DeleteSelection();
                evt.StopPropagation();
            }
        }
    }
}

