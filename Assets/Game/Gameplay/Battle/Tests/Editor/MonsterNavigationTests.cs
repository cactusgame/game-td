using Game.Battle.Domain;
using Game.Battle.Simulation;
using Game.MapEngine.Domain;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Tests
{
    /// <summary>独立 World 验证实际感知、路径 Buffer、移动 Job 和固定步顺序。</summary>
    public sealed class MonsterNavigationTests
    {
        private World _world;
        private BattleSystemGroup _group;
        private Entity _hero;
        private double _time;

        [SetUp] public void SetUp()
        {
            _time = 0;
            _world = new World("Monster navigation tests");
            _group = _world.GetOrCreateSystemManaged<BattleSystemGroup>();
            var navigation = _world.GetOrCreateSystemManaged<MonsterNavigationSystem>();
            navigation.Configure(GridMap.CreateOpenArena(20, 20, 1), new NavigationDefinition(1, 5, 0.25f));
            _group.AddSystemToUpdateList(navigation);
            _group.AddSystemToUpdateList(_world.GetOrCreateSystem<MovementSystem>());
            _group.SortSystems();
            var settings = _world.EntityManager.CreateEntity(typeof(BattleSettings));
            _world.EntityManager.SetComponentData(settings, new BattleSettings { HalfSize = new float2(10, 10) });
            _hero = Spawn(UnitKind.Hero, float2.zero, 1);
        }

        [TearDown] public void TearDown() => _world.Dispose();

        private Entity Spawn(UnitKind kind, float2 position, uint seed)
        {
            var em = _world.EntityManager;
            var entity = em.CreateEntity(typeof(UnitIdentity), typeof(GroundTransform), typeof(Vitals),
                typeof(UnitMotion), typeof(WanderState), typeof(MonsterNavigationState));
            em.AddBuffer<NavigationWaypoint>(entity);
            if (kind == UnitKind.Hero) em.AddComponent<HeroTag>(entity);
            else em.AddComponent<MonsterTag>(entity);
            em.SetComponentData(entity, new UnitIdentity { Id = seed, Kind = kind });
            em.SetComponentData(entity, new GroundTransform { Position = position, Previous = position, FacingX = 1 });
            em.SetComponentData(entity, new Vitals { Health = 10, MaxHealth = 10 });
            em.SetComponentData(entity, new UnitMotion { Speed = 1, Radius = 0.1f });
            em.SetComponentData(entity, new WanderState { Random = new Random(seed) });
            return entity;
        }

        private void Step()
        {
            _time += 0.02;
            _world.SetTime(new TimeData(_time, 0.02f));
            _group.Update();
            _world.EntityManager.CompleteAllTrackedJobs();
        }

        private void Move(Entity entity, float2 position)
        {
            var ground = _world.EntityManager.GetComponentData<GroundTransform>(entity);
            ground.Position = ground.Previous = position;
            _world.EntityManager.SetComponentData(entity, ground);
        }

        private MonsterNavigationState Navigation(Entity entity)
            => _world.EntityManager.GetComponentData<MonsterNavigationState>(entity);
        private DynamicBuffer<NavigationWaypoint> Path(Entity entity)
            => _world.EntityManager.GetBuffer<NavigationWaypoint>(entity);
        private float2 Position(Entity entity)
            => _world.EntityManager.GetComponentData<GroundTransform>(entity).Position;

        [Test] public void EachMonsterUsesItsOwnPerceptionAndPath()
        {
            var near = Spawn(UnitKind.Goblin, new float2(3, 0), 2);
            var far = Spawn(UnitKind.Runner, new float2(8, 0), 3);
            Step();
            Assert.That(Navigation(near).IsChasing, Is.EqualTo(1));
            Assert.That(Navigation(far).IsChasing, Is.Zero);
            Assert.That(Path(near).Length, Is.GreaterThan(0));
            Assert.That(Path(far).Length, Is.Zero);
            Assert.That(Position(near).x, Is.LessThan(3));

            Move(far, new float2(-3, 0));
            Step();
            Assert.That(Navigation(far).IsChasing, Is.EqualTo(1));
            var farWaypoint = Path(far)[0].Position;
            var nearPath = Path(near);
            nearPath.Clear();
            Assert.That(Path(far)[0].Position, Is.EqualTo(farWaypoint));
        }

        [Test] public void LeavingRangeCancelsImmediatelyAndReentryDoesNotWaitForRepathTimer()
        {
            var monster = Spawn(UnitKind.Goblin, new float2(3, 0), 2);
            Step();
            double nextRepath = Navigation(monster).NextRepathTime;
            Move(_hero, new float2(-4, 0));
            Step();
            Assert.That(_time, Is.LessThan(nextRepath));
            Assert.That(Navigation(monster).IsChasing, Is.Zero);
            Assert.That(Path(monster).Length, Is.Zero);
            Assert.That(_world.EntityManager.GetComponentData<WanderState>(monster).Remaining, Is.GreaterThan(0));
            Move(_hero, new float2(1, 0));
            Step();
            Assert.That(Navigation(monster).IsChasing, Is.EqualTo(1));
            Assert.That(Path(monster).Length, Is.GreaterThan(0));
        }

        [Test] public void SeparateReleaseRadiusKeepsExistingChaseButDoesNotAcquireOutsideDetection()
        {
            _world.GetExistingSystemManaged<MonsterNavigationSystem>().Configure(
                GridMap.CreateOpenArena(20, 20, 1), new NavigationDefinition(1, 5, 0.25f, 7));
            var monster = Spawn(UnitKind.Goblin, new float2(6, 0), 2);
            Step();
            Assert.That(Navigation(monster).IsChasing, Is.Zero);
            Move(monster, new float2(5, 0));
            Step();
            Assert.That(Navigation(monster).IsChasing, Is.EqualTo(1));
            Move(monster, new float2(7, 0));
            Step();
            Assert.That(Navigation(monster).IsChasing, Is.EqualTo(1));
            Move(monster, new float2(7.01f, 0));
            Step();
            Assert.That(Navigation(monster).IsChasing, Is.Zero);
            Assert.That(Path(monster).Length, Is.Zero);
            Move(monster, new float2(6, 0));
            Step();
            Assert.That(Navigation(monster).IsChasing, Is.Zero);
        }

        [Test] public void ExactRangeBoundaryIsIncluded()
        {
            var monster = Spawn(UnitKind.Goblin, new float2(5, 0), 2);
            Step();
            Assert.That(Navigation(monster).IsChasing, Is.EqualTo(1));
            Move(monster, new float2(5.01f, 0));
            Step();
            Assert.That(Navigation(monster).IsChasing, Is.Zero);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DeadMonsterOrDeadHeroEndsPursuit(bool killMonster)
        {
            var monster = Spawn(UnitKind.Runner, new float2(3, 0), 2);
            Step();
            float2 before = Position(monster);
            Entity victim = killMonster ? monster : _hero;
            var vitals = _world.EntityManager.GetComponentData<Vitals>(victim);
            vitals.Health = 0;
            _world.EntityManager.SetComponentData(victim, vitals);
            Step();
            Assert.That(Navigation(monster).IsChasing, Is.Zero);
            Assert.That(Path(monster).Length, Is.Zero);
            if (killMonster) Assert.That(Position(monster), Is.EqualTo(before));
        }

        [Test] public void HeroMovementInsideCellUpdatesEndpointWithoutReplanning()
        {
            Move(_hero, new float2(0.1f, 0.1f));
            var monster = Spawn(UnitKind.Goblin, new float2(3, 0.1f), 2);
            Step();
            double nextRepath = Navigation(monster).NextRepathTime;
            Move(_hero, new float2(0.2f, 0.1f));
            Step();
            Assert.That(Navigation(monster).NextRepathTime, Is.EqualTo(nextRepath));
            var path = Path(monster);
            Assert.That(path[path.Length - 1].Position, Is.EqualTo(Position(_hero)));
        }

        [Test] public void MovingHeroToAnotherCellReplansAtConfiguredInterval()
        {
            var monster = Spawn(UnitKind.Goblin, new float2(3, 0), 2);
            Step();
            int2 firstGoal = Navigation(monster).GoalCell;
            Move(_hero, new float2(0, 2));
            Step();
            Assert.That(Navigation(monster).GoalCell, Is.EqualTo(firstGoal));
            while (_time < 0.3) Step();
            Assert.That(Navigation(monster).GoalCell, Is.EqualTo(new int2(10, 12)));
        }

        [Test] public void SameCellPursuitReachesExactHeroPositionWithoutOvershoot()
        {
            Move(_hero, new float2(0.1f, 0.1f));
            var monster = Spawn(UnitKind.Runner, new float2(0.8f, 0.1f), 2);
            for (int i = 0; i < 60; i++) Step();
            Assert.That(math.distance(Position(monster), Position(_hero)), Is.LessThan(0.0001f));
            Assert.That(Navigation(monster).IsChasing, Is.EqualTo(1));
        }

        [Test] public void FastMovementConsumesMultipleWaypointsWithoutExceedingSpeed()
        {
            var monster = Spawn(UnitKind.Runner, new float2(3, 0), 2);
            var motion = _world.EntityManager.GetComponentData<UnitMotion>(monster);
            motion.Speed = 100;
            _world.EntityManager.SetComponentData(monster, motion);
            float2 previous = Position(monster);
            Step();
            Assert.That(math.distance(Position(monster), previous), Is.LessThanOrEqualTo(2.0001f));
            Step();
            Assert.That(math.distance(Position(monster), Position(_hero)), Is.LessThan(0.0001f));
        }
    }
}
