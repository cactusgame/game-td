using Game.Maps.Unity.Assets;
using System;
using System.Collections.Generic;
using Game.MapEngine.Domain;
using Game.Maps.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Tools.MapAuthoring.Editor
{
    public enum MapEditorTool
    {
        Select,
        Terrain,
        HeroSpawn,
        EnemyEntry,
        Facility,
        Wall,
        Pan
    }

    /// <summary>所有可选工具与素材集中显示；点选素材同时切换编辑模式，不再需要先选工具再找素材。</summary>
    public sealed class MapPalette : VisualElement
    {
        private readonly Dictionary<string, Button> _cards = new Dictionary<string, Button>();
        private readonly Label _current;
        public MapEditorTool Tool { get; private set; }
        public TerrainKind Terrain { get; private set; }
        public string FacilityId { get; private set; }
        public event Action SelectionChanged;

        public MapPalette(MapVisualCatalog visuals, MapContentCatalog catalog, MapEditorTool tool, TerrainKind terrain, string facilityId)
        {
            AddToClassList("map-palette");
            Tool = tool;
            Terrain = terrain;
            FacilityId = catalog.Find(facilityId) != null ? facilityId : catalog.FacilityIds.Count > 0 ? catalog.FacilityIds[0] : "";
            _current = new Label();
            _current.AddToClassList("palette-current");
            Add(_current);

            var tools = Group("工具", "palette-tools");
            AddTool(tools, MapEditorTool.Select, "选择移动", "点击选择对象，拖动改变位置");
            AddTool(tools, MapEditorTool.Pan, "平移 Q", "Q 切换平移工具，左键拖动地图；空格或中键也可临时平移");
            AddTool(tools, MapEditorTool.HeroSpawn, "英雄出生", "点击地图设置唯一的英雄出生位置");
            AddTool(tools, MapEditorTool.EnemyEntry, "敌人入口", "点击地图新增敌人入口");
            var wall = Card("tool-Wall", "城墙", "连续点击绘制墙网，Esc 结束折线", () => SelectTool(MapEditorTool.Wall));
            wall.Insert(0, Preview(visuals.WallBodySprite));
            tools.Add(wall);

            var terrains = Group("地形与障碍", "palette-terrain");
            foreach (TerrainKind kind in Enum.GetValues(typeof(TerrainKind)))
            {
                var item = kind;
                var visual = visuals.Terrain(item);
                string name = TerrainName(item);
                string rule = TerrainRules.IsBuildable(item) ? "可通行 · 可建设" : "阻挡通行 · 不可建设";
                var card = Card("terrain-" + item, name, name + "\n" + rule, () => SelectTerrain(item));
                var preview = Preview(visual?.ground);
                if (visual?.obstacle != null)
                {
                    var obstacle = new Image { sprite = visual.obstacle, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                    obstacle.AddToClassList("palette-overlay");
                    preview.Add(obstacle);
                }
                card.Insert(0, item == TerrainKind.Road && visuals.blendTerrain ? new MapTerrainThumbnail(visuals) : preview);
                terrains.Add(card);
            }

            var facilities = Group("建筑", "palette-buildings");
            foreach (var id in catalog.FacilityIds)
            {
                string definitionId = id;
                var definition = catalog.Find(id);
                string name = string.IsNullOrEmpty(definition.name) ? id : definition.name;
                var card = Card("facility-" + id, name, $"{name}\n占地 {definition.width} × {definition.height} 格", () => SelectFacility(definitionId));
                card.Insert(0, Preview(visuals.Facility(id)));
                facilities.Add(card);
            }
            UpdateSelection();
        }

        public void SelectTool(MapEditorTool tool)
        {
            Tool = tool;
            UpdateSelection();
            SelectionChanged?.Invoke();
        }

        public void SelectTerrain(TerrainKind terrain)
        {
            Terrain = terrain;
            SelectTool(MapEditorTool.Terrain);
        }

        public void SelectFacility(string id)
        {
            FacilityId = id;
            SelectTool(MapEditorTool.Facility);
        }

        private VisualElement Group(string title, string className)
        {
            var label = new Label(title);
            label.AddToClassList("palette-heading");
            Add(label);
            var group = new VisualElement();
            group.AddToClassList("palette-grid");
            group.AddToClassList(className);
            Add(group);
            return group;
        }

        private void AddTool(VisualElement group, MapEditorTool tool, string label, string tooltip)
        {
            var card = Card("tool-" + tool, label, tooltip, () => SelectTool(tool));
            card.Insert(0, new MapToolIcon(tool));
            group.Add(card);
        }

        private Button Card(string id, string title, string description, Action select)
        {
            var button = new Button(select) { name = id, tooltip = description };
            button.AddToClassList("palette-card");
            var label = new Label(title) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("palette-caption");
            button.Add(label);
            _cards.Add(id, button);
            return button;
        }

        private static VisualElement Preview(Sprite sprite)
        {
            var image = new Image { sprite = sprite, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            image.AddToClassList("palette-thumbnail");
            return image;
        }

        private void UpdateSelection()
        {
            string selected = Tool == MapEditorTool.Terrain ? "terrain-" + Terrain : Tool == MapEditorTool.Facility ? "facility-" + FacilityId : "tool-" + Tool;
            foreach (var pair in _cards)
                pair.Value.EnableInClassList("palette-selected", pair.Key == selected);
            _current.text = Tool == MapEditorTool.Terrain ? "当前：" + TerrainName(Terrain) + " · 地形笔刷" :
                Tool == MapEditorTool.Facility ? "当前：放置建筑 · 点击地图放置" :
                Tool == MapEditorTool.Select ? "当前：选择 / 移动" :
                Tool == MapEditorTool.Pan ? "当前：平移视图 · 拖动地图" :
                Tool == MapEditorTool.Wall ? "当前：绘制城墙 · Esc 结束折线" :
                Tool == MapEditorTool.HeroSpawn ? "当前：设置英雄出生位置" : "当前：设置敌人入口";
        }

        public static string TerrainName(TerrainKind kind)
        {
            switch (kind)
            {
                case TerrainKind.Grass: return "草地";
                case TerrainKind.Dirt: return "泥地";
                case TerrainKind.Road: return "道路";
                case TerrainKind.Water: return "河流";
                case TerrainKind.Rock: return "岩石";
                case TerrainKind.Tree: return "树木";
                default: return kind.ToString();
            }
        }
    }
}
