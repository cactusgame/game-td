using Game.Battle.Simulation.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Systems
{
    /// <summary>
    /// 移动后的局部拥挤求解。所有 Job 读取同一位置快照，只写自己的修正结果；
    /// 地形系统随后约束最终位移。网格每步重建，不拥有独立的单位位置真值。
    /// </summary>
    [DisableAutoCreation]
    [UpdateAfter(typeof(MovementSystem))]
    [UpdateBefore(typeof(TerrainCollisionSystem))]
    public partial class MonsterSeparationSystem : SystemBase
    {
        private EntityQuery _monsters;
        private float _gap;
        private float _correctionSpeed;

        protected override void OnCreate()
        {
            _monsters = GetEntityQuery(typeof(MonsterTag), typeof(UnitIdentity), typeof(GroundTransform), typeof(UnitMotion), typeof(Vitals));
        }

        public void Configure(float gap, float correctionSpeed)
        {
            _gap = gap;
            _correctionSpeed = correctionSpeed;
        }

        protected override void OnUpdate()
        {
            // 等待移动 Job；后续数组是本步临时读模型，不向其他单位并行写位置。
            Dependency.Complete();
            using var entities = _monsters.ToEntityArray(Allocator.TempJob);
            if (entities.Length < 2)
                return;
            using var positions = _monsters.ToComponentDataArray<GroundTransform>(Allocator.TempJob);
            using var motions = _monsters.ToComponentDataArray<UnitMotion>(Allocator.TempJob);
            using var health = _monsters.ToComponentDataArray<Vitals>(Allocator.TempJob);
            using var identities = _monsters.ToComponentDataArray<UnitIdentity>(Allocator.TempJob);
            using var corrections = new NativeArray<float2>(entities.Length, Allocator.TempJob);
            float maxRadius = 0;
            foreach (var motion in motions)
                maxRadius = math.max(maxRadius, motion.Radius);
            float cellSize = math.max(0.01f, maxRadius * 2 + _gap);
            using var neighbors = new NativeParallelMultiHashMap<int2, int>(entities.Length, Allocator.TempJob);
            for (int i = 0; i < entities.Length; i++)
            {
                if (health[i].Health > 0)
                    neighbors.Add((int2)math.floor(positions[i].Position / cellSize), i);
            }
            new SeparateJob
            {
                Positions = positions, Motions = motions, Health = health, Identities = identities,
                Neighbors = neighbors, Corrections = corrections, CellSize = cellSize, Gap = _gap,
                MaxCorrection = _correctionSpeed * SystemAPI.Time.DeltaTime
            }.Schedule(entities.Length, 64).Complete();
            for (int i = 0; i < entities.Length; i++)
            {
                var ground = positions[i];
                ground.Position += corrections[i];
                // Previous 保留移动前位置，地形系统对“移动 + 分离”整体做扫掠。
                EntityManager.SetComponentData(entities[i], ground);
            }
        }

        [BurstCompile]
        private struct SeparateJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<GroundTransform> Positions;
            [ReadOnly] public NativeArray<UnitMotion> Motions;
            [ReadOnly] public NativeArray<Vitals> Health;
            [ReadOnly] public NativeArray<UnitIdentity> Identities;
            [ReadOnly] public NativeParallelMultiHashMap<int2, int> Neighbors;
            public NativeArray<float2> Corrections;
            public float CellSize;
            public float Gap;
            public float MaxCorrection;

            public void Execute(int index)
            {
                if (Health[index].Health <= 0)
                    return;
                float2 position = Positions[index].Position;
                int2 cell = (int2)math.floor(position / CellSize);
                float2 correction = float2.zero;
                int contacts = 0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        if (!Neighbors.TryGetFirstValue(cell + new int2(x, y), out int other, out var iterator))
                            continue;
                        do
                        {
                            if (other == index)
                                continue;
                            float2 offset = position - Positions[other].Position;
                            float desired = Motions[index].Radius + Motions[other].Radius + Gap;
                            float distanceSq = math.lengthsq(offset);
                            if (distanceSq >= desired * desired)
                                continue;
                            float distance = math.sqrt(distanceSq);
                            float2 direction;
                            if (distance > 0.0001f)
                                direction = offset / distance;
                            else
                            {
                                // 对同一对 ID 使用同一角度、相反方向，打破完全重合的对称性。
                                ulong first = math.min(Identities[index].Id, Identities[other].Id);
                                ulong second = math.max(Identities[index].Id, Identities[other].Id);
                                uint hash = math.hash(new uint4((uint)first, (uint)(first >> 32), (uint)second, (uint)(second >> 32)));
                                float angle = (hash % 65536) * (math.PI * 2 / 65536);
                                direction = new float2(math.cos(angle), math.sin(angle));
                                if (Identities[index].Id > Identities[other].Id)
                                    direction = -direction;
                            }
                            correction += direction * ((desired - distance) * 0.5f);
                            contacts++;
                        } while (Neighbors.TryGetNextValue(out other, ref iterator));
                    }
                }
                if (contacts > 0)
                {
                    correction /= contacts;
                    float length = math.length(correction);
                    Corrections[index] = correction * math.min(1, MaxCorrection / math.max(0.0001f, length));
                }
            }
        }
    }
}
