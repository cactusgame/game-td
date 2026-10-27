using Game.Battle.Simulation.Components;
using Unity.Entities;

namespace Game.Battle.Simulation.Systems
{
    /// <summary>伤害结束后登记尸体位置，死亡期间位置属于幽灵；到期在幽灵当前位置满血复活并授予短暂无敌。</summary>
    [DisableAutoCreation]
    [UpdateAfter(typeof(StructureCombatSystem))]
    public partial class HeroRespawnSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            double now = SystemAPI.Time.ElapsedTime;
            foreach (var (vitals, ground, respawn) in SystemAPI.Query<RefRW<Vitals>, RefRW<GroundTransform>, RefRW<HeroRespawnState>>().WithAll<HeroTag>())
            {
                if (vitals.ValueRO.Health > 0)
                    continue;
                if (!respawn.ValueRO.IsWaiting)
                {
                    respawn.ValueRW.IsWaiting = true;
                    respawn.ValueRW.DeathPosition = ground.ValueRO.Position;
                    respawn.ValueRW.DeathSequence++;
                    respawn.ValueRW.DeathFacingX = ground.ValueRO.FacingX;
                    respawn.ValueRW.ReadyAt = now + respawn.ValueRO.DelaySeconds;
                    var deadHero = SystemAPI.GetSingletonEntity<HeroTag>();
                    if (EntityManager.HasComponent<UnitStatusState>(deadHero))
                        EntityManager.SetComponentData(deadHero, default(UnitStatusState));
                    continue;
                }
                if (now < respawn.ValueRO.ReadyAt)
                    continue;

                ground.ValueRW.Previous = ground.ValueRO.Position;
                vitals.ValueRW.Health = vitals.ValueRO.MaxHealth;
                vitals.ValueRW.LastHitTime = -100;
                vitals.ValueRW.LastAttackTime = -100;
                respawn.ValueRW.IsWaiting = false;
                respawn.ValueRW.ReadyAt = 0;
                var hero = SystemAPI.GetSingletonEntity<HeroTag>();
                if (EntityManager.HasComponent<UnitStatusState>(hero))
                {
                    var status = new UnitStatusState { InvulnerableUntil = now + respawn.ValueRO.InvulnerabilitySeconds };
                    EntityManager.SetComponentData(hero, status);
                }

                // 复活是新的接触周期；身边仍有怪物时，下一步正常重新判定接触，不沿用死亡前的去重标记。
                foreach (var contact in SystemAPI.Query<RefRW<HeroContact>>().WithAll<MonsterTag>())
                    contact.ValueRW.WasOverlapping = 0;
            }
        }
    }
}
