using System;
using System.Collections.Generic;
using Game.Battle.Domain;

namespace Game.Battle.Contracts
{
    /// <summary>设备无关的移动意图：表示玩家想往哪里走，实际位移由模拟根据速度和步长计算。</summary>
    public readonly struct MoveIntent
    {
        public readonly float X, Y;
        public MoveIntent(float x, float y) { X = x; Y = y; }
    }

    /// <summary>
    /// 从 ECS 复制出来的只读显示数据，不包含 Entity 句柄或底层组件引用。
    /// 表现层通过业务 Id 关联视图，使用此副本不会修改模拟中的权威状态。
    /// </summary>
    public readonly struct UnitSnapshot
    {
        public readonly ulong Id;
        public readonly UnitKind Kind;
        public readonly float X, Y, FacingX;
        public readonly int Health, MaxHealth;
        public readonly float LastHitTime, LastAttackTime;
        public UnitSnapshot(ulong id, UnitKind kind, float x, float y, float facingX,
            int health, int maxHealth, float lastHitTime, float lastAttackTime = -100)
        {
            Id = id; Kind = kind; X = x; Y = y; FacingX = facingX;
            Health = health; MaxHealth = maxHealth; LastHitTime = lastHitTime; LastAttackTime = lastAttackTime;
        }
    }

    // 配置源可替换；应用层不需要了解 TextAsset 或 JSON 序列化细节。
    public interface IBattleDefinitionSource { BattleDefinition Load(); }
    // 键盘、手柄、触控摇杆在适配层统一转换成 MoveIntent。
    public interface IHeroInput { MoveIntent Read(); }

    /// <summary>应用层调用模拟的边界；当前实现为 ECS，但契约不暴露 Entities 类型。</summary>
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
