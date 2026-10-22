using System;

namespace Game.MapEngine.Domain.Geometry
{
    /// <summary>连续地面上的线段计算，不认识建筑、墙网、关卡或 Unity 对象。</summary>
    public static class SegmentGeometry
    {
        public static float DistanceToSegment(float x, float y, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax;
            float dy = by - ay;
            float lengthSquared = dx * dx + dy * dy;
            float t = lengthSquared > 0 ? Math.Max(0, Math.Min(1, ((x - ax) * dx + (y - ay) * dy) / lengthSquared)) : 0;
            dx = x - ax - t * dx;
            dy = y - ay - t * dy;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public static bool Intersects(float ax, float ay, float bx, float by, float cx, float cy, float dx, float dy)
        {
            float abC = Cross(bx - ax, by - ay, cx - ax, cy - ay);
            float abD = Cross(bx - ax, by - ay, dx - ax, dy - ay);
            float cdA = Cross(dx - cx, dy - cy, ax - cx, ay - cy);
            float cdB = Cross(dx - cx, dy - cy, bx - cx, by - cy);
            const float tolerance = 0.0001f;
            if (Math.Abs(abC) < tolerance && Math.Abs(abD) < tolerance)
                return Math.Max(Math.Min(ax, bx), Math.Min(cx, dx)) <= Math.Min(Math.Max(ax, bx), Math.Max(cx, dx)) + tolerance &&
                    Math.Max(Math.Min(ay, by), Math.Min(cy, dy)) <= Math.Min(Math.Max(ay, by), Math.Max(cy, dy)) + tolerance;
            return abC * abD <= 0 && cdA * cdB <= 0;
        }

        private static float Cross(float ax, float ay, float bx, float by) => ax * by - ay * bx;
    }
}
