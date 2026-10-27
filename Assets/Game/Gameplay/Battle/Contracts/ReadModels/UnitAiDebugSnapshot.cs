using System;
using System.Collections.Generic;
using Game.Battle.Domain.ValueObjects;
using Game.MapEngine.Domain;

namespace Game.Battle.Contracts.ReadModels
{
    /// <summary>固定步边界的独立只读诊断快照，不泄漏 Entity、Buffer 或可写集合。</summary>
    public readonly struct UnitAiDebugSnapshot
    {
        public readonly ulong UnitId;
        public readonly UnitKind Kind;
        public readonly MapPoint Position;
        public readonly SoldierCommand Command;
        public readonly CombatStance Stance;
        public readonly UnitBehavior Behavior;
        public readonly string Target;
        public readonly MapPoint TargetPosition;
        public readonly bool HasTarget;
        public readonly MapPoint AlertCenter;
        public readonly float AlertRadius;
        public readonly MovementStopReason StopReason;
        public readonly double AttackCooldownSeconds;
        public readonly double SimulationTime;
        public IReadOnlyList<MapPoint> Path { get; }

        public UnitAiDebugSnapshot(ulong unitId, UnitKind kind, MapPoint position, SoldierCommand command, CombatStance stance,
            UnitBehavior behavior, string target, MapPoint targetPosition, bool hasTarget, MapPoint alertCenter, float alertRadius,
            MovementStopReason stopReason, double attackCooldownSeconds, double simulationTime, IReadOnlyList<MapPoint> path)
        {
            UnitId = unitId;
            Kind = kind;
            Position = position;
            Command = command;
            Stance = stance;
            Behavior = behavior;
            Target = target;
            TargetPosition = targetPosition;
            HasTarget = hasTarget;
            AlertCenter = alertCenter;
            AlertRadius = alertRadius;
            StopReason = stopReason;
            AttackCooldownSeconds = attackCooldownSeconds;
            SimulationTime = simulationTime;
            var copy = new MapPoint[path?.Count ?? 0];
            for (int i = 0; i < copy.Length; i++) copy[i] = path[i];
            Path = Array.AsReadOnly(copy);
        }
    }
}
