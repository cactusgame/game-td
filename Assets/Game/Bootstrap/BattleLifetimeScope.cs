using Game.Battle.Application;
using Game.Battle.Contracts;
using Game.Battle.Simulation;
using Game.Infrastructure.Configuration;
using Game.Presentation.Input;
using Game.Presentation.UI;
using Game.Presentation.Views;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// 场景的依赖组装入口。基类 LifetimeScope 在 Awake 中建立容器，再调用这里的 Configure。
    /// 阅读顺序：BattleEntryPoint → BattleSession → EcsBattleSimulation → 两个 ECS 系统 → BattleView。
    /// </summary>
    public sealed class BattleLifetimeScope : LifetimeScope
    {
        // 场景序列化保存的资源和组件引用；逻辑层不会自己在场景里查找这些对象。
        [SerializeField] private TextAsset definition;
        [SerializeField] private VirtualJoystick joystick;
        [SerializeField] private BattleView battleView;
        [SerializeField] private BattleHud battleHud;
        [SerializeField] private PerformanceHud performanceHud;

        // 编辑器搭建工具调用此方法，把引用写入场景；运行时由场景反序列化恢复。
        public void ConfigureScene(TextAsset config, VirtualJoystick input, BattleView view, BattleHud hud, PerformanceHud performance)
        { definition = config; joystick = input; battleView = view; battleHud = hud; performanceHud = performance; }

        protected override void Configure(IContainerBuilder builder)
        {
            // 将契约绑定到具体实现。BattleSession 只依赖三个接口，不依赖 JSON、输入设备或 ECS API。
            builder.RegisterInstance<IBattleDefinitionSource>(new JsonBattleDefinitionSource(definition));
            builder.Register<HeroInputAdapter>(_ => new HeroInputAdapter(joystick), Lifetime.Scoped).As<IHeroInput>();
            builder.Register<EcsBattleSimulation>(Lifetime.Scoped).As<IBattleSimulation>();
            builder.Register<BattleSession>(Lifetime.Scoped);
            // 注册场景中已有的视图组件，并向其 [Inject] 方法注入同一个 BattleSession。
            // Scoped 服务随作用域管理生命周期，模拟 World 和 InputAction 会在释放服务时清理。
            builder.RegisterComponent(battleView);
            builder.RegisterComponent(battleHud);
            builder.RegisterComponent(performanceHud);

            // 让 VContainer 驱动下面普通 C# 类的 Start/Tick，而不是给每个单位添加 MonoBehaviour.Update。
            builder.RegisterEntryPoint<BattleEntryPoint>();
        }
    }

    /// <summary>运行流程入口：启动时开始一局；每个画面帧把经过的时间交给应用层。</summary>
    public sealed class BattleEntryPoint : IStartable, ITickable
    {
        private readonly BattleSession _session;
        public BattleEntryPoint(BattleSession session) { _session = session; }
        public void Start()
        {
            // 120 是画面目标帧率；战斗模拟步长由 JSON 的 FixedStep 单独决定。
            Application.targetFrameRate = 120;
            Application.runInBackground = true;
            _session.Restart();
        }
        // 这是战斗模拟的唯一驱动入口；自建 ECS World 没有另外接入默认 PlayerLoop。
        public void Tick() { _session.Advance(Time.deltaTime); }
    }
}
