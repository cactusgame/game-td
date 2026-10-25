using Game.Battle.Contracts.Ports;
using Game.Battle.Simulation.Components;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Bridges
{
    public sealed partial class EcsBattleSimulation : IStructureHealthOperations
    {
        public bool DamageStructure(string id, int amount) => ChangeStructureHealth(id, amount, false);
        public bool RepairStructure(string id, int amount) => ChangeStructureHealth(id, amount, true);

        private bool ChangeStructureHealth(string id, int amount, bool repair)
        {
            if (amount <= 0 || !_structures.TryGetValue(id, out var entity))
                return false;
            var em = _world.EntityManager;
            em.CompleteAllTrackedJobs();
            var structure = em.GetComponentData<DefenseStructure>(entity);
            if (structure.Health <= 0)
                return false;
            structure.Health = repair
                ? structure.Health + math.min(amount, structure.MaxHealth - structure.Health)
                : math.max(0, structure.Health - amount);
            em.SetComponentData(entity, structure);
            return true;
        }
    }
}
