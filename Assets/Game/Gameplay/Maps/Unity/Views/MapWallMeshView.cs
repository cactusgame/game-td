using UnityEngine;

namespace Game.Maps.Unity.Views
{
    /// <summary>绘制一个遮挡片段内的墙身、墙顶与垛口；拥有并释放运行时 Mesh 与材质。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Game.Maps.Unity", "Game.MapContent.Unity", "MapWallMeshView")]
    public sealed class MapWallMeshView : MonoBehaviour
    {
        private Mesh _mesh;
        private Material _material;

        public void Build(MapSceneSprites.Entry entry, int order)
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
            _material = new Material(Shader.Find("Sprites/Default")) { mainTexture = texture };
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
                Destroy(_material);
            }
            else
            {
                DestroyImmediate(_mesh);
                DestroyImmediate(_material);
            }
        }
    }
}
