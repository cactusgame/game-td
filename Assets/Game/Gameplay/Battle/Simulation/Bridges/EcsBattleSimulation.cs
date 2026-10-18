using System.Collections.Generic;
using Game.Battle.Contracts;
using Game.Battle.Domain;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation
{
    // 系统组也是系统，负责按排序结果调用子系统。禁用自动创建，交由下面的桥接器手动组装。
    [DisableAutoCreation]
    public partial class BattleSystemGroup : ComponentSystemGroup { }

    /// <summary>
    /// 应用层与 ECS 之间的桥接器：拥有 World，负责生成单位、推进系统组、复制快照和释放资源。
    /// Entity 不会自动生成 GameObject；画面由 Presentation 中的 BattleView 单独创建。
    /// </summary>
    public sealed class EcsBattleSimulation : IBattleSimulation
    {
        private readonly World _world;
        private readonly BattleSystemGroup _group;
        private readonly EntityQuery _units;
        // Entity 只是这个 World 中的句柄；数据在它所挂载的组件里。
        private readonly Entity _settings;
        public double ElapsedTime { get; private set; }
        public int ContactCount => _world.EntityManager.GetComponentData<ContactStatistics>(_settings).Count;

        public EcsBattleSimulation()
        {
            // World 是实体、组件数据和系统的独立运行环境，不等于 Scene；字符串只是调试名称。
            // 此自建 World 不自动接入 PlayerLoop，只由应用层调用 Step 驱动。
            _world = new World("Battle Prototype — authoritative simulation");
            // 组继承 ComponentSystemGroup（class），Managed API 返回可直接调用的托管系统对象。
            // Managed 不表示组内不能执行并行 Job。
            _group = _world.GetOrCreateSystemManaged<BattleSystemGroup>();
            // MovementSystem/CircleContactSystem 是 ISystem 结构体，这里的 GetOrCreate 返回 SystemHandle。
            // “取得或创建系统”和“加入组的更新列表”是两步；这两步都没有执行一次模拟。
            _group.AddSystemToUpdateList(_world.GetOrCreateSystem<MovementSystem>());
            _group.AddSystemToUpdateList(_world.GetOrCreateSystem<CircleContactSystem>());
            // 根据 UpdateAfter 等约束排序；接触系统声明在移动系统之后调用，不依赖添加顺序。
            _group.SortSystems();
            // 一个设置实体保存本局设置和统计；它没有单位组件，所以不会匹配下面的 _units 查询。
            _settings = _world.EntityManager.CreateEntity(typeof(BattleSettings), typeof(ContactStatistics));
            // 查询同时具有三种组件的实体，允许它们还有其他组件。查询不是固定的旧实体数组，
            // Reset 后新生成且符合条件的单位也会匹配。
            _units = _world.EntityManager.CreateEntityQuery(typeof(UnitIdentity), typeof(GroundTransform), typeof(Vitals));
        }

        public void Reset(BattleDefinition definition)
        {
            var em = _world.EntityManager;
            // 创建/销毁实体是结构性变更。先等待原有 Job，防止其继续访问即将被清空的数据。
            em.CompleteAllTrackedJobs();
            // 只销毁单位查询匹配的实体，保留 _settings 与系统本身。
            em.DestroyEntity(_units);
            ElapsedTime = 0;
            em.SetComponentData(_settings, new BattleSettings { HalfSize = new float2(definition.Width, definition.Height) * 0.5f });
            em.SetComponentData(_settings, new ContactStatistics());
            // 重置种子，使同样的配置和输入在当前运行环境下可以重复验证；不承诺跨平台逐帧一致。
            var random = new Random(definition.Seed);
            Spawn(1, definition.Hero, float2.zero, random.NextUInt(1, uint.MaxValue));
            for (int i = 0; i < definition.MonsterCount; i++)
            {
                var unit = i < definition.GoblinCount ? definition.Goblin : definition.Runner;
                float2 limit = new float2(definition.Width, definition.Height) * 0.5f - unit.Radius - 0.2f;
                float2 position = limit;
                float clearanceSq = math.pow(definition.Hero.Radius + unit.Radius + 1, 2);
                // 随机尝试有上限；找不到时使用已由领域配置验证过的角落位置，避免无限循环。
                for (int attempt = 0; attempt < 128; attempt++)
                {
                    var candidate = random.NextFloat2(-limit, limit);
                    if (math.lengthsq(candidate) < clearanceSq) continue;
                    position = candidate;
                    break;
                }
                Spawn((ulong)i + 2, unit, position, random.NextUInt(1, uint.MaxValue));
            }
        }

        private void Spawn(ulong id, UnitDefinition definition, float2 position, uint seed)
        {
            var em = _world.EntityManager;
            // 五种组件是所有单位共有的数据，也是 MovementJob.Execute 的默认匹配条件。
            // 同组件类型组合称为 Archetype；这些组件数据会由 ECS 按 Chunk 组织存储。
            var entity = em.CreateEntity(typeof(UnitIdentity), typeof(GroundTransform), typeof(Vitals),
                typeof(UnitMotion), typeof(WanderState));
            // SetComponentData 修改已有组件，不会自动添加缺失组件；这里写入只读定义中的初始属性。
            em.SetComponentData(entity, new UnitIdentity { Id = id, Kind = definition.Kind });
            em.SetComponentData(entity, new GroundTransform { Position = position, Previous = position, FacingX = 1 });
            em.SetComponentData(entity, new Vitals { Health = definition.MaxHealth, MaxHealth = definition.MaxHealth,
                Attack = definition.Attack, Defense = definition.Defense, LastHitTime = -100, LastAttackTime = -100 });
            em.SetComponentData(entity, new UnitMotion { Speed = definition.Speed, Radius = definition.Radius });
            em.SetComponentData(entity, new WanderState { Random = new Random(seed) });
            // 英雄只额外带 HeroTag；怪物额外带 MonsterTag 和 HeroContact，不会同时带两种分类标记。
            // 英雄也有 WanderState 以匹配统一移动 Job，但英雄分支不使用随机行走状态。
            // 添加组件会改变 Archetype，当前集中在生成阶段执行。
            if (definition.Kind == UnitKind.Hero) em.AddComponent<HeroTag>(entity);
            else { em.AddComponent<MonsterTag>(entity); em.AddComponent<HeroContact>(entity); }
        }

        public void Step(float deltaTime, MoveIntent intent)
        {
            // GetComponentData 返回结构体副本：修改局部 settings 后必须 SetComponentData 写回。
            // MoveInput 是输入跨越应用层与 ECS 的数据载体，移动系统随后读取它。
            var settings = _world.EntityManager.GetComponentData<BattleSettings>(_settings);
            settings.MoveInput = new float2(intent.X, intent.Y);
            _world.EntityManager.SetComponentData(_settings, settings);
            ElapsedTime += deltaTime;
            // 设置系统读取到的 SystemAPI.Time；设置时间本身不执行系统。
            _world.SetTime(new TimeData(ElapsedTime, deltaTime));
            // 真正进入系统更新：移动 OnUpdate 调度 Job，接触 OnUpdate 在相关数据读取前完成必要依赖。
            // 系统调用顺序与 Job 完成顺序是两个概念，不能仅凭 UpdateAfter 认为后台计算已结束。
            _group.Update();
        }

        public void CopyUnits(List<UnitSnapshot> destination)
        {
            // 主线程需要完整显示结果时等待本 World 的 Job。此同步点便于原型验证，扩容后需评估等待成本。
            _world.EntityManager.CompleteAllTrackedJobs();
            destination.Clear();
            // ToComponentDataArray 复制数据，不返回组件引用；修改这些数组不会改动 ECS 原始状态。
            // Allocator.Temp 是短期原生内存，using var 在方法退出时 Dispose，不能依赖 C# GC。
            using var identities = _units.ToComponentDataArray<UnitIdentity>(Allocator.Temp);
            using var positions = _units.ToComponentDataArray<GroundTransform>(Allocator.Temp);
            using var vitals = _units.ToComponentDataArray<Vitals>(Allocator.Temp);
            // 三次复制之间没有结构性变更，按同一查询顺序对应单位；再转为不含 ECS 引用的业务快照。
            for (int i = 0; i < identities.Length; i++)
                destination.Add(new UnitSnapshot(identities[i].Id, identities[i].Kind,
                    positions[i].Position.x, positions[i].Position.y, positions[i].FacingX,
                    vitals[i].Health, vitals[i].MaxHealth, vitals[i].LastHitTime, vitals[i].LastAttackTime));
        }

        // World 持有原生资源与系统状态，必须显式释放；由 VContainer 作用域管理服务清理。
        public void Dispose() { if (_world.IsCreated) _world.Dispose(); }
    }
}
