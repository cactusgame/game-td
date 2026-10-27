using Game.Battle.Domain.Rules;
using Game.Battle.Domain.ValueObjects;
using Game.Battle.Simulation.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Systems
{
    /// <summary>英雄先完成一次普通攻击，再按各怪物的共享冷却结算反击。持续贴身也按攻击间隔推进。</summary>
    [DisableAutoCreation]
    [UpdateAfter(typeof(MovementSystem))]
    [BurstCompile]
    public partial struct CircleContactSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            Entity hero = SystemAPI.GetSingletonEntity<HeroTag>();
            var heroVital = SystemAPI.GetComponent<Vitals>(hero);
            if (heroVital.Health <= 0) return;
            var heroGround = SystemAPI.GetComponent<GroundTransform>(hero);
            var heroMotion = SystemAPI.GetComponent<UnitMotion>(hero);
            var heroProfile = SystemAPI.HasComponent<CombatProfile>(hero) ? SystemAPI.GetComponent<CombatProfile>(hero) : default;
            var heroStatus = SystemAPI.HasComponent<UnitStatusState>(hero) ? SystemAPI.GetComponent<UnitStatusState>(hero) : default;
            double now = SystemAPI.Time.ElapsedTime;
            int attacks = 0;
            Entity heroTarget = SystemAPI.HasComponent<CombatTargetState>(hero)
                ? SystemAPI.GetComponent<CombatTargetState>(hero).Target : Entity.Null;
            float nearest = float.PositiveInfinity;
            Entity chosen = Entity.Null;
            foreach (var (ground, motion, vital, monster) in
                SystemAPI.Query<RefRO<GroundTransform>, RefRO<UnitMotion>, RefRO<Vitals>>().WithAll<MonsterTag>().WithEntityAccess())
            {
                float reach = heroMotion.Radius + motion.ValueRO.Radius + heroProfile.AttackRange;
                if (vital.ValueRO.Health <= 0 || !Touches(heroGround, ground.ValueRO, reach)) continue;
                if (monster == heroTarget)
                {
                    chosen = monster;
                    break;
                }
                float distance = math.distancesq(heroGround.Position, ground.ValueRO.Position);
                if (distance > nearest || (distance == nearest && chosen != Entity.Null && monster.Index > chosen.Index)) continue;
                nearest = distance;
                chosen = monster;
            }
            heroTarget = chosen;
            if (SystemAPI.HasComponent<CombatTargetState>(hero))
                SystemAPI.SetComponent(hero, new CombatTargetState { Target = heroTarget });
            if (heroTarget != Entity.Null && heroStatus.CanAttackAt(now) && NormalAttackRules.IsReady(now, heroVital.NextNormalAttackAt))
            {
                var targetVital = SystemAPI.GetComponent<Vitals>(heroTarget);
                var targetStatus = SystemAPI.HasComponent<UnitStatusState>(heroTarget)
                    ? SystemAPI.GetComponent<UnitStatusState>(heroTarget) : default;
                int damage = targetStatus.IsInvulnerableAt(now) ? 0 : ContactRules.Damage(heroVital.Attack, targetVital.Defense);
                targetVital.Health = ContactRules.ApplyDamage(targetVital.Health, damage);
                if (damage > 0) targetVital.LastHitTime = (float)now;
                SystemAPI.SetComponent(heroTarget, targetVital);
                heroVital.LastAttackTime = (float)now;
                heroVital.NextNormalAttackAt = NormalAttackRules.NextAttackAt(now, heroProfile.AttackInterval);
                attacks++;
            }
            // 英雄的致命攻击先提交。被击杀怪物不会在同一步追加反击。
            foreach (var (ground, motion, vital, contact, monster) in
                SystemAPI.Query<RefRW<GroundTransform>, RefRO<UnitMotion>, RefRW<Vitals>, RefRW<HeroContact>>().WithAll<MonsterTag>().WithEntityAccess())
            {
                var profile = SystemAPI.HasComponent<CombatProfile>(monster) ? SystemAPI.GetComponent<CombatProfile>(monster) : default;
                var status = SystemAPI.HasComponent<UnitStatusState>(monster) ? SystemAPI.GetComponent<UnitStatusState>(monster) : default;
                Entity target = SystemAPI.HasComponent<CombatTargetState>(monster)
                    ? SystemAPI.GetComponent<CombatTargetState>(monster).Target : Entity.Null;
                float reach = heroMotion.Radius + motion.ValueRO.Radius + profile.AttackRange;
                bool canEngage = vital.ValueRO.Health > 0 && heroVital.Health > 0 &&
                    (target == hero || target == Entity.Null) && Touches(heroGround, ground.ValueRO, reach);
                if (!canEngage)
                {
                    contact.ValueRW.IsEngaged = false;
                    continue;
                }
                if (target == Entity.Null && SystemAPI.HasComponent<CombatTargetState>(monster))
                    SystemAPI.SetComponent(monster, new CombatTargetState { Target = hero });
                if (!status.CanAttackAt(now)) continue;
                if (!contact.ValueRO.IsEngaged)
                {
                    contact.ValueRW.IsEngaged = true;
                    vital.ValueRW.NextNormalAttackAt = math.max(vital.ValueRO.NextNormalAttackAt,
                        NormalAttackRules.NextAttackAt(now, profile.AttackInterval));
                    continue;
                }
                if (!NormalAttackRules.IsReady(now, vital.ValueRO.NextNormalAttackAt)) continue;
                int damage = heroStatus.IsInvulnerableAt(now) ? 0 : ContactRules.Damage(vital.ValueRO.Attack, heroVital.Defense);
                heroVital.Health = ContactRules.ApplyDamage(heroVital.Health, damage);
                if (damage > 0) heroVital.LastHitTime = (float)now;
                vital.ValueRW.LastAttackTime = (float)now;
                vital.ValueRW.NextNormalAttackAt = NormalAttackRules.NextAttackAt(now, profile.AttackInterval);
                float facing = heroGround.Position.x - ground.ValueRO.Position.x;
                if (math.abs(facing) > 0.0001f) ground.ValueRW.FacingX = math.sign(facing);
                if (SystemAPI.HasComponent<MonsterNavigationState>(monster))
                {
                    var navigation = SystemAPI.GetComponent<MonsterNavigationState>(monster);
                    navigation.Behavior = UnitBehavior.Attacking;
                    SystemAPI.SetComponent(monster, navigation);
                }
                attacks++;
            }
            SystemAPI.SetComponent(hero, heroVital);
            SystemAPI.GetSingletonRW<ContactStatistics>().ValueRW.Count += attacks;
        }

        private static bool Touches(GroundTransform hero, GroundTransform monster, float reach)
        {
            return ContactRules.SweptCircles(monster.Previous.x - hero.Previous.x, monster.Previous.y - hero.Previous.y,
                monster.Position.x - hero.Position.x, monster.Position.y - hero.Position.y, reach);
        }
    }
}
