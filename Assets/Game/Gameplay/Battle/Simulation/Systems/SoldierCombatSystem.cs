using Game.Battle.Domain.Rules;
using Game.Battle.Simulation.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Systems
{
    /// <summary>固定步内顺序结算士兵攻击和怪物近身反击。生命、攻击/受击时点只写 ECS。</summary>
    [DisableAutoCreation]
    [UpdateAfter(typeof(CircleContactSystem))]
    [UpdateBefore(typeof(StructureCombatSystem))]
    public partial class SoldierCombatSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            double now = SystemAPI.Time.ElapsedTime;
            foreach (var (ground, health, motion, profile) in
                SystemAPI.Query<RefRW<GroundTransform>, RefRW<Vitals>, RefRO<UnitMotion>, RefRO<CombatProfile>>().WithAll<SoldierState>())
            {
                if (health.ValueRO.Health <= 0 || now < health.ValueRO.LastAttackTime + profile.ValueRO.AttackInterval) continue;
                Entity target = Entity.Null;
                float nearest = float.PositiveInfinity;
                foreach (var (other, vital, otherMotion, entity) in SystemAPI.Query<RefRO<GroundTransform>, RefRO<Vitals>, RefRO<UnitMotion>>().WithAll<MonsterTag>().WithEntityAccess())
                {
                    float distance = math.distancesq(ground.ValueRO.Position, other.ValueRO.Position);
                    float reach = motion.ValueRO.Radius + otherMotion.ValueRO.Radius + profile.ValueRO.AttackRange;
                    if (vital.ValueRO.Health <= 0 || distance > reach * reach || distance >= nearest) continue;
                    nearest = distance;
                    target = entity;
                }
                if (target == Entity.Null) continue;
                Strike(target, health.ValueRO.Attack, now);
                health.ValueRW.LastAttackTime = (float)now;
                Face(ref ground.ValueRW, EntityManager.GetComponentData<GroundTransform>(target).Position);
            }
            foreach (var (ground, health, motion, profile) in
                SystemAPI.Query<RefRW<GroundTransform>, RefRW<Vitals>, RefRO<UnitMotion>, RefRO<CombatProfile>>().WithAll<MonsterTag>())
            {
                if (health.ValueRO.Health <= 0 || now < health.ValueRO.LastAttackTime + profile.ValueRO.AttackInterval) continue;
                Entity target = Entity.Null;
                float nearest = float.PositiveInfinity;
                foreach (var (other, vital, otherMotion, entity) in SystemAPI.Query<RefRO<GroundTransform>, RefRO<Vitals>, RefRO<UnitMotion>>().WithAll<SoldierState>().WithEntityAccess())
                {
                    float distance = math.distancesq(ground.ValueRO.Position, other.ValueRO.Position);
                    float reach = motion.ValueRO.Radius + otherMotion.ValueRO.Radius + profile.ValueRO.AttackRange;
                    if (vital.ValueRO.Health <= 0 || distance > reach * reach || distance >= nearest) continue;
                    nearest = distance;
                    target = entity;
                }
                if (target == Entity.Null) continue;
                Strike(target, health.ValueRO.Attack, now);
                health.ValueRW.LastAttackTime = (float)now;
                Face(ref ground.ValueRW, EntityManager.GetComponentData<GroundTransform>(target).Position);
            }
        }
        private void Strike(Entity target, int attack, double now)
        {
            var vital = EntityManager.GetComponentData<Vitals>(target);
            int damage = ContactRules.Damage(attack, vital.Defense);
            vital.Health = ContactRules.ApplyDamage(vital.Health, damage);
            if (damage > 0) vital.LastHitTime = (float)now;
            EntityManager.SetComponentData(target, vital);
        }
        private static void Face(ref GroundTransform ground, float2 target)
        {
            float x = target.x - ground.Position.x;
            if (math.abs(x) > 0.0001f) ground.FacingX = math.sign(x);
        }
    }
}
