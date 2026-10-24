using System.Collections.Generic;
using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.ValueObjects;
using UnityEngine;

namespace Game.Tools.MapPlaytest.Unity.Views
{
    /// <summary>只读战斗快照：白圈展示真实选择范围，小白圈标记选中士兵。</summary>
    public sealed class SoldierSelectionView : MonoBehaviour
    {
        private Material _material;
        private LineRenderer _range;
        private readonly Dictionary<ulong, LineRenderer> _selected = new Dictionary<ulong, LineRenderer>();
        public void Synchronize(IReadOnlyList<UnitSnapshot> units, PlayerControlSnapshot controls, bool running)
        {
            if (_material == null) _material = new Material(Shader.Find("Sprites/Default"));
            if (_range == null) _range = CreateRing("Nearby selection radius", 0.045f);
            _range.enabled = false;
            foreach (var ring in _selected.Values) ring.enabled = false;
            foreach (var unit in units)
            {
                if (unit.Kind == UnitKind.Hero && unit.Health > 0 && running && controls.IsSelectingWhileMoving)
                    Draw(_range, unit.X, unit.Y, controls.SelectionRadius);
                if (!unit.IsSelected || unit.Health <= 0 || !running) continue;
                if (!_selected.TryGetValue(unit.Id, out var ring))
                {
                    ring = CreateRing("Selected soldier " + unit.Id, 0.035f);
                    _selected.Add(unit.Id, ring);
                }
                Draw(ring, unit.X, unit.Y, 0.35f);
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
        private static void Draw(LineRenderer line, float x, float y, float radius)
        {
            line.enabled = true;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2 / line.positionCount;
                line.SetPosition(i, new Vector3(x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius, -0.1f));
            }
        }
        public void Clear()
        {
            foreach (var line in _selected.Values) Destroy(line.gameObject);
            _selected.Clear();
            if (_range != null) _range.enabled = false;
        }
        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
