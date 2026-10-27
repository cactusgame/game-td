using System.Collections.Generic;
using Game.Battle.Application.Sessions;
using Game.Battle.Contracts.Commands;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Input
{
    /// <summary>PC 操作适配；未来触控可直接提交同一 PlayerOperation。</summary>
    public sealed class KeyboardPlayerOperations
    {
        private readonly PressGesture[] _buttons = { new PressGesture(), new PressGesture(), new PressGesture(), new PressGesture(), new PressGesture() };
        private bool _inactive;
        public void Reset()
        {
            foreach (var button in _buttons) button.Reset();
        }
        public void Read(List<PlayerOperation> destination, bool suppressSkills = false)
        {
            var keys = Keyboard.current;
            if (!UnityEngine.Application.isFocused || keys == null)
            {
                if (!_inactive)
                {
                    destination.Clear();
                    destination.Add(PlayerOperation.Cancel);
                }
                Reset();
                _inactive = true;
                return;
            }
            _inactive = false;
            var controls = new[] { keys.spaceKey, keys.rKey, keys.digit1Key, keys.digit2Key, keys.digit3Key };
            var taps = new[] { PlayerOperation.SkillOne, PlayerOperation.NearbyTap, PlayerOperation.CallOne, PlayerOperation.CallOneMelee, PlayerOperation.CallOneRanged };
            var holds = new[] { PlayerOperation.SkillTwo, PlayerOperation.NearbyHold, PlayerOperation.CallAll, PlayerOperation.CallAllMelee, PlayerOperation.CallAllRanged };
            for (int i = 0; i < _buttons.Length; i++)
            {
                if (i == 0 && suppressSkills)
                {
                    _buttons[i].Reset();
                    continue;
                }
                int gesture = _buttons[i].Update(controls[i].isPressed, Time.unscaledTimeAsDouble);
                if (gesture == 1) destination.Add(taps[i]);
                if (gesture == 2 || gesture == 4) destination.Add(holds[i]);
                // 低帧率下可能直接收到长按松键，仍要执行一次长按。
                if (gesture == 3 || gesture == 4)
                {
                    if (i == 1) destination.Add(PlayerOperation.NearbyRelease);
                }
            }
        }
    }
}
