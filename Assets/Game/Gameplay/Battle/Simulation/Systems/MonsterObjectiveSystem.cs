using System.Collections.Generic;
using Game.Battle.Simulation.Components;
using Game.MapEngine.Domain;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Systems
{
    /// <summary>敌人行为选择防守目标和可达攻击位置；导航系统只负责到达该位置。</summary>
    [DisableAutoCreation]
    [UpdateBefore(typeof(MonsterNavigationSystem))]
    public partial class MonsterObjectiveSystem : SystemBase
    {
        private GridMap _map;
        private GridPathfinder _pathfinder;
        private readonly List<GridCell> _pathCells = new List<GridCell>();
        public void Configure(GridMap map)
        {
            _map = map;
            _pathfinder = new GridPathfinder(map);
        }
        protected override void OnUpdate()
        {
            if (_map == null)
                return;
            using var query = EntityManager.CreateEntityQuery(typeof(DefenseStructure));
            using var structures = query.ToComponentDataArray<DefenseStructure>(Allocator.Temp);
            using var entities = query.ToEntityArray(Allocator.Temp);
            bool hasCastle = false;
            foreach (var structure in structures)
                hasCastle |= structure.IsMainCastle != 0;
            foreach (var (ground, motion, vitals, navigation, profile) in SystemAPI.Query<RefRO<GroundTransform>, RefRO<UnitMotion>, RefRO<Vitals>, RefRW<MonsterNavigationState>, RefRO<CombatProfile>>().WithAll<MonsterTag>())
            {
                bool wantsCastle = hasCastle && profile.ValueRO.TargetMainCastle != 0;
                navigation.ValueRW.HasCastleObjective = wantsCastle ? (byte)1 : (byte)0;
                if (!wantsCastle || vitals.ValueRO.Health <= 0 || SystemAPI.Time.ElapsedTime < navigation.ValueRO.NextRepathTime)
                    continue;
                Entity previous = navigation.ValueRO.StructureTarget;
                bool kept = TrySelectCastleGoal(ground.ValueRO.Position, motion.ValueRO.Radius, profile.ValueRO.AttackRange,
                    structures, entities, previous, out var target, out var position);
                if (!kept && previous != Entity.Null)
                    TrySelectCastleGoal(ground.ValueRO.Position, motion.ValueRO.Radius, profile.ValueRO.AttackRange,
                        structures, entities, Entity.Null, out target, out position);
                navigation.ValueRW.StructureTarget = target;
                navigation.ValueRW.StructureGoal = position;
            }
        }
        private bool TrySelectCastleGoal(float2 position, float radius, float attackRange, NativeArray<DefenseStructure> structures,
            NativeArray<Entity> entities, Entity preferred, out Entity target, out float2 destination)
        {
            target = Entity.Null;
            destination = position;
            float nearest = float.MaxValue;
            if (!_map.TryWorldToCell(position.x, position.y, out var start))
                return false;
            for (int i = 0; i < structures.Length; i++)
            {
                var castle = structures[i];
                if (preferred != Entity.Null && entities[i] != preferred) continue;
                if (castle.IsMainCastle == 0 || castle.Health <= 0)
                    continue;
                for (int cellIndex = 0; cellIndex < _map.CellCount; cellIndex++)
                {
                    var cell = _map.CellAt(cellIndex);
                    var point = _map.CellCenter(cell);
                    var candidate = new float2(point.X, point.Y);
                    float2 closest = math.clamp(candidate, castle.Min, castle.Max);
                    float edgeDistance = math.distance(candidate, closest);
                    if (edgeDistance > radius + math.max(_map.CellWidth, _map.CellHeight) || edgeDistance <= 0)
                        continue;
                    candidate = closest + math.normalizesafe(candidate - closest) * (radius + math.min(0.05f, attackRange * 0.5f));
                    if (!_map.TryWorldToCell(candidate.x, candidate.y, out cell) || !_map.CanOccupy(candidate.x, candidate.y, radius))
                        continue;
                    float distance = math.distancesq(position, candidate);
                    if (distance >= nearest || !_pathfinder.TryFindPath(start, cell, _pathCells))
                        continue;
                    nearest = distance;
                    target = entities[i];
                    destination = candidate;
                }
            }
            return target != Entity.Null;
        }
    }
}
