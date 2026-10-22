using System.Collections.Generic;
using UnityEngine;

namespace Game.Maps.Unity.Views
{
    /// <summary>一次地图视图拥有的共享墙材质，避免每个遮挡片段分配一份相同材质。</summary>
    public sealed class MapWallMaterialLibrary : MonoBehaviour
    {
        private readonly Dictionary<Texture, Material> _materials = new Dictionary<Texture, Material>();

        public Material Require(Texture texture)
        {
            if (!_materials.TryGetValue(texture, out var material))
            {
                material = new Material(Shader.Find("Sprites/Default")) { mainTexture = texture };
                _materials.Add(texture, material);
            }
            return material;
        }

        private void OnDestroy()
        {
            foreach (var material in _materials.Values)
            {
                if (Application.isPlaying)
                    Destroy(material);
                else
                    DestroyImmediate(material);
            }
            _materials.Clear();
        }
    }
}
