using Game.Fortifications.Unity.Views;
using System.Collections.Generic;
using Game.Maps.Domain.Mappings;
using Game.Fortifications.Domain.Definitions;
using Game.Fortifications.Domain.ValueObjects;
using Game.Maps.Unity.Assets;
using Game.MapEngine.Domain;
using Game.Maps.Domain;
using UnityEngine;

namespace Game.Maps.Unity.Views
{
    /// <summary>只从地图状态重建表现。Sprite 外框不参与占地或碰撞裁决。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Game.Maps.Unity", "Game.MapContent.Unity", "MapWorldView")]
    public sealed class MapWorldView : MonoBehaviour
    {
        private GameObject _root;
        private readonly Dictionary<string, GameObject[]> _gateVariants = new Dictionary<string, GameObject[]>();
        private readonly Dictionary<string, int> _conditions = new Dictionary<string, int>();
        private MapVisualCatalog _visuals;
        private MapContentCatalog _catalog;
        private bool _conditionChanged;

        /// <summary>生命只用于计算表现档位；不保存可写生命，也不裁决伤害和通行。</summary>
        public void SetCondition(string id, int health, int maximum, float damagedRatio)
        {
            int state = health <= 0 ? 2 : maximum > 0 && (double)health / maximum < damagedRatio ? 1 : 0;
            if ((_conditions.TryGetValue(id, out var previous) ? previous : 0) == state)
                return;
            _conditions[id] = state;
            _conditionChanged = true;
        }

        public void RefreshConditions(MapLayout layout)
        {
            if (!_conditionChanged)
                return;
            _conditionChanged = false;
            Rebuild(layout, _visuals, _catalog);
        }

        private int Condition(string id) => _conditions.TryGetValue(id, out var state) ? state : 0;

        /// <summary>仅切换城门的预生成表现，不创建 Mesh、材质或重建地图。</summary>
        public void SynchronizeGates(MapLayout layout)
        {
            foreach (var edge in layout.wallEdges)
            {
                foreach (var gate in edge.gates)
                {
                    if (!_gateVariants.TryGetValue(gate.id, out var variants))
                        continue;
                    int active = gate.isDestroyed ? 2 : gate.isOpen ? 1 : 0;
                    for (int i = 0; i < variants.Length; i++)
                        if (variants[i].activeSelf != (i == active))
                            variants[i].SetActive(i == active);
                }
            }
        }

        /// <summary>仅地图布置或美术改变时重建静态地图，并预生成各座门的三种状态。</summary>
        public void Rebuild(MapLayout layout, MapVisualCatalog visuals, MapContentCatalog catalog)
        {
            _visuals = visuals;
            _catalog = catalog;
            if (_root != null)
            {
                _root.SetActive(false);
                Destroy(_root);
            }
            _gateVariants.Clear();
            _root = new GameObject("Map state view");
            _root.transform.SetParent(transform, false);
            var wallMaterials = _root.AddComponent<WallMaterialLibrary>();
            if (visuals.blendTerrain)
                _root.AddComponent<MapSurfaceView>().Build(layout, visuals);
            else
            {
                var geometry = layout.Geometry;
                for (int i = 0; i < geometry.CellCount; i++)
                {
                    var cell = geometry.CellAt(i);
                    var center = geometry.CellCenter(cell);
                    AddSprite("Ground", visuals.Terrain(layout.GroundAt(i))?.ground, center.X, center.Y, layout.cellWidth, layout.cellHeight, -3000, false);
                }
            }
            var network = MapWallSnapshotMapper.Read(layout);
            var appearance = new WallDefinition(catalog.Wall.Width * visuals.wallWidthScale,
                catalog.Wall.Height * visuals.wallHeightScale,
                Mathf.Max(catalog.Wall.JunctionRadius, catalog.Wall.Width * visuals.wallWidthScale * 0.6f), catalog.Wall.JunctionClearance);
            foreach (var edge in network.Edges)
            {
                foreach (var gate in edge.Gates)
                {
                    var variants = new GameObject[3];
                    _gateVariants.Add(gate.Id, variants);
                    for (int state = 0; state < variants.Length; state++)
                    {
                        var group = new GameObject($"Gate {gate.Id} state {state}");
                        group.transform.SetParent(_root.transform, false);
                        variants[state] = group;
                        // 校验使用所属快照内的同一门对象；独立复制门会被合法地判为重复 ID。
                        var stateLayout = layout.Copy();
                        var stateGate = stateLayout.wallEdges.Find(item => item.id == edge.Id).gates.Find(item => item.id == gate.Id);
                        stateGate.isOpen = state == 1;
                        stateGate.isDestroyed = state == 2;
                        var stateNetwork = MapWallSnapshotMapper.Read(stateLayout);
                        var stateEdge = stateNetwork.FindEdge(edge.Id);
                        var snapshot = System.Linq.Enumerable.First(stateEdge.Gates, item => item.Id == gate.Id);
                        int index = 0;
                        int condition = state == 2 ? 2 : Condition(gate.Id);
                        var masonry = condition == 2 ? visuals.destroyedMasonry : condition == 1 ? visuals.damagedMasonry : null;
                        var style = masonry == null ? visuals.WallStyle : visuals.WallStyle.WithMaterials(masonry, masonry, visuals.gateSideInset);
                        style.Iron = visuals.WallStyle.Iron;
                        foreach (var entry in GatePresentation.Build(stateNetwork, stateEdge, snapshot, appearance, style))
                        {
                            var part = new GameObject(entry.Id);
                            part.transform.SetParent(group.transform, false);
                            part.AddComponent<WallMeshView>().Build(entry, Sort(entry.GroundY), wallMaterials.Require(entry.Wall.Mesh.Sprite.texture));
                            part.transform.position += Vector3.back * (++index * 0.00001f);
                        }
                    }
                }
            }
            SynchronizeGates(layout);
            int depthIndex = 0;
            foreach (var entry in MapSceneSprites.Build(layout, visuals, catalog, Condition))
            {
                if (_gateVariants.ContainsKey(entry.Id))
                    continue;
                GameObject go;
                if (entry.Wall != null)
                {
                    go = new GameObject(entry.Id);
                    go.transform.SetParent(_root.transform, false);
                    go.AddComponent<WallMeshView>().Build(entry, Sort(entry.GroundY), wallMaterials.Require(entry.Wall.Mesh.Sprite.texture));
                }
                else
                {
                    go = AddSprite(entry.Id, entry.Sprite, entry.Center.x, entry.Center.y, entry.Width, entry.Height, Sort(entry.GroundY), false);
                    go.transform.rotation = Quaternion.Euler(0, 0, entry.Angle * Mathf.Rad2Deg);
                }
                // sortingOrder 同值时，正交相机再按深度排序。
                // 使用共享的完整顺序，避免重建后的 InstanceID 决定遮挡。
                go.transform.position += Vector3.back * (++depthIndex * 0.00001f);
            }
        }

        private GameObject AddSprite(string label, Sprite sprite, float x, float y, float width, float height, int sort, bool foot)
        {
            var go = new GameObject(label);
            go.transform.SetParent(_root.transform, false);
            go.transform.position = new Vector3(x, y + (foot ? height * 0.4f : 0), 0);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sort;
            if (sprite != null)
                go.transform.localScale = new Vector3(width / sprite.bounds.size.x, height / sprite.bounds.size.y, 1);
            return go;
        }

        public static int Sort(float y) => Mathf.Clamp(1000 - Mathf.RoundToInt(y * 100), -30000, 30000);
    }
}
