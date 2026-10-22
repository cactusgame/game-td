using Game.Maps.Unity.Assets;
using System;
using Game.MapEngine.Domain;
using Game.Maps.Domain;
using UnityEngine;

namespace Game.Maps.Unity.Views
{
    /// <summary>
    /// 编辑器与游戏共用的地表几何。相邻格在同一连续密度场取样，边界顶点相同，
    /// 草地连续铺底，土路和河岸以柔和遮罩覆盖；图片只提供材质，不再携带方形边框。
    /// </summary>
    public static class MapSurfaceGeometry
    {
        public sealed class Patch
        {
            public Sprite Sprite;
            public Vector2[] Positions;
            public Vector2[] Uvs;
            public Color32[] Colors;
            public int[] Indices;
        }

        public static Patch Build(MapLayout layout, MapVisualCatalog visuals, int x, int y, int layer, int resolution = 8)
        {
            var kind = layer == 3 ? TerrainKind.Water : layer == 0 ? TerrainKind.Grass : TerrainKind.Dirt;
            var sprite = visuals.Terrain(kind)?.ground;
            if (sprite == null || layer > 0 && !HasNeighbour(layout, x, y, layer >= 2))
                return null;
            int steps = layer == 0 ? 1 : resolution;
            int stride = steps + 1;
            var patch = new Patch
            {
                Sprite = sprite,
                Positions = new Vector2[stride * stride],
                Uvs = new Vector2[stride * stride],
                Colors = new Color32[stride * stride],
                Indices = new int[steps * steps * 6]
            };
            for (int row = 0; row <= steps; row++)
            {
                for (int column = 0; column <= steps; column++)
                {
                    float u = (float)column / steps;
                    float v = (float)row / steps;
                    int index = row * stride + column;
                    float worldX = layout.originX + (x + u) * layout.cellWidth;
                    float worldY = layout.originY + (y + v) * layout.cellHeight;
                    patch.Positions[index] = new Vector2(worldX, worldY);
                    patch.Uvs[index] = TextureUv(sprite, (x + u) / 4, (y + v) / 4);
                    float alpha = layer == 0 ? 1 : Coverage(layout, x + u, y + v, layer);
                    float tint = layer == 0 ? 0.94f + Mathf.Sin(worldX * 0.21f + Mathf.Sin(worldY * 0.29f)) * 0.045f : 1;
                    patch.Colors[index] = new Color(tint, tint, tint, alpha);
                }
            }
            int next = 0;
            for (int row = 0; row < steps; row++)
            {
                for (int column = 0; column < steps; column++)
                {
                    int a = row * stride + column;
                    patch.Indices[next++] = a;
                    patch.Indices[next++] = a + stride + 1;
                    patch.Indices[next++] = a + 1;
                    patch.Indices[next++] = a;
                    patch.Indices[next++] = a + stride;
                    patch.Indices[next++] = a + stride + 1;
                }
            }
            return patch;
        }

        public static float Coverage(MapLayout layout, float gridX, float gridY, int layer)
        {
            float density = Density(layout, gridX, gridY, layer >= 2);
            if (density > 0 && density < 1)
                density += Mathf.Sin(gridX * 1.73f + gridY * 0.87f) * 0.025f + Mathf.Sin(gridX * 0.41f - gridY * 2.03f) * 0.018f;
            float low = layer == 2 ? 0.24f : 0.45f;
            float high = layer == 2 ? 0.44f : 0.64f;
            return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(low, high, density));
        }

        private static float Density(MapLayout layout, float x, float y, bool water)
        {
            x -= 0.5f;
            y -= 0.5f;
            int left = Mathf.FloorToInt(x);
            int bottom = Mathf.FloorToInt(y);
            float tx = Mathf.SmoothStep(0, 1, x - left);
            float ty = Mathf.SmoothStep(0, 1, y - bottom);
            float lower = Mathf.Lerp(Value(layout, left, bottom, water), Value(layout, left + 1, bottom, water), tx);
            float upper = Mathf.Lerp(Value(layout, left, bottom + 1, water), Value(layout, left + 1, bottom + 1, water), tx);
            return Mathf.Lerp(lower, upper, ty);
        }

        private static float Value(MapLayout layout, int x, int y, bool water)
        {
            x = Mathf.Clamp(x, 0, layout.columns - 1);
            y = Mathf.Clamp(y, 0, layout.rows - 1);
            var kind = layout.terrain[y * layout.columns + x];
            if (water)
                return kind == TerrainKind.Water ? 1 : 0;
            return kind == TerrainKind.Dirt ? 1.12f : kind == TerrainKind.Road ? 1 : 0;
        }

        private static bool HasNeighbour(MapLayout layout, int x, int y, bool water)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (Value(layout, x + dx, y + dy, water) > 0)
                        return true;
            return false;
        }

        public static Vector2 TextureUv(Sprite sprite, float x, float y)
        {
            // 镜像连续采样保证材质重复边缘像素一致，并隔离图集相邻素材的颜色。
            x = Mathf.PingPong(x, 1);
            y = Mathf.PingPong(y, 1);
            var rect = sprite.rect;
            return new Vector2((rect.x + 0.5f + x * (rect.width - 1)) / sprite.texture.width,
                (rect.y + 0.5f + y * (rect.height - 1)) / sprite.texture.height);
        }
    }
}
