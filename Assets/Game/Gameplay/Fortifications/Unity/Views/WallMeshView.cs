using Game.MapEngine.Unity.Views;
using UnityEngine;

namespace Game.Fortifications.Unity.Views
{
    /// <summary>绘制一个墙体遮挡片段，拥有 Mesh；地图批量生成时由地图视图提供共享材质。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Game.Maps.Unity.Views", "Game.Maps.Unity", "MapWallMeshView")]
    public sealed class WallMeshView : MonoBehaviour
    {
        private Mesh _mesh;
        private Material _material;
        private bool _ownsMaterial;

        public void Build(SceneRenderEntry entry, int order, Material sharedMaterial = null)
        {
            var patch = entry.Wall.Mesh;
            var sprite = patch.Sprite;
            var texture = sprite.texture;
            _mesh = new Mesh { name = "Wall " + entry.Id };
            var positions = new Vector3[patch.Positions.Length];
            for (int i = 0; i < positions.Length; i++)
                positions[i] = patch.Positions[i];
            _mesh.vertices = positions;
            _mesh.colors32 = patch.Colors;
            _mesh.uv = patch.Uvs;
            _mesh.triangles = patch.Indices;
            _mesh.RecalculateBounds();
            _ownsMaterial = sharedMaterial == null;
            _material = sharedMaterial != null ? sharedMaterial : new Material(Shader.Find("Sprites/Default")) { mainTexture = texture };
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.sortingOrder = order;
        }

        private void OnDestroy()
        {
            if (Application.isPlaying)
            {
                Destroy(_mesh);
                if (_ownsMaterial)
                    Destroy(_material);
            }
            else
            {
                DestroyImmediate(_mesh);
                if (_ownsMaterial)
                    DestroyImmediate(_material);
            }
        }
    }
}
