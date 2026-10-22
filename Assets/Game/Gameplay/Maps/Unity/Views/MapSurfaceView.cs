using Game.Maps.Unity.Assets;
using System.Collections.Generic;
using Game.Maps.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Maps.Unity.Views
{
    /// <summary>按 16×16 格合并地表 Mesh，避免大地图每个地形格都创建一个 GameObject。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Game.Maps.Unity", "Game.MapContent.Unity", "MapSurfaceView")]
    public sealed class MapSurfaceView : MonoBehaviour
    {
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<Material> _materials = new List<Material>();

        public void Build(MapLayout layout, MapVisualCatalog visuals)
        {
            for (int cy = 0; cy < layout.rows; cy += 16)
            {
                for (int cx = 0; cx < layout.columns; cx += 16)
                {
                    for (int layer = 0; layer < 4; layer++)
                        BuildChunk(layout, visuals, cx, cy, layer);
                }
            }
        }

        private void BuildChunk(MapLayout layout, MapVisualCatalog visuals, int cx, int cy, int layer)
        {
            var positions = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color32>();
            var indices = new List<int>();
            Texture texture = null;
            for (int y = cy; y < Mathf.Min(cy + 16, layout.rows); y++)
            {
                for (int x = cx; x < Mathf.Min(cx + 16, layout.columns); x++)
                {
                    var patch = MapSurfaceGeometry.Build(layout, visuals, x, y, layer);
                    if (patch == null)
                        continue;
                    texture = patch.Sprite.texture;
                    int offset = positions.Count;
                    foreach (var position in patch.Positions)
                        positions.Add(position);
                    uvs.AddRange(patch.Uvs);
                    colors.AddRange(patch.Colors);
                    foreach (int index in patch.Indices)
                        indices.Add(offset + index);
                }
            }
            if (positions.Count == 0)
                return;
            var mesh = new Mesh { name = $"Map surface {cx},{cy} layer {layer}", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(positions);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            _meshes.Add(mesh);
            var material = new Material(Shader.Find("Sprites/Default")) { mainTexture = texture };
            _materials.Add(material);
            var go = new GameObject(mesh.name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = -3000 + layer;
        }

        private void OnDestroy()
        {
            foreach (var mesh in _meshes)
            {
                if (Application.isPlaying) Destroy(mesh);
                else DestroyImmediate(mesh);
            }
            foreach (var material in _materials)
            {
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
        }
    }
}
