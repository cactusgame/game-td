using Game.Maps.Unity.Views;
using Game.Maps.Unity.Assets;
using Game.MapEngine.Domain;
using Game.Maps.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Tools.MapAuthoring.Editor
{
    /// <summary>道路缩略图使用实际自动连接规则，避免和泥地材质显示成同一张图片。</summary>
    public sealed class MapTerrainThumbnail : VisualElement
    {
        private readonly MapVisualCatalog _visuals;
        private readonly MapLayout _layout;
        public MapTerrainThumbnail(MapVisualCatalog visuals)
        {
            _visuals = visuals;
            _layout = new MapLayout { columns = 3, rows = 3, originX = 0, originY = 0, terrain = new TerrainKind[9] };
            _layout.terrain[3] = TerrainKind.Road;
            _layout.terrain[4] = TerrainKind.Road;
            _layout.terrain[7] = TerrainKind.Road;
            pickingMode = PickingMode.Ignore;
            AddToClassList("palette-thumbnail");
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            for (int layer = 0; layer < 2; layer++)
            {
                for (int y = 0; y < 3; y++)
                {
                    for (int x = 0; x < 3; x++)
                    {
                        var patch = MapSurfaceGeometry.Build(_layout, _visuals, x, y, layer);
                        if (patch == null)
                            continue;
                        var mesh = context.Allocate(patch.Positions.Length, patch.Indices.Length, patch.Sprite.texture);
                        for (int i = 0; i < patch.Positions.Length; i++)
                        {
                            var point = patch.Positions[i];
                            mesh.SetNextVertex(new Vertex { position = new Vector3(point.x / 3 * contentRect.width, (1 - point.y / 3) * contentRect.height, Vertex.nearZ), uv = patch.Uvs[i], tint = patch.Colors[i] });
                        }
                        foreach (int index in patch.Indices)
                            mesh.SetNextIndex((ushort)index);
                    }
                }
            }
        }
    }
}
