using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Game.Battle.Contracts.Ports;
using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.Definitions;
using Game.Battle.Domain.ValueObjects;

namespace Game.Battle.Application.Sessions
{
    /// <summary>
    /// 应用层负责组织一局的加载、重启与时间推进。
    /// 实际生命和位置归模拟实现所有，这里只保留供界面读取的快照与统计。
    /// </summary>
    public sealed class BattleSession
    {
        private const int MaxStepsPerFrame = 8;
        private readonly IBattleDefinitionSource _source;
        private readonly IBattleSimulation _simulation;
        private readonly IHeroInput _input;
        private readonly List<UnitSnapshot> _units = new List<UnitSnapshot>();
        private readonly ReadOnlyCollection<UnitSnapshot> _readOnlyUnits;
        // 尚未消费的模拟时间。画面帧与模拟步不一一对应：一帧可以执行零步或多步。
        private float _accumulator;
        public BattleDefinition Definition { get; private set; }

        // 每次重启递增，视图用它区分两局中重复使用的单位 ID，并重建显示对象。
        public int Generation { get; private set; }

        // 活视图只读包装：后续 Refresh 可更新内容，消费者无法强转回内部 List 修改数据。
        public IReadOnlyList<UnitSnapshot> Units => _readOnlyUnits;
        public int AliveMonsters { get; private set; }

        public int HeroHealth { get; private set; }

        public int ContactCount => _simulation.ContactCount;
        public double ElapsedTime => _simulation.ElapsedTime;
        public double LastSimulationMs { get; private set; }

        public BattleSession(IBattleDefinitionSource source, IBattleSimulation simulation, IHeroInput input)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _readOnlyUnits = _units.AsReadOnly();
        }

        /// <summary>首次启动和 Restart 按钮都走此流程：加载配置 → 重建单位 → 刷新快照。</summary>
        public void Restart()
        {
            // 先完成配置解析和验证；非法配置不会进入 Reset，当前模拟也就不会被提前清空。
            var definition = _source.Load();
            _simulation.Reset(definition);
            Definition = definition;
            Generation++;
            _accumulator = 0;
            LastSimulationMs = 0;
            Refresh();
        }

        /// <summary>每个画面帧调用；内部按固定步长推进，不把不稳定的画面帧时直接当作移动步长。</summary>
        public void Advance(float deltaTime, Action beforeStep = null, Func<bool> afterStep = null)
        {
            // 非有限时间会污染累加器，使后续合法帧也永远无法补步；在任何状态修改前拒绝。
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Frame time must be finite.");
            }

            if (Definition == null)
                return;
            long start = Stopwatch.GetTimestamp();
            // 卡顿或切回窗口时限制补步量，避免为了追赶历史时间而持续卡顿；超额时间会被舍弃。
            _accumulator += Math.Min(Math.Max(deltaTime, 0), Definition.FixedStep * MaxStepsPerFrame);
            // 一次读取本画面帧的输入，本帧的所有模拟补步使用同一份移动意图。
            var intent = _input.Read();
            int steps = 0;
            while (_accumulator >= Definition.FixedStep && steps < MaxStepsPerFrame)
            {
                // 接口调用会进入 EcsBattleSimulation.Step，再由系统组调度移动与接触处理。
                beforeStep?.Invoke();
                _simulation.Step(Definition.FixedStep, intent);
                _accumulator -= Definition.FixedStep;
                steps++;
                if (afterStep != null)
                {
                    Refresh();
                    if (!afterStep())
                        break;
                }
            }

            Refresh();
            // 此耗时包括固定步推进、等待相关 Job 和收集快照，不只是移动公式本身的耗时。
            LastSimulationMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }

        public void RefreshSnapshot() => Refresh();

        private void Refresh()
        {
            // 只读取已完成的模拟结果。UI 修改显示内容不会反向改变 ECS 的生命或位置。
            _simulation.CopyUnits(_units);
            AliveMonsters = 0;
            HeroHealth = 0;
            foreach (var unit in _units)
            {
                if (unit.Kind == UnitKind.Hero)
                {
                    HeroHealth = unit.Health;
                }
                else if (UnitKinds.IsEnemy(unit.Kind) && unit.Health > 0)
                {
                    AliveMonsters++;
                }
            }
        }
    }
}
