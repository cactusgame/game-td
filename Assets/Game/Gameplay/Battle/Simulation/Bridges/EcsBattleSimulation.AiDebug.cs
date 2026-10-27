using System;
using System.Collections.Generic;
using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.ValueObjects;
using Game.Battle.Simulation.Components;
using Game.MapEngine.Domain;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Bridges
{
    public sealed partial class EcsBattleSimulation
    {
        // 仅为诊断记录本步外部目标，玩法冷却和目标裁决均不读取此缓存。
        private readonly Dictionary<ulong, (string Label, MapPoint Position, double Time)> _externalDebugTargets =
            new Dictionary<ulong, (string, MapPoint, double)>();

        public void CopyAiDebug(List<UnitAiDebugSnapshot> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            var em = _world.EntityManager;
            em.CompleteAllTrackedJobs();
            destination.Clear();
            using var entities = _units.ToEntityArray(Allocator.Temp);
            Entity hero = _unitEntities.TryGetValue(1, out var heroEntity) ? heroEntity : Entity.Null;
            foreach (var entity in entities)
            {
                var identity = em.GetComponentData<UnitIdentity>(entity);
                var ground = em.GetComponentData<GroundTransform>(entity);
                var vital = em.GetComponentData<Vitals>(entity);
                var navigation = em.GetComponentData<MonsterNavigationState>(entity);
                var status = em.GetComponentData<UnitStatusState>(entity);
                bool soldier = em.HasComponent<SoldierState>(entity);
                var state = soldier ? em.GetComponentData<SoldierState>(entity) : default;
                var behavior = soldier ? state.Behavior : navigation.Behavior;
                var stop = navigation.StopReason;
                bool ghost = identity.Kind == UnitKind.Hero && vital.Health <= 0;
                if (vital.Health <= 0 && !ghost) { behavior = UnitBehavior.Dead; stop = MovementStopReason.Dead; }
                else if (vital.Health > 0 && status.IsStunnedAt(ElapsedTime)) { behavior = UnitBehavior.Idle; stop = MovementStopReason.Stunned; }
                else if (vital.Health > 0 && status.IsRootedAt(ElapsedTime))
                {
                    stop = MovementStopReason.Rooted;
                    if (behavior != UnitBehavior.Attacking) behavior = UnitBehavior.Idle;
                }
                else if (identity.Kind == UnitKind.Hero)
                    behavior = math.distancesq(ground.Position, ground.Previous) > 0.00000001f ? UnitBehavior.Moving :
                        Math.Abs(vital.LastAttackTime - ElapsedTime) < 0.0001 ? UnitBehavior.Attacking : UnitBehavior.Idle;
                var position = new MapPoint(ground.Position.x, ground.Position.y);
                var center = position;
                float radius = 0;
                if (soldier && state.Stance == CombatStance.Alert)
                {
                    radius = _definition.SoldierDetectionRadius;
                    if (state.Command == SoldierCommand.FollowHero && hero != Entity.Null && em.GetComponentData<Vitals>(hero).Health > 0)
                    {
                        var heroGround = em.GetComponentData<GroundTransform>(hero);
                        center = new MapPoint(heroGround.Position.x, heroGround.Position.y);
                        radius = state.FollowCombatRadius > 0 ? state.FollowCombatRadius : radius;
                    }
                }
                else if (UnitKinds.IsEnemy(identity.Kind)) radius = _definition.Navigation.DetectionRadius;
                Entity target = em.GetComponentData<CombatTargetState>(entity).Target;
                bool hasTarget = target != Entity.Null && em.Exists(target);
                string label = "无";
                MapPoint targetPoint = default;
                if (hasTarget && em.HasComponent<UnitIdentity>(target))
                {
                    var targetIdentity = em.GetComponentData<UnitIdentity>(target);
                    var targetGround = em.GetComponentData<GroundTransform>(target);
                    label = targetIdentity.Kind + " #" + targetIdentity.Id;
                    targetPoint = new MapPoint(targetGround.Position.x, targetGround.Position.y);
                }
                else if (hasTarget && em.HasComponent<DefenseStructure>(target))
                {
                    var structure = em.GetComponentData<DefenseStructure>(target);
                    string id = "建筑";
                    foreach (var pair in _structures) if (pair.Value == target) { id = pair.Key; break; }
                    label = (structure.IsMainCastle != 0 ? "主堡 " : "建筑 ") + id;
                    targetPoint = new MapPoint((structure.Min.x + structure.Max.x) * 0.5f, (structure.Min.y + structure.Max.y) * 0.5f);
                }
                if (_externalDebugTargets.TryGetValue(identity.Id, out var external) && external.Time == ElapsedTime)
                {
                    label = external.Label;
                    targetPoint = external.Position;
                    hasTarget = true;
                }
                var path = em.GetBuffer<NavigationWaypoint>(entity);
                var points = new List<MapPoint>();
                for (int i = math.clamp(navigation.WaypointIndex, 0, path.Length); i < path.Length; i++)
                    points.Add(new MapPoint(path[i].Position.x, path[i].Position.y));
                destination.Add(new UnitAiDebugSnapshot(identity.Id, identity.Kind, position, state.Command, state.Stance, behavior,
                    label, targetPoint, hasTarget, center, radius, stop, math.max(0, vital.NextNormalAttackAt - ElapsedTime), ElapsedTime, points));
            }
        }
    }
}
