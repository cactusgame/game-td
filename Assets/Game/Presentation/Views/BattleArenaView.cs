using UnityEngine;

namespace Game.Presentation.Views
{
    /// <summary>从对局地图尺寸重建视觉地面，防止 JSON 与旧场景地面大小不一致。</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class BattleArenaView : MonoBehaviour
    {
        private Mesh _runtimeMesh;
        public void Configure(float width, float height, BattleMapViewSettings settings)
        {
            if (_runtimeMesh == null)
                _runtimeMesh = new Mesh{name = "Battle ground (runtime)"};
            BattleArenaMesh.Build(_runtimeMesh, width, height, settings);
            GetComponent<MeshFilter>().sharedMesh = _runtimeMesh;
            var renderer = GetComponent<MeshRenderer>();
            renderer.sortingLayerName = BattleRenderSorting.GroundLayer;
            renderer.sortingOrder = 0;
        }

        private void OnDestroy()
        {
            if (_runtimeMesh != null)
                Destroy(_runtimeMesh);
        }
    }
}
