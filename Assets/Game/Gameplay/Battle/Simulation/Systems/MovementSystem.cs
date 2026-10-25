using Game.Battle.Simulation.Components;
using Game.Battle.Domain.ValueObjects;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Systems
{
    /// <summary>推进主动移动：英雄消费输入，怪物消费路径；后续分离与地形系统约束本步最终位置。</summary>
    [DisableAutoCreation]
    [UpdateAfter(typeof(MonsterNavigationSystem))]
    [BurstCompile]
    public partial struct MovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var settings = SystemAPI.GetSingleton<BattleSettings>();
            state.Dependency = new MovementJob{DeltaTime = SystemAPI.Time.DeltaTime, HalfSize = settings.HalfSize, Center = settings.Center, HeroInput = math.normalizesafe(settings.MoveInput) * math.min(1, math.length(settings.MoveInput))}.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct MovementJob : IJobEntity
        {
            public float DeltaTime;
            public float2 HalfSize;
            public float2 Center;
            public float2 HeroInput;
            // 每个 Job 实例只写当前实体，路径来自导航系统完成的 Buffer。
            private void Execute(ref GroundTransform ground, ref WanderState wander, ref MonsterNavigationState navigation, in DynamicBuffer<NavigationWaypoint> path, in UnitIdentity identity, in UnitMotion motion, in Vitals vitals)
            {
                ground.Previous = ground.Position;
                // 死亡英雄的位置代表幽灵；仍消费移动输入，尸体位置独立保存在复活状态中。
                if (vitals.Health <= 0 && identity.Kind != UnitKind.Hero)
                    return;
                float2 limit = HalfSize - motion.Radius;
                float2 next;
                if (identity.Kind == UnitKind.Hero)
                {
                    next = ground.Position + HeroInput * motion.Speed * DeltaTime;
                }
                else if (navigation.IsChasing != 0)
                {
                    next = FollowPath(ground.Position, limit, Center, motion.Speed * DeltaTime, path, ref navigation);
                }
                else
                {
                    wander.Remaining -= DeltaTime;
                    if (wander.Remaining <= 0)
                    {
                        float angle = wander.Random.NextFloat(0, math.PI * 2);
                        wander.Direction = new float2(math.cos(angle), math.sin(angle));
                        wander.Remaining = wander.Random.NextFloat(0.6f, 2.2f);
                    }

                    next = ground.Position + wander.Direction * motion.Speed * DeltaTime;
                    if (next.x < Center.x - limit.x || next.x > Center.x + limit.x)
                        wander.Direction.x *= -1;
                    if (next.y < Center.y - limit.y || next.y > Center.y + limit.y)
                        wander.Direction.y *= -1;
                }

                ground.Position = math.clamp(next, Center - limit, Center + limit);
                float facing = ground.Position.x - ground.Previous.x;
                if (math.abs(facing) > 0.00001f)
                    ground.FacingX = facing;
            }

            private static float2 FollowPath(float2 position, float2 limit, float2 center, float distanceBudget, in DynamicBuffer<NavigationWaypoint> path, ref MonsterNavigationState navigation)
            {
                // 一步可以走过多个路径点，仍只消费 speed * deltaTime 的距离，避免高速穿点后倒退。
                const float arrivalTolerance = 0.0001f;
                while (distanceBudget > 0 && navigation.WaypointIndex < path.Length)
                {
                    float2 target = math.clamp(path[navigation.WaypointIndex].Position, center - limit, center + limit);
                    float2 offset = target - position;
                    float distance = math.length(offset);
                    bool lastWaypoint = navigation.WaypointIndex == path.Length - 1;
                    if (distance <= arrivalTolerance)
                    {
                        if (lastWaypoint)
                            break;
                        navigation.WaypointIndex++;
                        continue;
                    }

                    float travel = math.min(distance, distanceBudget);
                    position += offset * (travel / distance);
                    distanceBudget -= travel;
                    if (travel < distance || lastWaypoint)
                        break;
                    navigation.WaypointIndex++;
                }

                return position;
            }
        }
    }
}
