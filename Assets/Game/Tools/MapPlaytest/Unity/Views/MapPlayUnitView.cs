using System.Collections.Generic;
using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.Definitions;
using Game.Battle.Domain.ValueObjects;
using Game.Maps.Unity.Views;
using UnityEngine;

namespace Game.Tools.MapPlaytest.Unity.Views
{
    /// <summary>只读取单位快照建立试玩 Sprite，不拥有单位生命或位置。</summary>
    public sealed class MapPlayUnitView : MonoBehaviour
    {
        private readonly Dictionary<ulong, SpriteRenderer> _units = new Dictionary<ulong, SpriteRenderer>();
        private Sprite _hero;
        private Sprite _enemy;

        public void Initialize(Sprite hero, Sprite enemy)
        {
            _hero = hero;
            _enemy = enemy;
        }

        public void Synchronize(IReadOnlyList<UnitSnapshot> units, BattleDefinition definition)
        {
            foreach (var unit in units)
            {
                if (!_units.TryGetValue(unit.Id, out var renderer))
                {
                    var go = new GameObject("Unit " + unit.Id);
                    go.transform.SetParent(transform, false);
                    renderer = go.AddComponent<SpriteRenderer>();
                    renderer.sprite = unit.Kind == UnitKind.Hero ? _hero : _enemy;
                    _units.Add(unit.Id, renderer);
                    if (renderer.sprite != null)
                        go.transform.localScale = Vector3.one * (definition.GetUnit(unit.Kind).VisualHeight / renderer.sprite.bounds.size.y);
                }
                renderer.transform.position = new Vector3(unit.X, unit.Y + 0.5f, 0);
                renderer.sortingOrder = MapWorldView.Sort(unit.Y);
                renderer.color = unit.Health > 0 ? Color.white : new Color(0.4f, 0.4f, 0.4f, 0.5f);
            }
        }

        public void Clear()
        {
            foreach (var unit in _units.Values)
            {
                unit.gameObject.SetActive(false);
                Destroy(unit.gameObject);
            }
            _units.Clear();
        }
    }
}
