using Game.Battle.Simulation.Systems;
using Game.Battle.Simulation.Components;
using System;
using System.Collections.Generic;
using Game.Battle.Contracts.Commands;
using Game.Battle.Contracts.Ports;
using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.Definitions;
using Game.Battle.Domain.ValueObjects;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Game.Battle.Simulation.Bridges
{
    /// <summary>
    /// 应用层与 ECS 之间的桥接器：拥有 World，负责生成单位、推进系统组、复制快照和释放资源。
    /// Entity 不会自动生成 GameObject；画面由 Presentation 中的 BattleView 单独创建。
    /// </summary>
    public sealed partial class EcsBattleSimulation : ILevelEnemySimulation, IDefenseBattleSimulation, IPlayerOperations, ISoldierDismissal
    {
        private readonly Dictionary<string, Entity> _structures = new Dictionary<string, Entity>();
        private readonly Dictionary<ulong, Entity> _unitEntities = new Dictionary<ulong, Entity>();
        private BattleDefinition _definition;
        private ulong _nextId;
        private Game.MapEngine.Domain.GridMap _spawnMap;
        private readonly World _world;
        private readonly BattleSystemGroup _group;
        private readonly MonsterNavigationSystem _navigation;
        private readonly MonsterObjectiveSystem _objectives;
        private readonly TerrainCollisionSystem _terrainCollision;
        private readonly MonsterSeparationSystem _separation;
        private readonly SoldierNavigationSystem _soldierNavigation;
        private Game.MapEngine.Domain.GridMap _friendlySpawnMap;
        private readonly EntityQuery _units;
        // Entity 只是这个 World 中的句柄；数据在它所挂载的组件里。
        private readonly Entity _settings;
        public double ElapsedTime { get; private set; }

        public int ContactCount => _world.EntityManager.GetComponentData<ContactStatistics>(_settings).Count;
        public EcsBattleSimulation() : this(false)
        {
        }

        /// <summary>默认兵营驻守；跟随能力保留为显式的会话组装选项。</summary>
        public EcsBattleSimulation(bool soldiersFollowHero)
        {
            // World 是实体、组件数据和系统的独立运行环境，不等于 Scene；字符串只是调试名称。
            // 此自建 World 不自动接入 PlayerLoop，只由应用层调用 Step 驱动。
            _world = new World("Battle Prototype — authoritative simulation");
            // 组继承 ComponentSystemGroup（class），Managed API 返回可直接调用的托管系统对象。
            // Managed 不表示组内不能执行并行 Job。
            _group = _world.GetOrCreateSystemManaged<BattleSystemGroup>();
            _objectives = _world.GetOrCreateSystemManaged<MonsterObjectiveSystem>();
            _group.AddSystemToUpdateList(_objectives);
            _navigation = _world.GetOrCreateSystemManaged<MonsterNavigationSystem>();
            _group.AddSystemToUpdateList(_navigation);
            _soldierNavigation = _world.GetOrCreateSystemManaged<SoldierNavigationSystem>();
            _soldierNavigation.FollowHero = soldiersFollowHero;
            _group.AddSystemToUpdateList(_soldierNavigation);
            _group.AddSystemToUpdateList(_world.GetOrCreateSystemManaged<SoldierCombatSystem>());
            _separation = _world.GetOrCreateSystemManaged<MonsterSeparationSystem>();
            _group.AddSystemToUpdateList(_separation);
            _terrainCollision = _world.GetOrCreateSystemManaged<TerrainCollisionSystem>();
            _group.AddSystemToUpdateList(_terrainCollision);
            // MovementSystem/CircleContactSystem 是 ISystem 结构体，这里的 GetOrCreate 返回 SystemHandle。
            // “取得或创建系统”和“加入组的更新列表”是两步；这两步都没有执行一次模拟。
            _group.AddSystemToUpdateList(_world.GetOrCreateSystem<MovementSystem>());
            _group.AddSystemToUpdateList(_world.GetOrCreateSystem<CircleContactSystem>());
            // 根据 UpdateAfter 等约束排序；接触系统声明在移动系统之后调用，不依赖添加顺序。
            _group.AddSystemToUpdateList(_world.GetOrCreateSystemManaged<StructureCombatSystem>());
            _group.SortSystems();
            // 一个设置实体保存本局设置和统计；它没有单位组件，所以不会匹配下面的 _units 查询。
            _settings = _world.EntityManager.CreateEntity(typeof(BattleSettings), typeof(ContactStatistics));
            // 查询同时具有三种组件的实体，允许它们还有其他组件。查询不是固定的旧实体数组，
            // Reset 后新生成且符合条件的单位也会匹配。
            _units = _world.EntityManager.CreateEntityQuery(typeof(UnitIdentity), typeof(GroundTransform), typeof(Vitals), typeof(UnitMotion));
        }

        public void Reset(BattleDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var em = _world.EntityManager;
            // 创建/销毁实体是结构性变更。先等待原有 Job，防止其继续访问即将被清空的数据。
            em.CompleteAllTrackedJobs();
            _definition = definition;
            _spawnMap = definition.NavigationMap;
            _friendlySpawnMap = definition.NavigationMap;
            _soldierNavigation.Configure(definition.NavigationMap);
            _nextId = (ulong)definition.MonsterCount + 2;
            foreach (var entity in _structures.Values)
                em.DestroyEntity(entity);
            _structures.Clear();
            _separation.Configure(definition.Navigation.SeparationGap, definition.Navigation.SeparationSpeed);
            _objectives.Configure(definition.NavigationMap);
            _navigation.Configure(definition.NavigationMap, definition.Navigation);
            _terrainCollision.Configure(definition.NavigationMap);
            // 只销毁单位查询匹配的实体，保留 _settings 与系统本身。
            em.DestroyEntity(_units);
            _unitEntities.Clear();
            ElapsedTime = 0;
            ResetPlayerControls();
            var center = new float2(definition.NavigationMap.OriginX + definition.Width * 0.5f, definition.NavigationMap.OriginY + definition.Height * 0.5f);
            em.SetComponentData(_settings, new BattleSettings{HalfSize = new float2(definition.Width, definition.Height) * 0.5f, Center = center});
            em.SetComponentData(_settings, new ContactStatistics());
            // 重置种子，使同样的配置和输入在当前运行环境下可以重复验证；不承诺跨平台逐帧一致。
            var random = new Random(definition.Seed);
            Spawn(1, definition.Hero, new float2(definition.HeroSpawn.X, definition.HeroSpawn.Y), random.NextUInt(1, uint.MaxValue));
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
                    if (math.lengthsq(candidate) < clearanceSq)
                        continue;
                    position = candidate;
                    break;
                }

                position += center;
                if (definition.EnemySpawns.Count > 0)
                {
                    var entry = definition.EnemySpawns[i % definition.EnemySpawns.Count];
                    position = new float2(entry.X, entry.Y);
                }
                else if (!definition.NavigationMap.CanOccupy(position.x, position.y, unit.Radius))
                {
                    // 导入地图未指定入口时，只从确有净空的格子生成，不能把兜底放在障碍里。
                    bool found = false;
                    for (int cellIndex = 0; cellIndex < definition.NavigationMap.CellCount; cellIndex++)
                    {
                        var candidate = definition.NavigationMap.CellCenter(definition.NavigationMap.CellAt(cellIndex));
                        if (!definition.NavigationMap.CanOccupy(candidate.X, candidate.Y, unit.Radius))
                            continue;
                        position = new float2(candidate.X, candidate.Y);
                        found = true;
                        break;
                    }
                    if (!found)
                        throw new InvalidOperationException("Map has no clear enemy spawn.");
                }
                Spawn((ulong)i + 2, unit, position, random.NextUInt(1, uint.MaxValue));
            }
        }

        /// <summary>建设提交的同步边界：先完成旧步，再同时替换路径查询与实际碰撞地图。</summary>
        public void UpdateNavigationMap(Game.MapEngine.Domain.GridMap map, NavigationDefinition navigation, Game.MapEngine.Domain.GridMap friendlyMap = null)
        {
            _world.EntityManager.CompleteAllTrackedJobs();
            // 敌方用含门洞的路线接近破门目标，实际通行仍由敌方碰撞地图裁决。
            _spawnMap = map;
            _friendlySpawnMap = friendlyMap ?? map;
            // 士兵的出营净空与寻路必须使用和英雄碰撞相同的我方图，不能被敌方关门状态拦住。
            _soldierNavigation.Configure(_friendlySpawnMap);
            _objectives.Configure(friendlyMap ?? map);
            _navigation.Configure(friendlyMap ?? map, navigation);
            _terrainCollision.Configure(map, friendlyMap);
            using var query = _world.EntityManager.CreateEntityQuery(typeof(MonsterNavigationState));
            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var state in entities)
            {
                _world.EntityManager.SetComponentData(state, default(MonsterNavigationState));
            }
        }

        /// <summary>同一波仍在同一模拟边界生成；在入口附近寻找有净空且互不重叠的位置。</summary>
        public void SpawnEnemies(IReadOnlyList<Game.MapEngine.Domain.MapPoint> entries, int countPerEntry)
        {
            var em = _world.EntityManager;
            em.CompleteAllTrackedJobs();
            var occupied = new List<(float2 Position, float Radius)>();
            using (var positions = _units.ToComponentDataArray<GroundTransform>(Allocator.Temp))
            using (var health = _units.ToComponentDataArray<Vitals>(Allocator.Temp))
            using (var motions = _units.ToComponentDataArray<UnitMotion>(Allocator.Temp))
            {
                for (int i = 0; i < positions.Length; i++)
                    if (health[i].Health > 0)
                        occupied.Add((positions[i].Position, motions[i].Radius));
            }
            var pathfinder = new Game.MapEngine.Domain.GridPathfinder(_spawnMap);
            var path = new List<Game.MapEngine.Domain.GridCell>();
            foreach (var entry in entries)
            {
                for (int i = 0; i < countPerEntry; i++)
                {
                    var unit = i % 2 == 0 ? _definition.Goblin : _definition.Runner;
                    var position = FindSpawnPosition(new float2(entry.X, entry.Y), unit.Radius, occupied, pathfinder, path);
                    ulong id = _nextId++;
                    Spawn(id, unit, position, (uint)(id % (uint.MaxValue - 1)) + 1);
                    occupied.Add((position, unit.Radius));
                }
            }
        }

        public ulong SpawnEnemy(UnitKind kind, Game.MapEngine.Domain.MapPoint entry)
        {
            if (!UnitKinds.IsEnemy(kind)) throw new ArgumentException("关卡只能生成敌人");
            var em = _world.EntityManager;
            em.CompleteAllTrackedJobs();
            var occupied = new List<(float2 Position, float Radius)>();
            using (var positions = _units.ToComponentDataArray<GroundTransform>(Allocator.Temp))
            using (var health = _units.ToComponentDataArray<Vitals>(Allocator.Temp))
            using (var motions = _units.ToComponentDataArray<UnitMotion>(Allocator.Temp))
            {
                for (int i = 0; i < positions.Length; i++)
                    if (health[i].Health > 0) occupied.Add((positions[i].Position, motions[i].Radius));
            }
            var unit = _definition.GetUnit(kind);
            var position = FindSpawnPosition(new float2(entry.X, entry.Y), unit.Radius, occupied,
                new Game.MapEngine.Domain.GridPathfinder(_spawnMap), new List<Game.MapEngine.Domain.GridCell>());
            ulong id = _nextId++;
            Spawn(id, unit, position, (uint)(id % (uint.MaxValue - 1)) + 1);
            return id;
        }

        private float2 FindSpawnPosition(float2 entry, float radius, List<(float2 Position, float Radius)> occupied,
            Game.MapEngine.Domain.GridPathfinder pathfinder, List<Game.MapEngine.Domain.GridCell> path)
        {
            float spacing = radius * 2 + _definition.Navigation.SeparationGap;
            float2 best = entry;
            float bestClearance = float.NegativeInfinity;
            bool found = false;
            _spawnMap.TryWorldToCell(entry.x, entry.y, out var start);
            // 从近到远扫描同心环。边界或窄门附近跳过非法点，不把怪物生成到墙的另一侧。
            for (int ring = 0; ring <= 8; ring++)
            {
                int samples = ring == 0 ? 1 : ring * 8;
                for (int sample = 0; sample < samples; sample++)
                {
                    float angle = sample * (math.PI * 2 / samples);
                    float2 candidate = entry + new float2(math.cos(angle), math.sin(angle)) * (ring * spacing);
                    if (!_spawnMap.CanOccupy(candidate.x, candidate.y, radius) ||
                        !_spawnMap.TryWorldToCell(candidate.x, candidate.y, out var cell) ||
                        !pathfinder.TryFindPath(start, cell, path))
                        continue;
                    float clearance = float.PositiveInfinity;
                    foreach (var other in occupied)
                        clearance = math.min(clearance, math.distance(candidate, other.Position) - radius - other.Radius);
                    if (clearance >= _definition.Navigation.SeparationGap - 0.0001f)
                        return candidate;
                    if (clearance > bestClearance)
                    {
                        best = candidate;
                        bestClearance = clearance;
                        found = true;
                    }
                }
            }
            if (!found)
                throw new InvalidOperationException("Enemy entry has no reachable spawn space.");
            // 极端拥堵时保留本波数量，选择重叠最少的合法点，后续局部分离持续疏散。
            return best;
        }

        public void SynchronizeStructures(IReadOnlyList<DefenseStructureDefinition> definitions)
        {
            var em = _world.EntityManager;
            em.CompleteAllTrackedJobs();
            var retained = new HashSet<string>();
            foreach (var definition in definitions)
            {
                retained.Add(definition.Id);
                if (_structures.TryGetValue(definition.Id, out var existing))
                {
                    var current = em.GetComponentData<DefenseStructure>(existing);
                    // 升级更新能力并保留生命比例、攻击冷却；空间同步不会复活已毁建筑。
                    current.Health = current.Health <= 0 ? 0 : (int)math.ceil((double)current.Health * definition.MaxHealth / current.MaxHealth);
                    current.MaxHealth = definition.MaxHealth;
                    current.Attack = definition.Attack;
                    current.Range = definition.Range;
                    current.AttackInterval = definition.AttackInterval;
                    current.Min = new float2(definition.MinX, definition.MinY);
                    current.Max = new float2(definition.MaxX, definition.MaxY);
                    em.SetComponentData(existing, current);
                    continue;
                }
                var entity = em.CreateEntity(typeof(DefenseStructure));
                em.SetComponentData(entity, new DefenseStructure
                {
                    Min = new float2(definition.MinX, definition.MinY),
                    Max = new float2(definition.MaxX, definition.MaxY),
                    Health = definition.MaxHealth, MaxHealth = definition.MaxHealth,
                    IsMainCastle = definition.IsMainCastle ? (byte)1 : (byte)0,
                    Attack = definition.Attack, Range = definition.Range, AttackInterval = definition.AttackInterval
                });
                _structures.Add(definition.Id, entity);
            }
            var removed = new List<string>();
            foreach (var pair in _structures)
                if (!retained.Contains(pair.Key))
                {
                    em.DestroyEntity(pair.Value);
                    removed.Add(pair.Key);
                }
            foreach (var id in removed)
                _structures.Remove(id);
        }

        public void CopyStructures(List<DefenseStructureSnapshot> destination)
        {
            _world.EntityManager.CompleteAllTrackedJobs();
            destination.Clear();
            foreach (var pair in _structures)
            {
                var structure = _world.EntityManager.GetComponentData<DefenseStructure>(pair.Value);
                destination.Add(new DefenseStructureSnapshot(pair.Key, structure.Health, structure.IsMainCastle != 0));
            }
        }

        public void DismissSoldiers(IReadOnlyList<ulong> ids)
        {
            var em = _world.EntityManager;
            em.CompleteAllTrackedJobs();
            foreach (var id in ids)
            {
                if (!_unitEntities.TryGetValue(id, out var entity) || !em.Exists(entity)) continue;
                var identity = em.GetComponentData<UnitIdentity>(entity);
                if (!UnitKinds.IsSoldier(identity.Kind)) continue;
                em.DestroyEntity(entity);
                _unitEntities.Remove(id);
            }
        }

        private void Spawn(ulong id, UnitDefinition definition, float2 position, uint seed)
        {
            var em = _world.EntityManager;
            // 统一移动 Job 所需的数据。英雄的导航组件为空，不使用怪物寻路分支。
            // 同组件类型组合称为 Archetype；这些组件数据会由 ECS 按 Chunk 组织存储。
            var entity = em.CreateEntity(typeof(UnitIdentity), typeof(GroundTransform), typeof(Vitals), typeof(UnitMotion), typeof(WanderState), typeof(MonsterNavigationState));
            _unitEntities.Add(id, entity);
            em.AddComponentData(entity, new CombatProfile { AttackInterval = definition.AttackInterval, AttackRange = definition.AttackRange, TargetMainCastle = definition.TargetMainCastle ? (byte)1 : (byte)0 });
            em.AddBuffer<NavigationWaypoint>(entity);
            // SetComponentData 修改已有组件，不会自动添加缺失组件；这里写入只读定义中的初始属性。
            em.SetComponentData(entity, new UnitIdentity{Id = id, Kind = definition.Kind});
            em.SetComponentData(entity, new GroundTransform{Position = position, Previous = position, FacingX = 1});
            em.SetComponentData(entity, new Vitals{Health = definition.MaxHealth, MaxHealth = definition.MaxHealth, Attack = definition.Attack, Defense = definition.Defense, LastHitTime = -100, LastAttackTime = -100});
            em.SetComponentData(entity, new UnitMotion{Speed = definition.Speed, Radius = definition.Radius});
            em.SetComponentData(entity, new WanderState{Random = new Random(seed)});
            // 英雄只额外带 HeroTag；怪物额外带 MonsterTag 和 HeroContact，不会同时带两种分类标记。
            // 英雄也有 WanderState 以匹配统一移动 Job，但英雄分支不使用随机行走状态。
            // 添加组件会改变 Archetype，当前集中在生成阶段执行。
            if (definition.Kind == UnitKind.Hero)
                em.AddComponent<HeroTag>(entity);
            else if (UnitKinds.IsEnemy(definition.Kind))
            {
                em.AddComponent<MonsterTag>(entity);
                em.AddComponent<HeroContact>(entity);
            }
        }

        public bool TrySpawnSoldier(UnitKind kind, Game.MapEngine.Domain.MapPoint exit, Game.MapEngine.Domain.MapPoint rally, out ulong unitId)
        {
            unitId = 0;
            if (!UnitKinds.IsSoldier(kind)) throw new ArgumentException("A barracks can only deploy soldiers.");
            var definition = _definition.GetUnit(kind);
            var map = _friendlySpawnMap;
            if (!map.CanOccupy(exit.X, exit.Y, definition.Radius)) return false;
            var em = _world.EntityManager;
            em.CompleteAllTrackedJobs();
            using var positions = _units.ToComponentDataArray<GroundTransform>(Allocator.Temp);
            using var health = _units.ToComponentDataArray<Vitals>(Allocator.Temp);
            using var motions = _units.ToComponentDataArray<UnitMotion>(Allocator.Temp);
            for (int i = 0; i < positions.Length; i++)
            {
                float clearance = motions[i].Radius + definition.Radius + _definition.Navigation.SeparationGap;
                if (health[i].Health > 0 && math.distancesq(positions[i].Position, new float2(exit.X, exit.Y)) < clearance * clearance)
                    return false;
            }
            // 驻守点被地形覆盖时，在附近寻找可达净空；不能把兵传送到墙的另一边。
            var pathfinder = new Game.MapEngine.Domain.GridPathfinder(map);
            var cells = new List<Game.MapEngine.Domain.GridCell>();
            map.TryWorldToCell(exit.X, exit.Y, out var start);
            float2 destination = default;
            bool found = false;
            for (int ring = 0; ring <= 4 && !found; ring++)
            {
                int count = ring == 0 ? 1 : ring * 8;
                for (int i = 0; i < count; i++)
                {
                    float angle = i * math.PI * 2 / count;
                    float2 candidate = new float2(rally.X, rally.Y) + new float2(math.cos(angle), math.sin(angle)) * ring * 0.5f;
                    if (!map.CanOccupy(candidate.x, candidate.y, definition.Radius) ||
                        !map.TryWorldToCell(candidate.x, candidate.y, out var goal) || !pathfinder.TryFindPath(start, goal, cells)) continue;
                    destination = candidate;
                    found = true;
                    break;
                }
            }
            if (!found) return false;
            unitId = _nextId++;
            Spawn(unitId, definition, new float2(exit.X, exit.Y), (uint)(unitId % (uint.MaxValue - 1)) + 1);
            var entity = _unitEntities[unitId];
            em.AddComponentData(entity, new SoldierState { Rally = destination });
            em.SetComponentData(entity, new MonsterNavigationState { IsChasing = 1 });
            return true;
        }

        /// <summary>外部接收者提交伤害后调用。只登记攻击事实，不由表现再次扣血。</summary>
        public void RecordExternalAttack(ulong attackerId, Game.MapEngine.Domain.MapPoint target)
        {
            var em = _world.EntityManager;
            em.CompleteAllTrackedJobs();
            if (!_unitEntities.TryGetValue(attackerId, out var entity) || !em.Exists(entity))
                return;
            var vitals = em.GetComponentData<Vitals>(entity);
            if (vitals.Health <= 0)
                return;
            vitals.LastAttackTime = (float)ElapsedTime;
            em.SetComponentData(entity, vitals);
            var ground = em.GetComponentData<GroundTransform>(entity);
            float dx = target.X - ground.Position.x;
            if (math.abs(dx) > 0.0001f)
                ground.FacingX = math.sign(dx);
            em.SetComponentData(entity, ground);
        }

        public void Step(float deltaTime, MoveIntent intent)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Simulation step must be finite and positive.");
            }

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
            UpdateMovingSelection();
        }

        public void CopyUnits(List<UnitSnapshot> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

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
            {
                bool selected = _unitEntities.TryGetValue(identities[i].Id, out var entity) &&
                    _world.EntityManager.HasComponent<SoldierState>(entity) &&
                    _world.EntityManager.GetComponentData<SoldierState>(entity).IsSelected;
                destination.Add(new UnitSnapshot(identities[i].Id, identities[i].Kind, positions[i].Position.x, positions[i].Position.y, positions[i].FacingX, vitals[i].Health, vitals[i].MaxHealth, vitals[i].LastHitTime, vitals[i].LastAttackTime, selected));
            }
        }

        // World 持有原生资源与系统状态，必须显式释放；由 VContainer 作用域管理服务清理。
        public void Dispose()
        {
            if (_world.IsCreated)
                _world.Dispose();
        }
    }
}
