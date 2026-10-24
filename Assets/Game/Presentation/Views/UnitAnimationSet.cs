using System;
using UnityEngine;

namespace Game.Presentation.Views
{
    /// <summary>
    /// 制作完成的一套人物动画资源。帧引用、标准动作、帧率和视觉校准随素材一起保存。
    /// 产品只选择此资产的资源 ID；运行时只读，不在这里保存单位当前动作或模拟状态。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Unit Animation Set")]
    public sealed class UnitAnimationSet : ScriptableObject
    {
        public Sprite[] Frames;
        public float BodyHeightFraction;
        public float DeathBodyHeightFraction;
        public bool FacesLeft;
        [Tooltip("原始素材缺少倒地帧时，由表现补充倾倒收尾，不改变战斗死亡判定。")]
        public bool UseFallbackDeath;

        [Header("Required actions")]
        public UnitActionSettings Idle;
        public UnitActionSettings Walk;
        public UnitActionSettings Attack;

        [Header("Optional actions (empty means unavailable)")]
        public UnitActionSettings Run;
        public UnitActionSettings Hurt;
        public UnitActionSettings Death;
        public UnitActionSettings Skill;
        public UnitActionSettings Skill2;
        public UnitActionSettings Skill3;
        public UnitActionSettings Jump;
        public UnitActionSettings Spawn;
        public UnitActionSettings WalkBack;

        public UnitActionSettings GetSequence(UnitAction action)
        {
            switch (action)
            {
                case UnitAction.Idle: return Idle;
                case UnitAction.Walk: return Walk;
                case UnitAction.Attack: return Attack;
                case UnitAction.Run: return Run;
                case UnitAction.Hurt: return Hurt;
                case UnitAction.Death: return Death;
                case UnitAction.Skill: return Skill;
                case UnitAction.Skill2: return Skill2;
                case UnitAction.Skill3: return Skill3;
                case UnitAction.Jump: return Jump;
                case UnitAction.Spawn: return Spawn;
                case UnitAction.WalkBack: return WalkBack;
                default: throw new ArgumentOutOfRangeException(nameof(action));
            }
        }
    }
}
