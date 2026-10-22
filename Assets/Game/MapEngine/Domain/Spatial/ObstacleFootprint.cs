using System;
using Game.MapEngine.Domain.Geometry;

namespace Game.MapEngine.Domain.Spatial
{
    /// <summary>业务对象导出的不可变移动障碍，只包含矩形或有厚度线段的空间事实。</summary>
    public readonly struct ObstacleFootprint
    {
        private readonly MapPoint _a;
        private readonly MapPoint _b;
        private readonly float _thickness;
        private readonly bool _isSegment;

        private ObstacleFootprint(MapPoint a, MapPoint b, float thickness, bool isSegment)
        {
            _a = a;
            _b = b;
            _thickness = thickness;
            _isSegment = isSegment;
        }

        public static ObstacleFootprint Rectangle(MapPoint min, MapPoint max)
        {
            if (min.X >= max.X || min.Y >= max.Y)
                throw new ArgumentException("Obstacle rectangle must have positive size.");
            return new ObstacleFootprint(min, max, 0, false);
        }

        public static ObstacleFootprint Segment(MapPoint start, MapPoint end, float thickness)
        {
            if (float.IsNaN(thickness) || float.IsInfinity(thickness) || thickness <= 0)
                throw new ArgumentOutOfRangeException(nameof(thickness));
            return new ObstacleFootprint(start, end, thickness, true);
        }

        public bool BlocksCell(GridMap geometry, GridCell cell)
        {
            var center = geometry.CellCenter(cell);
            if (!_isSegment)
                return center.X >= _a.X && center.X < _b.X && center.Y >= _a.Y && center.Y < _b.Y;
            // 保留首版保守投影语义，不把导航格子当作精确连续碰撞轮廓。
            float cellRadius = Math.Max(geometry.CellWidth, geometry.CellHeight) * 0.5f;
            return SegmentGeometry.DistanceToSegment(center.X, center.Y, _a.X, _a.Y, _b.X, _b.Y) < cellRadius + _thickness * 0.5f;
        }
    }
}
