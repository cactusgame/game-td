using Game.Battle.Simulation.Components;
using Game.Battle.Domain.Rules;
using Game.Battle.Domain.ValueObjects;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Systems
{
    // 单英雄对 N 个怪物扫描为 O(N)，当前没有怪物之间的两两检测。
    // 接触检测与结算集中于此；以后替换检测实现时，应用层和表现层契约可以保持稳定。
    [DisableAutoCreation]
    // 只规定同组系统的 OnUpdate 调用顺序，不保证移动 Job 此时已完成；数据依赖另外负责同步。
    [UpdateAfter(typeof(MovementSystem))]
    [BurstCompile]
    public partial struct CircleContactSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 单英雄假设：取得当前 World 唯一带 HeroTag 的实体。Entity 是句柄，不是英雄对象。
            Entity hero = SystemAPI.GetSingletonEntity<HeroTag>();
            // GetComponent 返回组件副本；下面修改 heroVitals 后，必须在循环结束时显式写回。
            var heroVitals = SystemAPI.GetComponent<Vitals>(hero);
            if (heroVitals.Health <= 0)
                return;
            // 普通 SystemAPI.GetComponent 会完成所需的冲突依赖，确保读到移动 Job 更新后的位置。
            var heroGround = SystemAPI.GetComponent<GroundTransform>(hero);
            var heroMotion = SystemAPI.GetComponent<UnitMotion>(hero);
            int heroDamage = 0, contacts = 0;
            float now = (float)SystemAPI.Time.ElapsedTime;
            var heroStatus = SystemAPI.HasComponent<UnitStatusState>(hero)
                ? SystemAPI.GetComponent<UnitStatusState>(hero) : default;
            bool heroInvulnerable = heroStatus.IsInvulnerableAt(SystemAPI.Time.ElapsedTime);
            bool heroCanAttack = heroStatus.CanAttackAt(SystemAPI.Time.ElapsedTime);
            // Query 要求同时具有四种组件，再用 WithAll 额外要求 MonsterTag。
            // RefRO/ValueRO 只读；RefRW/ValueRW 可直接修改 ECS 数据，不需要另行 SetComponent。
            // 此 foreach 在主线程遍历；源代码生成器缓存查询并在遍历前完成必要的数据依赖。
            foreach (var(ground, motion, vitals, contact, monster)in SystemAPI.Query<RefRW<GroundTransform>, RefRO<UnitMotion>, RefRW<Vitals>, RefRW<HeroContact>>().WithAll<MonsterTag>().WithEntityAccess())
            {
                if (vitals.ValueRO.Health <= 0)
                    continue;
                // 两个圆是否接触可转成相对位置是否落入“两个半径之和”的范围。
                float radius = heroMotion.Radius + motion.ValueRO.Radius;
                float2 current = ground.ValueRO.Position - heroGround.Position;
                float2 previous = ground.ValueRO.Previous - heroGround.Previous;
                bool overlaps = math.lengthsq(current) <= radius * radius;
                // 保护结束时重新判定持续接触，避免怪物贴身时永久漏掉伤害。
                if (contact.ValueRO.WasHeroInvulnerable != 0 && !heroInvulnerable)
                    contact.ValueRW.WasOverlapping = 0;
                contact.ValueRW.WasHeroInvulnerable = heroInvulnerable ? (byte)1 : (byte)0;
                // 上一步未重叠且本步路径发生接触才扣血；持续重叠不重复扣血。
                // 终点分离时将状态归零，下一步允许重新接触；扫掠检测也覆盖高速中途穿过。
                bool hit = contact.ValueRO.WasOverlapping == 0 && ContactRules.SweptCircles(previous.x, previous.y, current.x, current.y, radius);
                var monsterStatus = SystemAPI.HasComponent<UnitStatusState>(monster)
                    ? SystemAPI.GetComponent<UnitStatusState>(monster) : default;
                bool monsterCanAttack = monsterStatus.CanAttackAt(SystemAPI.Time.ElapsedTime);
                bool heroHit = heroCanAttack && (hit || (overlaps && contact.ValueRO.WasHeroAttackBlocked != 0));
                bool monsterHit = monsterCanAttack && (hit || (overlaps && contact.ValueRO.WasMonsterAttackBlocked != 0));
                contact.ValueRW.WasHeroAttackBlocked = heroCanAttack ? (byte)0 : (byte)1;
                contact.ValueRW.WasMonsterAttackBlocked = monsterCanAttack ? (byte)0 : (byte)1;
                contact.ValueRW.WasOverlapping = overlaps ? (byte)1 : (byte)0;
                if (!heroHit && !monsterHit)
                    continue;
                // 攻击事件独立于实际伤害：被防御抵消时也需要挥击动作。
                if (heroHit)
                    heroVitals.LastAttackTime = now;
                if (monsterHit)
                    vitals.ValueRW.LastAttackTime = now;
                if (monsterHit && SystemAPI.HasComponent<MonsterNavigationState>(monster))
                {
                    var navigation = SystemAPI.GetComponent<MonsterNavigationState>(monster);
                    navigation.Behavior = UnitBehavior.Attacking;
                    SystemAPI.SetComponent(monster, navigation);
                }
                float facing = heroGround.Position.x - ground.ValueRO.Position.x;
                if (monsterHit && math.abs(facing) > 0.0001f)
                    ground.ValueRW.FacingX = math.sign(facing);
                // Domain 只计算伤害数值，当前系统决定何时计算并更新组件。
                // 双方根据接触处理前的属性互相攻击，即使怪物本次被击杀，仍结算它的这次攻击。
                int damage = heroHit ? ContactRules.Damage(heroVitals.Attack, vitals.ValueRO.Defense) : 0;
                int incoming = monsterHit ? ContactRules.Damage(vitals.ValueRO.Attack, heroVitals.Defense) : 0;
                if (heroInvulnerable) incoming = 0;
                if (monsterStatus.IsInvulnerableAt(SystemAPI.Time.ElapsedTime))
                    damage = 0;
                // 累加伤害最多记到英雄当前生命，防止多次极端伤害造成整数溢出。
                heroDamage = incoming >= heroVitals.Health - heroDamage ? heroVitals.Health : heroDamage + incoming;
                // 怪物通过组件引用直接扣血，Health 为零后不再移动或参与接触；实体保留至重启。
                vitals.ValueRW.Health = ContactRules.ApplyDamage(vitals.ValueRO.Health, damage);
                if (damage > 0)
                    vitals.ValueRW.LastHitTime = now;
                contacts++;
            }

            heroVitals.Health = ContactRules.ApplyDamage(heroVitals.Health, heroDamage);
            if (heroDamage > 0)
                heroVitals.LastHitTime = now;
            // 英雄此前读到的是副本，这一步才写回真实组件；与上面怪物的 ValueRW 写法不同。
            SystemAPI.SetComponent(hero, heroVitals);
            // 统计是 Singleton 可写引用，直接更新。当前没有后台 Job 读写它；Singleton API 本身不自动等 Job。
            SystemAPI.GetSingletonRW<ContactStatistics>().ValueRW.Count += contacts;
        }
    }
}
