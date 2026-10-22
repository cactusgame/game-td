using Game.Battle.Simulation.Components;
using Game.MapEngine.Domain;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Systems
{
    /// <summary>
    /// 移动后、接触结算前裁决地形占位。补足导航缓存以外的英雄、游走和连续移动阻挡。
    /// 主线程查询会完成移动 Job；按细步扫掠，防止一步跨过障碍或斜向穿角。
    /// </summary>
    [DisableAutoCreation]
    [UpdateAfter(typeof(MovementSystem))]
    [UpdateBefore(typeof(CircleContactSystem))]
    public partial class TerrainCollisionSystem : SystemBase
    {
        private GridMap _map;
        public void Configure(GridMap map) => _map = map;

        protected override void OnUpdate()
        {
            if (_map == null)
                return;
            foreach (var (ground, motion) in SystemAPI.Query<RefRW<GroundTransform>, RefRO<UnitMotion>>())
            {
                float2 position = ground.ValueRO.Previous;
                float2 delta = ground.ValueRO.Position - position;
                int steps = math.max(1, (int)math.ceil(math.length(delta) / (math.min(_map.CellWidth, _map.CellHeight) * 0.2f)));
                float2 increment = delta / steps;
                for (int i = 0; i < steps; i++)
                {
                    float2 next = position + increment;
                    if (_map.CanOccupy(next.x, next.y, motion.ValueRO.Radius))
                    {
                        position = next;
                        continue;
                    }
                    if (_map.CanOccupy(next.x, position.y, motion.ValueRO.Radius))
                        position.x = next.x;
                    if (_map.CanOccupy(position.x, next.y, motion.ValueRO.Radius))
                        position.y = next.y;
                }
                ground.ValueRW.Position = position;
            }
        }
    }
}
