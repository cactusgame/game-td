using Game.Fortifications.Unity.Assets;
using System;
using Game.MapEngine.Domain;
using Game.Maps.Domain;
using UnityEngine;

namespace Game.Maps.Unity.Assets
{
    /// <summary>固定素材制作元数据：资源和锚点；可调占地规则单独存放 JSON。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Game.Maps.Unity", "Game.MapContent.Unity", "MapVisualCatalog")]
    [CreateAssetMenu(menuName = "Game/Map Visual Catalog")]
    public sealed class MapVisualCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class TerrainVisual
        {
            public TerrainKind kind;
            public Sprite ground;
            public Sprite obstacle;
        }
        [Serializable]
        public sealed class FacilityVisual
        {
            public string definitionId;
            public Sprite sprite;
        }
        public TerrainVisual[] terrain;
        public FacilityVisual[] facilities;
        public Sprite wall;
        [Tooltip("墙身在原 Sprite 内的归一化区域；排除透明留白和重复端柱，不修改原图片。")]
        public Rect wallBodyRegion = new Rect(0.32f, 0.06f, 0.65f, 0.4f);
        public TextAsset facilityRules;
        public bool blendTerrain;
        public Sprite[] treeVariants;
        public Sprite[] rockVariants;
        public Sprite wallVertical;
        public Sprite wallDiagonalUp;
        public Sprite wallDiagonalDown;
        public Sprite wallCorner;
        public Sprite wallEnd;
        [Tooltip("石砌侧面材质，用于纵墙与陡斜墙的立体墙身。")]
        public Sprite wallMasonry;
        [Tooltip("独立的墙顶铺地素材；与墙面分别取样，不把墙面立面压扁作为走道。")]
        public Sprite wallWalkway;
        public Sprite gateClosed;
        public Sprite gateOpen;
        [Tooltip("城门素材底部透明留白占高度比例；两态共用地面脚底锚点。")]
        public float gateFootInset = 0.025f;
        [Tooltip("城门两侧透明留白占宽度比例；墙身略伸入门框以避免露出地面缝隙。")]
        public float gateSideInset = 0.025f;
        [Tooltip("墙面重复尺度、女墙与垛口等素材制作元数据；物理墙宽和高度来自内容 JSON。")]
        public WallVisualStyle wallDimensions = new WallVisualStyle();

        public WallVisualStyle WallStyle
        {
            get
            {
                var style = wallDimensions ?? new WallVisualStyle();
                var masonry = wallMasonry != null ? wallMasonry : WallBodySprite;
                var paving = wallWalkway != null ? wallWalkway : masonry;
                return style.WithMaterials(masonry, paving, gateSideInset);
            }
        }

        public Sprite Obstacle(Game.Maps.Domain.MapLayout layout, int x, int y)
        {
            var kind = layout.terrain[y * layout.columns + x];
            var variants = kind == TerrainKind.Tree ? treeVariants : kind == TerrainKind.Rock ? rockVariants : null;
            if (variants != null && variants.Length > 0)
            {
                uint hash = unchecked((uint)(x * 73856093 ^ y * 19349663));
                return variants[hash % variants.Length];
            }
            return Terrain(kind)?.obstacle;
        }
        [NonSerialized] private Sprite _wallBody;
        [NonSerialized] private Sprite _wallBodySource;
        [NonSerialized] private Rect _wallBodySourceRegion;

        public Sprite WallBodySprite
        {
            get
            {
                if (wall == null)
                    return null;
                if (wallBodyRegion.width <= 0 || wallBodyRegion.height <= 0)
                    return wall;
                if (_wallBody != null && _wallBodySource == wall && _wallBodySourceRegion == wallBodyRegion)
                    return _wallBody;
                ReleaseWallBody();
                var region = wall.rect;
                var crop = Rect.MinMaxRect(
                    region.x + Mathf.Clamp01(wallBodyRegion.xMin) * region.width,
                    region.y + Mathf.Clamp01(wallBodyRegion.yMin) * region.height,
                    region.x + Mathf.Clamp01(wallBodyRegion.xMax) * region.width,
                    region.y + Mathf.Clamp01(wallBodyRegion.yMax) * region.height);
                if (crop.width < 1 || crop.height < 1)
                    return wall;
                _wallBody = Sprite.Create(wall.texture, crop, new Vector2(0.5f, 0.5f), wall.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                _wallBody.name = wall.name + " (wall body)";
                _wallBody.hideFlags = HideFlags.HideAndDontSave;
                _wallBodySource = wall;
                _wallBodySourceRegion = wallBodyRegion;
                return _wallBody;
            }
        }

        private void OnDisable() => ReleaseWallBody();

        private void ReleaseWallBody()
        {
            if (_wallBody == null)
                return;
            if (Application.isPlaying)
                Destroy(_wallBody);
            else
                DestroyImmediate(_wallBody);
            _wallBody = null;
        }

        public TerrainVisual Terrain(TerrainKind kind) => Array.Find(terrain, item => item.kind == kind);
        public Sprite Facility(string id) => Array.Find(facilities, item => item.definitionId == id)?.sprite;

    }
}
