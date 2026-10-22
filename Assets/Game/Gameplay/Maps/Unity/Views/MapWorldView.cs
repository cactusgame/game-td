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
        public void Rebuild(MapLayout layout, MapVisualCatalog visuals, MapContentCatalog catalog)
        {
            if (_root != null)
            {
                _root.SetActive(false);
                Destroy(_root);
            }
            _root = new GameObject("Map state view");
            _root.transform.SetParent(transform, false);
            if (visuals.blendTerrain)
                _root.AddComponent<MapSurfaceView>().Build(layout, visuals);
            else
            {
                var geometry = layout.Geometry;
                for (int i = 0; i < geometry.CellCount; i++)
                {
                    var cell = geometry.CellAt(i);
                    var center = geometry.CellCenter(cell);
                    AddSprite("Ground", visuals.Terrain(layout.terrain[i])?.ground, center.X, center.Y, layout.cellWidth, layout.cellHeight, -3000, false);
                }
            }
            int depthIndex = 0;
            foreach (var entry in MapSceneSprites.Build(layout, visuals, catalog))
            {
                GameObject go;
                if (entry.Wall != null)
                {
                    go = new GameObject(entry.Id);
                    go.transform.SetParent(_root.transform, false);
                    go.AddComponent<MapWallMeshView>().Build(entry, Sort(entry.GroundY));
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

        public static int Sort(float y) => Mathf.Clamp(1000 - Mathf.RoundToInt(y * 10), -1000, 30000);
    }
}
