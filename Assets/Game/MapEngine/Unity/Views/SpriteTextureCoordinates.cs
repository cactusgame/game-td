using UnityEngine;

namespace Game.MapEngine.Unity.Views
{
    public static class SpriteTextureCoordinates
    {
        public static Vector2 Sample(Sprite sprite, float x, float y)
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
