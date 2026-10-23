using System.Collections.Generic;
using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.Definitions;
using Game.Battle.Unity.Assets;
using Game.Battle.Unity.Views;
using UnityEngine;

namespace Game.Tools.MapPlaytest.Unity.Views
{
    /// <summary>试玩仅适配公共战斗表现，不再维护第二套静态单位和死亡机制。</summary>
    public sealed class MapPlayUnitView : MonoBehaviour
    {
        private BattleUnitPresentation _presentation;
        public void Initialize() => _presentation = new BattleUnitPresentation(transform, Resources.Load<UnitVisualCatalog>("UnitVisualCatalog"), Game.Maps.Unity.Views.MapWorldView.Sort);
        public void Synchronize(IReadOnlyList<UnitSnapshot> units, BattleDefinition definition, double time, float deltaTime, bool ended)
            => _presentation.Synchronize(units, definition, time, deltaTime, ended);
        public void Clear() => _presentation.Clear();
    }
}
