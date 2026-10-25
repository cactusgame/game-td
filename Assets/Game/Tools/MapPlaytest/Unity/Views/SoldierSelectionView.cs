using System.Collections.Generic;
using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.ValueObjects;
using UnityEngine;

namespace Game.Tools.MapPlaytest.Unity.Views
{
    /// <summary>只读战斗快照：同尺寸白色椭圆以实线展示选择、虚线展示死守，不参与 AI 决策。</summary>
    public sealed class SoldierSelectionView : MonoBehaviour
    {
        private const float MarkerRadiusX = 0.4f;
        private const float MarkerRadiusY = 0.2f;
        private Material _material;
        private LineRenderer _range;
        private readonly Dictionary<ulong, LineRenderer> _selected = new Dictionary<ulong, LineRenderer>();
        private readonly Dictionary<ulong, LineRenderer[]> _stationed = new Dictionary<ulong, LineRenderer[]>();
        public void Synchronize(IReadOnlyList<UnitSnapshot> units, PlayerControlSnapshot controls, bool running)
        {
            if (_material == null) _material = new Material(Shader.Find("Sprites/Default"));
            if (_range == null) _range = CreateRing("Nearby selection radius", 0.045f);
            _range.enabled = false;
            foreach (var ring in _selected.Values) ring.enabled = false;
            foreach (var dashes in _stationed.Values)
                foreach (var dash in dashes) dash.enabled = false;
            foreach (var unit in units)
            {
                if (unit.Kind == UnitKind.Hero && unit.Health > 0 && running && controls.IsSelectingWhileMoving)
                    Draw(_range, unit.X, unit.Y, controls.SelectionRadius, controls.SelectionRadius);
                if (unit.Health <= 0 || !running) continue;
                if (unit.IsStationed)
                    DrawStationed(unit);
                if (!unit.IsSelected) continue;
                if (!_selected.TryGetValue(unit.Id, out var ring))
                {
                    ring = CreateRing("Selected soldier " + unit.Id, 0.035f);
                    _selected.Add(unit.Id, ring);
                }
                Draw(ring, unit.X, unit.Y, MarkerRadiusX, MarkerRadiusY);
            }
        }
        private void DrawStationed(UnitSnapshot unit)
        {
            const int dashCount = 12;
            if (!_stationed.TryGetValue(unit.Id, out var dashes))
            {
                dashes = new LineRenderer[dashCount];
                for (int i = 0; i < dashCount; i++)
                {
                    var dash = CreateRing("Hold ground " + unit.Id + " dash " + i, 0.035f);
                    dash.loop = false;
                    dash.positionCount = 5;
                    dashes[i] = dash;
                }
                _stationed.Add(unit.Id, dashes);
            }
            for (int i = 0; i < dashCount; i++)
            {
                var dash = dashes[i];
                dash.enabled = true;
                for (int point = 0; point < dash.positionCount; point++)
                {
                    float angle = (i + 0.6f * point / (dash.positionCount - 1)) * Mathf.PI * 2 / dashCount;
                    dash.SetPosition(point, new Vector3(unit.X + Mathf.Cos(angle) * MarkerRadiusX,
                        unit.Y + Mathf.Sin(angle) * MarkerRadiusY, -0.1f));
                }
            }
        }
        private LineRenderer CreateRing(string label, float width)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = _material;
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = 96;
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = Color.white;
            line.sortingOrder = 10000;
            return line;
        }
        private static void Draw(LineRenderer line, float x, float y, float radiusX, float radiusY)
        {
            line.enabled = true;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2 / line.positionCount;
                line.SetPosition(i, new Vector3(x + Mathf.Cos(angle) * radiusX, y + Mathf.Sin(angle) * radiusY, -0.1f));
            }
        }
        public void Clear()
        {
            foreach (var line in _selected.Values) Destroy(line.gameObject);
            _selected.Clear();
            foreach (var dashes in _stationed.Values)
                foreach (var dash in dashes) Destroy(dash.gameObject);
            _stationed.Clear();
            if (_range != null) _range.enabled = false;
        }
        private void OnDestroy()
        {
            if (_material == null) return;
            if (UnityEngine.Application.isPlaying) Destroy(_material);
            else DestroyImmediate(_material);
        }
    }
}
