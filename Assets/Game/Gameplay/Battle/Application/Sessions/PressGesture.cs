namespace Game.Battle.Application.Sessions
{
    /// <summary>单键短/长按状态机；用输入时钟判定，不依赖模拟补步次数。</summary>
    public sealed class PressGesture
    {
        public const double HoldSeconds = 0.45;
        private bool _pressed, _held, _blocked;
        private double _started;
        public void Reset() { _pressed = _held = false; _blocked = true; }
        // 0=无动作，1=短按松开，2=到达长按阈值，3=长按松开，4=低帧率跳过阈值后松开。
        public int Update(bool down, double now)
        {
            if (_blocked)
            {
                if (!down) _blocked = false;
                return 0;
            }
            if (down && !_pressed)
            {
                _pressed = true;
                _held = false;
                _started = now;
            }
            if (!_pressed) return 0;
            if (down && !_held && now - _started >= HoldSeconds)
            {
                _held = true;
                return 2;
            }
            if (down) return 0;
            _pressed = false;
            return _held ? 3 : now - _started >= HoldSeconds ? 4 : 1;
        }
    }
}
