using Game.Battle.Contracts.Commands;
using Game.Battle.Contracts.Ports;
using UnityEngine.InputSystem;

namespace Game.Tools.MapPlaytest.Unity.Input
{
    /// <summary>键盘只产生移动意图，不写入单位位置。</summary>
    public sealed class KeyboardHeroInput : IHeroInput
    {
        public MoveIntent Read()
        {
            var keys = Keyboard.current;
            if (keys == null)
                return new MoveIntent(0, 0);
            float x = (keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1 : 0) - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1 : 0);
            float y = (keys.wKey.isPressed || keys.upArrowKey.isPressed ? 1 : 0) - (keys.sKey.isPressed || keys.downArrowKey.isPressed ? 1 : 0);
            return new MoveIntent(x, y);
        }
    }
}
