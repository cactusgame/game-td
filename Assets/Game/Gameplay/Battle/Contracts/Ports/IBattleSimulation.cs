using System;
using System.Collections.Generic;
using Game.Battle.Domain.Definitions;
using Game.Battle.Contracts.Commands;
using Game.Battle.Contracts.ReadModels;

namespace Game.Battle.Contracts.Ports
{
    /// <summary>模拟生命周期端口；Reset 重建一局，Step 推进，CopyUnits 写入调用方缓冲，Dispose 释放原生资源。</summary>
    public interface IBattleSimulation : IDisposable
    {
        double ElapsedTime { get; }

        int ContactCount { get; }

        // Reset 重建一局；Step 推进一步；CopyUnits 将结果写入调用方复用的列表。
        // Dispose 的职责包括释放模拟使用的原生资源，而不仅是等待 C# GC。
        void Reset(BattleDefinition definition);
        void Step(float deltaTime, MoveIntent intent);
        void CopyUnits(List<UnitSnapshot> destination);
    }
}
