using System;
using System.Linq;
using Game.Battle.Contracts.Ports;

namespace Game.Tools.MapPlaytest.Application.Sessions
{
    public sealed partial class MapPlaySession
    {
        public int GetWallHealth(string id) => _gates.WallHealth(id);

        /// <summary>试玩伤害/修复用例。UI 只提交目标与比例，权威状态由模拟或防御协调器修改。</summary>
        public bool TryChangeStructureHealth(string id, bool repair, float fraction, out string reason)
        {
            RequireActive();
            reason = null;
            if (!CanChangeConstruction(out reason))
                return false;
            if (float.IsNaN(fraction) || fraction <= 0 || fraction > 1)
            {
                reason = "生命变化比例无效";
                return false;
            }
            var layout = _placements.Snapshot;
            var building = layout.facilities.Find(item => item.id == id);
            bool changed;
            if (building != null)
            {
                int maximum = _catalog.Find(building.definitionId).maxHealth + Development(id).HealthBonus;
                int amount = Math.Max(1, (int)Math.Round(maximum * (double)fraction, MidpointRounding.AwayFromZero));
                var operations = _simulation as IStructureHealthOperations;
                changed = operations != null && (repair ? operations.RepairStructure(id, amount) : operations.DamageStructure(id, amount));
                ReadStructures();
            }
            else
            {
                bool wall = layout.wallEdges.Any(edge => edge.id == id);
                bool gate = layout.wallEdges.SelectMany(edge => edge.gates).Any(item => item.id == id);
                if (!wall && !gate)
                {
                    reason = "建筑已不存在";
                    return false;
                }
                int maximum = wall ? _catalog.Wall.WallHealth : _catalog.Wall.GateHealth;
                changed = _gates.ChangeHealth((IDefenseBattleSimulation)_simulation, layout, Definition,
                    id, wall, Math.Max(1, (int)Math.Round(maximum * (double)fraction, MidpointRounding.AwayFromZero)), repair);
                _enemyNavigation = _gates.Navigation(layout, false);
            }
            if (!changed)
                reason = "损毁建筑不能修复，或当前已是满血";
            return changed;
        }
    }
}
