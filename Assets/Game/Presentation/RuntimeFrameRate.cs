using UnityEngine;

namespace Game.Presentation
{
    /// <summary>所有游戏场景共用的画面帧率策略；不修改战斗模拟的固定步长或时间倍率。</summary>
    internal static class RuntimeFrameRate
    {
        private const int TargetFramesPerSecond = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            // 桌面端启用 VSync 时会忽略 targetFrameRate，因此由这里统一负责画面限帧。
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFramesPerSecond;
        }
    }
}
