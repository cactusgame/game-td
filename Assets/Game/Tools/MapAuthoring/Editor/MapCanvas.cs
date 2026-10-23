using Game.MapEngine.Unity.Views;
using Game.Maps.Unity.Views;
using Game.Maps.Unity.Assets;
using System;
using System.Collections.Generic;
using Game.MapEngine.Domain;
using Game.Maps.Domain;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Tools.MapAuthoring.Editor
{
    /// <summary>地图画布只绘制文档、反馈鼠标意图；不保存地图权威状态。</summary>
    public sealed class MapCanvas : VisualElement
    {
        private MapLayout _layout;
        private readonly Dictionary<int, SurfaceMeshData[]> _surfaces = new Dictionary<int, SurfaceMeshData[]>();
        private List<SceneRenderEntry> _sprites;
        public MapLayout Layout
        {
            get => _layout;
            set
            {
                _layout = value;
                _surfaces.Clear();
                _sprites = null;
            }
        }
        public MapVisualCatalog Visuals;
        public MapContentCatalog Catalog;
        public bool ShowRules;
        public bool ShowGrid;
        public bool PanTool;
        private bool _spaceHeld;
        public Vector2 ViewPan => _pan;
        public string SelectedId;
        public GridCell? HighlightCell;
        public Action<Vector2, int> Pressed;
        public Action<Vector2> Dragged;
        public Action Released;
        public Action<Vector2> Hovered;
        private float _zoom = 1;
        private Vector2 _pan;
        private Vector2 _lastPointer;
        private bool _panning;
        private bool _dragging;
        private float _scale;
        private Vector2 _offset;

        public MapCanvas()
        {
            AddToClassList("map-canvas");
            focusable = true;
            generateVisualContent += Draw;
            RegisterCallback<PointerDownEvent>(evt =>
            {
                Focus();
                _lastPointer = evt.localPosition;
                _panning = evt.button == 2 || evt.button == 0 && (PanTool || _spaceHeld);
                _dragging = evt.button == 0 && !_panning;
                if (_panning || _dragging)
                    this.CapturePointer(evt.pointerId);
                if (_dragging)
                    Pressed?.Invoke(ToWorld(evt.localPosition), evt.button);
                evt.StopPropagation();
            });
            RegisterCallback<PointerMoveEvent>(evt =>
            {
                Vector2 point = evt.localPosition;
                if (_panning)
                {
                    _pan += point - _lastPointer;
                    MarkDirtyRepaint();
                }
                else if (_dragging)
                    Dragged?.Invoke(ToWorld(point));
                Hovered?.Invoke(ToWorld(point));
                _lastPointer = point;
            });
            RegisterCallback<PointerUpEvent>(evt =>
            {
                if (_dragging)
                    Released?.Invoke();
                _dragging = false;
                _panning = false;
                this.ReleasePointer(evt.pointerId);
            });
            RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                if (_dragging)
                    Released?.Invoke();
                _dragging = false;
                _panning = false;
            });
            RegisterCallback<WheelEvent>(evt =>
            {
                Vector2 before = ToWorld(evt.localMousePosition);
                _zoom = Mathf.Clamp(_zoom * Mathf.Pow(1.12f, -evt.delta.y), 0.25f, 20);
                UpdateMapping();
                Vector2 after = ToScreen(before.x, before.y);
                _pan += (Vector2)evt.localMousePosition - after;
                MarkDirtyRepaint();
                evt.StopPropagation();
            });
            RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Space)
                {
                    _spaceHeld = true;
                    evt.StopPropagation();
                }
            });
            RegisterCallback<KeyUpEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Space)
                {
                    _spaceHeld = false;
                    evt.StopPropagation();
                }
            });
            RegisterCallback<FocusOutEvent>(_ => _spaceHeld = false);
        }

        public void Fit()
        {
            _zoom = 1;
            _pan = Vector2.zero;
            MarkDirtyRepaint();
        }

        public void Locate(GridCell cell)
        {
            HighlightCell = cell;
            if (Layout != null && Layout.Geometry.Contains(cell))
            {
                var point = Layout.Geometry.CellCenter(cell);
                LocatePoint(point.X, point.Y);
            }
            MarkDirtyRepaint();
        }

        public void LocatePoint(float x, float y)
        {
            UpdateMapping();
            _pan = new Vector2((Layout.columns * Layout.cellWidth * 0.5f - x + Layout.originX) * _scale,
                (y - Layout.originY - Layout.rows * Layout.cellHeight * 0.5f) * _scale);
            MarkDirtyRepaint();
        }

        private void UpdateMapping()
        {
            if (Layout == null)
                return;
            _scale = Mathf.Max(0.001f, Mathf.Min((contentRect.width - 36) / (Layout.columns * Layout.cellWidth), (contentRect.height - 36) / (Layout.rows * Layout.cellHeight))) * _zoom;
            _offset = new Vector2((contentRect.width - Layout.columns * Layout.cellWidth * _scale) * 0.5f,
                (contentRect.height - Layout.rows * Layout.cellHeight * _scale) * 0.5f) + _pan;
        }

        private Vector2 ToWorld(Vector2 point)
        {
            UpdateMapping();
            if (Layout == null)
                return Vector2.zero;
            return new Vector2(Layout.originX + (point.x - _offset.x) / _scale,
                Layout.originY + Layout.rows * Layout.cellHeight - (point.y - _offset.y) / _scale);
        }

        private Vector2 ToScreen(float x, float y)
        {
            return _offset + new Vector2((x - Layout.originX) * _scale, (Layout.originY + Layout.rows * Layout.cellHeight - y) * _scale);
        }

        private Rect CellRect(int x, int y, int width = 1, int height = 1)
        {
            var top = ToScreen(Layout.originX + x * Layout.cellWidth, Layout.originY + (y + height) * Layout.cellHeight);
            return new Rect(top.x, top.y, Layout.cellWidth * width * _scale, Layout.cellHeight * height * _scale);
        }

        private void Draw(MeshGenerationContext ctx)
        {
            if (Layout == null || Visuals == null || contentRect.width < 1 || contentRect.height < 1)
                return;
            UpdateMapping();
            var geometry = Layout.Geometry;
            var navigation = ShowRules ? MapSpatialRules.CreateNavigationMap(Layout, Catalog) : null;
            for (int i = 0; i < Layout.terrain.Length; i++)
            {
                var cell = geometry.CellAt(i);
                var rect = CellRect(cell.X, cell.Y);
                if (!rect.Overlaps(contentRect))
                    continue;
                if (Visuals.blendTerrain)
                {
                    if (!_surfaces.TryGetValue(i, out var patches))
                    {
                        patches = new SurfaceMeshData[4];
                        for (int layer = 0; layer < patches.Length; layer++)
                            patches[layer] = MapSurfaceGeometry.Build(Layout, Visuals, cell.X, cell.Y, layer);
                        _surfaces.Add(i, patches);
                    }
                    foreach (var patch in patches)
                    {
                        if (patch != null)
                            DrawSurface(ctx, patch);
                    }
                }
                else
                    Quad(ctx, rect, Visuals.Terrain(Layout.terrain[i])?.ground);
                if (ShowRules)
                    Rectangle(ctx.painter2D, rect, navigation.IsWalkable(cell) ? new Color(0.1f, 0.8f, 0.4f, 0.18f) : new Color(1, 0.15f, 0.1f, 0.4f), ShowGrid ? new Color(0, 0, 0, 0.25f) : Color.clear);
                else if (ShowGrid && rect.width >= 8)
                    Rectangle(ctx.painter2D, rect, Color.clear, new Color(0, 0, 0, 0.18f));
            }
            _sprites ??= MapSceneSprites.Build(Layout, Visuals, Catalog);
            foreach (var entry in _sprites)
            {
                if (entry.Wall != null)
                {
                    DrawSurface(ctx, entry.Wall.Mesh, true);
                    continue;
                }
                var center = ToScreen(entry.Center.x, entry.Center.y);
                var rect = new Rect(center.x - entry.Width * _scale * 0.5f, center.y - entry.Height * _scale * 0.5f, entry.Width * _scale, entry.Height * _scale);
                if (rect.Overlaps(contentRect))
                    Quad(ctx, rect, entry.Sprite, -entry.Angle);
            }
            foreach (var facility in Layout.facilities)
            {
                if (facility.id != SelectedId && !ShowRules)
                    continue;
                var definition = Catalog.Find(facility.definitionId);
                Rectangle(ctx.painter2D, CellRect(facility.x, facility.y, definition?.width ?? 1, definition?.height ?? 1), Color.clear,
                    facility.id == SelectedId ? Color.yellow : new Color(0.2f, 0.7f, 1));
            }
            foreach (var node in Layout.wallNodes)
                if (node.id == SelectedId || ShowGrid) Circle(ctx.painter2D, ToScreen(node.x, node.y), node.id == SelectedId ? Color.yellow : Color.white, 4);
            foreach (var edge in Layout.wallEdges)
            {
                if (edge.id != SelectedId || !edge.hasGate || !WallGateRules.Validate(Layout, edge, out _))
                    continue;
                var gate = WallGateRules.Center(Layout, edge);
                Circle(ctx.painter2D, ToScreen(gate.X, gate.Y), Color.yellow, 6);
            }
            foreach (var marker in Layout.markers)
                Circle(ctx.painter2D, ToScreen(marker.x, marker.y), marker.id == SelectedId ? Color.yellow : marker.kind == MarkerKind.HeroSpawn ? Color.cyan : new Color(1, 0.35f, 0.2f), 8);
            if (HighlightCell.HasValue)
                Rectangle(ctx.painter2D, CellRect(HighlightCell.Value.X, HighlightCell.Value.Y), new Color(1, 1, 0, 0.2f), Color.yellow);
        }

        private void DrawSurface(MeshGenerationContext context, SurfaceMeshData patch, bool reverseWinding = false)
        {
            var mesh = context.Allocate(patch.Positions.Length, patch.Indices.Length, patch.Sprite.texture);
            for (int i = 0; i < patch.Positions.Length; i++)
            {
                var screen = ToScreen(patch.Positions[i].x, patch.Positions[i].y);
                mesh.SetNextVertex(new Vertex { position = new Vector3(screen.x, screen.y, Vertex.nearZ), uv = patch.Uvs[i], tint = patch.Colors[i] });
            }
            for (int i = 0; i < patch.Indices.Length; i += 3)
            {
                mesh.SetNextIndex((ushort)patch.Indices[i]);
                mesh.SetNextIndex((ushort)patch.Indices[i + (reverseWinding ? 2 : 1)]);
                mesh.SetNextIndex((ushort)patch.Indices[i + (reverseWinding ? 1 : 2)]);
            }
        }


        private static void Circle(Painter2D painter, Vector2 center, Color color, float radius)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.Arc(center, radius, Angle.Degrees(0), Angle.Degrees(360));
            painter.Fill();
        }

        private static void Rectangle(Painter2D painter, Rect rect, Color fill, Color stroke)
        {
            painter.fillColor = fill;
            painter.strokeColor = stroke;
            painter.lineWidth = 1;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            if (fill.a > 0)
                painter.Fill();
            painter.Stroke();
        }

        private static void Quad(MeshGenerationContext ctx, Rect rect, Sprite sprite, float angle = 0)
        {
            if (sprite == null)
                return;
            var texture = sprite.texture;
            var source = sprite.rect;
            var mesh = ctx.Allocate(4, 6, texture);
            // 当前 UI Toolkit 会自动映射动态图集坐标，使用原纹理的归一化 UV。
            float u0 = source.xMin / texture.width;
            float u1 = source.xMax / texture.width;
            float v0 = source.yMin / texture.height;
            float v1 = source.yMax / texture.height;
            mesh.SetNextVertex(new Vertex { position = Rotate(rect.min, rect.center, angle), uv = new Vector2(u0, v1), tint = Color.white });
            mesh.SetNextVertex(new Vertex { position = Rotate(new Vector2(rect.xMax, rect.yMin), rect.center, angle), uv = new Vector2(u1, v1), tint = Color.white });
            mesh.SetNextVertex(new Vertex { position = Rotate(rect.max, rect.center, angle), uv = new Vector2(u1, v0), tint = Color.white });
            mesh.SetNextVertex(new Vertex { position = Rotate(new Vector2(rect.xMin, rect.yMax), rect.center, angle), uv = new Vector2(u0, v0), tint = Color.white });
            mesh.SetNextIndex(0);
            mesh.SetNextIndex(1);
            mesh.SetNextIndex(2);
            mesh.SetNextIndex(2);
            mesh.SetNextIndex(3);
            mesh.SetNextIndex(0);
        }

        private static Vector3 Rotate(Vector2 point, Vector2 center, float angle)
        {
            Vector2 delta = point - center;
            return new Vector3(center.x + delta.x * Mathf.Cos(angle) - delta.y * Mathf.Sin(angle),
                center.y + delta.x * Mathf.Sin(angle) + delta.y * Mathf.Cos(angle), Vertex.nearZ);
        }
    }
}
