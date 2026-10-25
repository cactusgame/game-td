using System;
using Game.Encounter.Domain.Definitions;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Tools.MapAuthoring.Editor
{
    /// <summary>每个出怪组单独一行；位置表示起始延迟，长度表示首只到末只的生成时间。</summary>
    internal sealed class SpawnTimeline : VisualElement
    {
        public SpawnTimeline(SpawnGroupLayout group, double duration)
        {
            AddToClassList("spawn-timeline");
            generateVisualContent += context =>
            {
                if (contentRect.width < 1 || group.delaySeconds < 0 || group.intervalSeconds < 0) return;
                double scale = Math.Max(1, duration);
                float left = (float)(group.delaySeconds / scale) * (contentRect.width - 6) + 3;
                float right = (float)(group.EndSeconds / scale) * (contentRect.width - 6) + 3;
                var painter = context.painter2D;
                painter.strokeColor = new Color(0.24f, 0.34f, 0.39f);
                painter.lineWidth = 2;
                painter.BeginPath(); painter.MoveTo(new Vector2(3, 10)); painter.LineTo(new Vector2(contentRect.width - 3, 10)); painter.Stroke();
                painter.strokeColor = new Color(0.44f, 0.82f, 0.67f);
                painter.lineWidth = 8;
                painter.BeginPath(); painter.MoveTo(new Vector2(left, 10)); painter.LineTo(new Vector2(Math.Max(left + 1, right), 10)); painter.Stroke();
            };
        }
    }
}
