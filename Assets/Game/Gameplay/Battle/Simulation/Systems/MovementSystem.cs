using Game.Battle.Domain;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation
{
    // 禁止默认启动流程自动创建，避免手动模拟和默认 PlayerLoop 重复驱动同一套玩法。
    [DisableAutoCreation]
    // Burst 优化可编译的计算，不会自动创建线程；并行来自下面的 ScheduleParallel。
    [BurstCompile]
    // partial 允许 Entities 源代码生成器补充查询和 Job 调度代码；ISystem 本身不是 MonoBehaviour。
    public partial struct MovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 取得当前 World 唯一匹配的 BattleSettings 副本；不存在或不唯一时此调用不成立。
            // Singleton API 不自动完成冲突 Job；当前设置仅在 Step 写入，移动 Job 使用下面复制出的字段。
            var settings = SystemAPI.GetSingleton<BattleSettings>();
            // 右侧 state.Dependency 是此前必要任务的完成凭据；返回的 JobHandle 再写回左侧，
            // 让 ECS 能安排后续组件读写依赖。调度后 OnUpdate 可以返回，Execute 可能仍在工作线程运行。
            // ScheduleParallel 将匹配数据分批调度，不是每个单位创建一个线程，也不在此等待全部完成。
            state.Dependency = new MovementJob
            {
                // 使用 Step 传给 World 的固定模拟时间，而不是从 Job 内读取画面帧时。
                DeltaTime = SystemAPI.Time.DeltaTime,
                HalfSize = settings.HalfSize,
                // 保留模拟摇杆力度，同时限制最大长度，防止对角输入让英雄移动得更快。
                HeroInput = math.normalizesafe(settings.MoveInput) * math.min(1, math.length(settings.MoveInput))
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct MovementJob : IJobEntity
        {
            public float DeltaTime;
            public float2 HalfSize, HeroInput;
            // Execute 参数决定默认查询：实体必须同时具有这五种组件，所以英雄和怪物都会匹配。
            // ref 是组件可读写引用，直接修改真实数据，无需 SetComponentData；in 声明只读访问。
            // Unity 根据这些访问权限安排依赖。每次 Execute 处理一个实体，不在这里访问 GameObject 或 UI。
            private void Execute(ref GroundTransform ground, ref WanderState wander,
                in UnitIdentity identity, in UnitMotion motion, in Vitals vitals)
            {
                // 保存本步起点，接触系统用起点与终点判断是否中途穿过目标。
                ground.Previous = ground.Position;
                if (vitals.Health <= 0) return;
                float2 direction = HeroInput;
                // 英雄使用输入方向；怪物使用自己的随机方向，而非另一份 Monster 对象状态。
                if (identity.Kind != UnitKind.Hero)
                {
                    wander.Remaining -= DeltaTime;
                    if (wander.Remaining <= 0)
                    {
                        // Random 是每个实体自己的可变结构体；ref wander 使新随机状态保留到下一步。
                        float angle = wander.Random.NextFloat(0, math.PI * 2);
                        wander.Direction = new float2(math.cos(angle), math.sin(angle));
                        wander.Remaining = wander.Random.NextFloat(0.6f, 2.2f);
                    }
                    direction = wander.Direction;
                }
                // 位置 += 方向 × 每秒速度 × 本步秒数。边界扣除半径，让整个逻辑圆留在地图内。
                float2 limit = HalfSize - motion.Radius;
                float2 next = ground.Position + direction * motion.Speed * DeltaTime;
                if (identity.Kind != UnitKind.Hero)
                {
                    if (next.x < -limit.x || next.x > limit.x) wander.Direction.x *= -1;
                    if (next.y < -limit.y || next.y > limit.y) wander.Direction.y *= -1;
                }
                // 这是写入单位真实位置的地方；后续快照再把位置交给画面 Transform 显示。
                ground.Position = math.clamp(next, -limit, limit);
                if (math.abs(direction.x) > 0.01f) ground.FacingX = direction.x;
            }
        }
    }
}
