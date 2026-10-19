using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Presentation.Views
{
    /// <summary>纯视觉地面与边框网格构建器，编辑器预览和运行时重建共用。</summary>
    public static class BattleArenaMesh
    {
        public static void Build(Mesh mesh, float width, float height, BattleMapViewSettings settings)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            void Quad(float x, float y, float w, float h, Color color)
            {
                int index = vertices.Count;
                vertices.Add(new Vector3(x, y));
                vertices.Add(new Vector3(x + w, y));
                vertices.Add(new Vector3(x + w, y + h));
                vertices.Add(new Vector3(x, y + h));
                for (int i = 0; i < 4; i++)
                    colors.Add(color);
                triangles.Add(index);
                triangles.Add(index + 2);
                triangles.Add(index + 1);
                triangles.Add(index);
                triangles.Add(index + 3);
                triangles.Add(index + 2);
            }

            float size = Mathf.Max(0.1f, settings.GroundPatternSize);
            float left = -width * 0.5f;
            float bottom = -height * 0.5f;
            int columns = Mathf.CeilToInt(width / size);
            int rows = Mathf.CeilToInt(height / size);
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    Color color = (x + y) % 2 == 0 ? settings.GroundDark : settings.GroundLight;
                    Quad(left + x * size, bottom + y * size, Mathf.Min(size, width - x * size), Mathf.Min(size, height - y * size), color);
                }
            }

            // 边框位于地图外侧，不改变 ECS 的可移动范围，也不是障碍或 Tilemap。
            float border = Mathf.Max(0.01f, settings.BorderThickness);
            Quad(left - border, bottom - border, width + border * 2, border, settings.BorderColor);
            Quad(left - border, -bottom, width + border * 2, border, settings.BorderColor);
            Quad(left - border, bottom, border, height, settings.BorderColor);
            Quad(-left, bottom, border, height, settings.BorderColor);
            mesh.Clear();
            mesh.indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }
    }
}
