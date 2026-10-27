using Game.Battle.Application.Sessions;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>运行流程入口：启动时开始一局；每个画面帧把经过的时间交给应用层。</summary>
    public sealed class BattleEntryPoint : IStartable, ITickable
    {
        private readonly BattleSession _session;
        public BattleEntryPoint(BattleSession session)
        {
            _session = session;
        }

        public void Start()
        {
            // 画面限帧由 Presentation 的 RuntimeFrameRate 统一设置，模拟仍按配置的固定步推进。
            Application.runInBackground = true;
            _session.Restart();
        }

        // 这是战斗模拟的唯一驱动入口；自建 ECS World 没有另外接入默认 PlayerLoop。
        public void Tick()
        {
            _session.Advance(Time.deltaTime);
        }
    }
}
