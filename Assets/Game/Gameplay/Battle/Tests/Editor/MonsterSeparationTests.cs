using Game.Battle.Domain.ValueObjects;
using Game.Battle.Simulation.Components;
using Game.Battle.Simulation.Systems;
using Game.MapEngine.Domain;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Tests
{
    public sealed class MonsterSeparationTests
    {
        private World _world;
        private MonsterSeparationSystem _separation;

        [SetUp]
        public void SetUp()
        {
            _world = new World("Crowd separation tests");
            _separation = _world.GetOrCreateSystemManaged<MonsterSeparationSystem>();
            _separation.Configure(0.12f, 3);
            _world.SetTime(new TimeData(0.02, 0.02f));
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private Entity Spawn(ulong id, float2 position, int health = 10)
        {
            var entity = _world.EntityManager.CreateEntity(typeof(MonsterTag), typeof(UnitIdentity),
                typeof(GroundTransform), typeof(UnitMotion), typeof(Vitals));
            _world.EntityManager.SetComponentData(entity, new UnitIdentity { Id = id, Kind = UnitKind.Goblin });
            _world.EntityManager.SetComponentData(entity, new GroundTransform { Position = position, Previous = position });
            _world.EntityManager.SetComponentData(entity, new UnitMotion { Radius = 0.4f });
            _world.EntityManager.SetComponentData(entity, new Vitals { Health = health });
            return entity;
        }

        private float2 Position(Entity entity) => _world.EntityManager.GetComponentData<GroundTransform>(entity).Position;

        [Test]
        public void ExactOverlapBreaksSymmetryWithBoundedOppositeCorrections()
        {
            var a = Spawn(1, float2.zero);
            var b = Spawn(2, float2.zero);
            _separation.Update();
            Assert.That(math.distance(Position(a), Position(b)), Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(math.length(Position(a) + Position(b)), Is.LessThan(0.0001f));
            Assert.That(math.all(math.isfinite(Position(a))), Is.True);
        }

        [Test]
        public void FiveOverlappingMonstersSpreadWithoutMovingDeadBodies()
        {
            var monsters = new Entity[5];
            for (int i = 0; i < monsters.Length; i++)
                monsters[i] = Spawn((ulong)i + 1, float2.zero);
            var dead = Spawn(100, float2.zero, 0);
            for (int step = 0; step < 150; step++)
                _separation.Update();
            for (int i = 0; i < monsters.Length; i++)
                for (int j = i + 1; j < monsters.Length; j++)
                    Assert.That(math.distance(Position(monsters[i]), Position(monsters[j])), Is.GreaterThan(0.89f));
            Assert.That(Position(dead), Is.EqualTo(float2.zero));
        }

        [Test]
        public void DeadAndDistantMonstersDoNotPushLivingMonster()
        {
            var live = Spawn(1, float2.zero);
            Spawn(2, float2.zero, 0);
            Spawn(3, new float2(8, 8));
            _separation.Update();
            Assert.That(Position(live), Is.EqualTo(float2.zero));
        }

        [Test]
        public void TerrainConstraintPreventsSeparationFromPushingThroughWall()
        {
            var walkable = new bool[64];
            for (int i = 0; i < walkable.Length; i++)
                walkable[i] = i % 8 != 4;
            var map = new GridMap(8, 8, 1, 1, -4, -4, walkable);
            var collision = _world.GetOrCreateSystemManaged<TerrainCollisionSystem>();
            collision.Configure(map);
            var a = Spawn(1, new float2(-0.41f, 0.5f));
            Spawn(2, new float2(-0.42f, 0.5f));
            for (int step = 0; step < 60; step++)
            {
                using var query = _world.EntityManager.CreateEntityQuery(typeof(GroundTransform));
                using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
                foreach (var entity in entities)
                {
                    var ground = _world.EntityManager.GetComponentData<GroundTransform>(entity);
                    ground.Previous = ground.Position;
                    _world.EntityManager.SetComponentData(entity, ground);
                }
                _separation.Update();
                collision.Update();
                Assert.That(map.CanOccupy(Position(a).x, Position(a).y, 0.4f), Is.True);
                Assert.That(Position(a).x, Is.LessThanOrEqualTo(-0.4f));
            }
        }
    }
}
