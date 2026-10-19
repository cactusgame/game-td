using UnityEngine.Serialization;
using Game.Battle.Application.Sessions;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using VContainer;

namespace Game.Presentation.UI
{
    /// <summary>表现层性能采样，使用真实画面帧时；统计刷新频率与战斗固定步无关。</summary>
    public sealed class PerformanceHud : MonoBehaviour
    {
        [FormerlySerializedAs("label")]
        [SerializeField]
        private TMP_Text _label;
        private BattleSession _session;
        private ProfilerRecorder _drawCalls;
        private float _elapsed, _worst;
        private int _frames;
        private double _simulationTotal;
        public void Configure(TMP_Text text)
        {
            _label = text;
        }

        [Inject]
        public void Initialize(BattleSession session)
        {
            _session = session;
        }

        private void OnEnable()
        {
            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
        }

        private void Update()
        {
            if (_session == null)
                return;
            float dt = Time.unscaledDeltaTime;
            _elapsed += dt;
            _frames++;
            _worst = Mathf.Max(_worst, dt);
            _simulationTotal += _session.LastSimulationMs;
            // 在约半秒窗口内统计平均 FPS、平均帧时与最慢帧，而不是用一个瞬时帧猜整体性能。
            if (_elapsed < 0.5f)
                return;
            const double mb = 1024.0 * 1024.0;
            // 当前窗口没有有效渲染计数时显示 n/a，不能把缺失数据解释成零次绘制。
            string draws = _drawCalls.Valid && _drawCalls.Count > 0 && _drawCalls.LastValue > 0 ? _drawCalls.LastValue.ToString() : "n/a";
            // Editor 内存包含编辑器开销；这些是 Unity 内存计数，不是操作系统进程工作集。
            // SIM 来自 BattleSession，包含模拟、同步等待和快照收集。
            string context = Application.isEditor ? "EDITOR PROCESS" : "PLAYER";
            _label.text = $"<color=#E6CB83><b>PERFORMANCE</b></color>  /  {context}\n" + $"<size=28><b>{_frames / _elapsed:0}</b></size> FPS    {_elapsed * 1000 / _frames:0.0} ms\n" + $"WORST {_worst * 1000:0.0} ms    SIM {_simulationTotal / _frames:0.00} ms\n" + $"UNITY USED {Profiler.GetTotalAllocatedMemoryLong() / mb:0.0} MB\n" + $"RESERVED {Profiler.GetTotalReservedMemoryLong() / mb:0.0} MB\n" + $"MANAGED {Profiler.GetMonoUsedSizeLong() / mb:0.0} MB\n" + $"UNITS {_session.AliveMonsters + (_session.HeroHealth > 0 ? 1 : 0)}    DRAW CALLS {draws}";
            _elapsed = _worst = 0;
            _frames = 0;
            _simulationTotal = 0;
        }

        // ProfilerRecorder 持有资源，组件禁用时配对释放。
        private void OnDisable()
        {
            _drawCalls.Dispose();
        }
    }
}
