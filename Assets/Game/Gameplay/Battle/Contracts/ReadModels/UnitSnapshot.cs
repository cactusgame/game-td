using System;
using System.Collections.Generic;
using Game.Battle.Domain.ValueObjects;

namespace Game.Battle.Contracts.ReadModels
{
    /// <summary>从模拟复制的只读显示数据，不包含实体句柄或可变组件引用。</summary>
    public readonly struct UnitSnapshot
    {
        public readonly ulong Id;
        public readonly UnitKind Kind;
        public readonly float X, Y, FacingX;
        public readonly int Health, MaxHealth;
        public readonly float LastHitTime, LastAttackTime;
        public readonly bool IsSelected;
        public UnitSnapshot(ulong id, UnitKind kind, float x, float y, float facingX, int health, int maxHealth, float lastHitTime, float lastAttackTime = -100, bool isSelected = false)
        {
            Id = id;
            Kind = kind;
            X = x;
            Y = y;
            FacingX = facingX;
            Health = health;
            MaxHealth = maxHealth;
            LastHitTime = lastHitTime;
            LastAttackTime = lastAttackTime;
            IsSelected = isSelected;
        }
    }
}
