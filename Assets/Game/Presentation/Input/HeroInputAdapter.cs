using System;
using Game.Battle.Contracts.Commands;
using Game.Battle.Contracts.Ports;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Input
{
    /// <summary>把键盘、手柄和触控摇杆统一成移动意图；不直接修改 ECS 或画面 Transform。</summary>
    public sealed class HeroInputAdapter : IHeroInput, IDisposable
    {
        private readonly InputAction _move;
        private readonly VirtualJoystick _joystick;
        public HeroInputAdapter(VirtualJoystick joystick)
        {
            _joystick = joystick;
            _move = new InputAction("Hero Move", InputActionType.Value);
            // 一个二维动作组合上下左右四个按键；两套键盘绑定和手柄共用同一输出。
            _move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            _move.AddBinding("<Gamepad>/leftStick");
            _move.Enable();
        }

        public MoveIntent Read()
        {
            var keyboard = _move.ReadValue<Vector2>();
            var touch = _joystick != null ? _joystick.Direction : Vector2.zero;
            // 摇杆有输入时优先使用摇杆；限制向量长度但保留小幅拖动的模拟力度。
            var direction = Vector2.ClampMagnitude(touch.sqrMagnitude > 0 ? touch : keyboard, 1);
            // 原型可在后台模拟，但失焦时不继续保留角色移动意图。
            if (!Application.isFocused)
                direction = Vector2.zero;
            return new MoveIntent(direction.x, direction.y);
        }

        // InputAction 自己创建就自己释放，由 DI 作用域结束时触发。
        public void Dispose()
        {
            _move.Disable();
            _move.Dispose();
        }
    }
}
