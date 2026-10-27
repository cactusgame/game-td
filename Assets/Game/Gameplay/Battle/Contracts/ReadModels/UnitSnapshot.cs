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
        public bool IsStationed => Stance == CombatStance.HoldGround;
        // 命令和姿态当前只适用于士兵；英雄尚无 AI 行为时为 None。
        public readonly SoldierCommand Command;
        public readonly CombatStance Stance;
        public readonly UnitBehavior Behavior;
        public readonly UnitStatusSnapshot Status;
        public bool CanAttack => Health > 0 && Status.CanAttack;
        public bool CanCast => Health > 0 && Status.CanCast;
        public bool CanMove => (Health > 0 || IsGhost) && Status.CanMove;
        public readonly double RespawnRemainingSeconds;
        public readonly bool IsGhost;
        public readonly float DeathX, DeathY, DeathFacingX;
        public readonly uint DeathSequence;
        public readonly double InvulnerabilityRemainingSeconds;
        public readonly SkillCooldownSnapshot SkillOneCooldown, SkillTwoCooldown;
        public bool IsInvulnerable => InvulnerabilityRemainingSeconds > 0;
        public UnitSnapshot(ulong id, UnitKind kind, float x, float y, float facingX, int health, int maxHealth, float lastHitTime, float lastAttackTime = -100, bool isSelected = false, bool isStationed = false, double respawnRemainingSeconds = 0,
            bool isGhost = false, float deathX = 0, float deathY = 0, float deathFacingX = 1, uint deathSequence = 0, double invulnerabilityRemainingSeconds = 0,
            SkillCooldownSnapshot skillOneCooldown = default, SkillCooldownSnapshot skillTwoCooldown = default,
            SoldierCommand command = SoldierCommand.None, CombatStance stance = CombatStance.Alert, UnitBehavior behavior = UnitBehavior.None,
            UnitStatusSnapshot status = default)
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
            Stance = isStationed ? CombatStance.HoldGround : stance;
            Command = command;
            Behavior = behavior;
            Status = status;
            RespawnRemainingSeconds = respawnRemainingSeconds;
            IsGhost = isGhost;
            DeathX = deathX;
            DeathY = deathY;
            DeathFacingX = deathFacingX;
            DeathSequence = deathSequence;
            InvulnerabilityRemainingSeconds = invulnerabilityRemainingSeconds;
            SkillOneCooldown = skillOneCooldown;
            SkillTwoCooldown = skillTwoCooldown;
        }
    }
}
