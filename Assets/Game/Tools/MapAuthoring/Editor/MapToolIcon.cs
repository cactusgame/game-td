using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Tools.MapAuthoring.Editor
{
    /// <summary>工具图标使用矢量路径，标记颜色与地图上的出生点、入口一致。</summary>
    public sealed class MapToolIcon : VisualElement
    {
        private readonly MapEditorTool _tool;
        public MapToolIcon(MapEditorTool tool)
        {
            _tool = tool;
            pickingMode = PickingMode.Ignore;
            AddToClassList("palette-thumbnail");
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            Vector2 center = contentRect.center;
            if (_tool == MapEditorTool.Pan)
            {
                painter.strokeColor = new Color(0.75f, 0.92f, 1);
                painter.lineWidth = 3;
                painter.BeginPath();
                painter.MoveTo(center + new Vector2(-16, 0));
                painter.LineTo(center + new Vector2(16, 0));
                painter.MoveTo(center + new Vector2(0, -16));
                painter.LineTo(center + new Vector2(0, 16));
                painter.MoveTo(center + new Vector2(-10, -6));
                painter.LineTo(center + new Vector2(-16, 0));
                painter.LineTo(center + new Vector2(-10, 6));
                painter.MoveTo(center + new Vector2(10, -6));
                painter.LineTo(center + new Vector2(16, 0));
                painter.LineTo(center + new Vector2(10, 6));
                painter.Stroke();
                return;
            }
            if (_tool == MapEditorTool.Select)
            {
                painter.fillColor = new Color(0.85f, 0.94f, 1);
                painter.BeginPath();
                painter.MoveTo(center + new Vector2(-12, -20));
                painter.LineTo(center + new Vector2(14, 2));
                painter.LineTo(center + new Vector2(3, 5));
                painter.LineTo(center + new Vector2(-2, 18));
                painter.LineTo(center + new Vector2(-8, 15));
                painter.LineTo(center + new Vector2(-3, 3));
                painter.LineTo(center + new Vector2(-14, 8));
                painter.ClosePath();
                painter.Fill();
                return;
            }
            Color color = _tool == MapEditorTool.HeroSpawn ? new Color(0.1f, 0.85f, 0.95f) : new Color(1, 0.42f, 0.2f);
            painter.strokeColor = color;
            painter.lineWidth = 3;
            painter.BeginPath();
            painter.Arc(center, 17, Angle.Degrees(0), Angle.Degrees(360));
            painter.Stroke();
            painter.fillColor = color;
            painter.BeginPath();
            if (_tool == MapEditorTool.HeroSpawn)
            {
                painter.Arc(center + new Vector2(0, -5), 6, Angle.Degrees(0), Angle.Degrees(360));
                painter.Fill();
                painter.BeginPath();
                painter.MoveTo(center + new Vector2(-9, 10));
                painter.LineTo(center + new Vector2(0, -1));
                painter.LineTo(center + new Vector2(9, 10));
            }
            else
            {
                painter.MoveTo(center + new Vector2(-10, -5));
                painter.LineTo(center + new Vector2(2, -5));
                painter.LineTo(center + new Vector2(2, -11));
                painter.LineTo(center + new Vector2(12, 0));
                painter.LineTo(center + new Vector2(2, 11));
                painter.LineTo(center + new Vector2(2, 5));
                painter.LineTo(center + new Vector2(-10, 5));
            }
            painter.ClosePath();
            painter.Fill();
        }
    }
}
