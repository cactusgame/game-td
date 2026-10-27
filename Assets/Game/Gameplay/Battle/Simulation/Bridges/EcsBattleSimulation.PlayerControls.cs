using System.Collections.Generic;
using Game.Battle.Contracts.Commands;
using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.Rules;
using Game.Battle.Domain.ValueObjects;
using Game.Battle.Simulation.Components;
using Game.MapEngine.Domain;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Battle.Simulation.Bridges
{
    public sealed partial class EcsBattleSimulation
    {
        private float SelectionRadius => _definition.SoldierSelectionRadius;
        private bool _selectingWhileMoving;
        private bool _instantSelectionDeployment;
        private readonly double[] _skillReadyAt = new double[2];
        private Unity.Mathematics.Random _callRandom;

        public PlayerControlSnapshot PlayerControls
        {
            get
            {
                _world.EntityManager.CompleteAllTrackedJobs();
                return new PlayerControlSnapshot(_selectingWhileMoving, SelectionRadius, Soldiers(true).Count,
                    math.max(0, _skillReadyAt[0] - ElapsedTime), math.max(0, _skillReadyAt[1] - ElapsedTime));
            }
        }

        private void ResetPlayerControls()
        {
            _selectingWhileMoving = false;
            _instantSelectionDeployment = false;
            _skillReadyAt[0] = _skillReadyAt[1] = 0;
            _callRandom = new Unity.Mathematics.Random(_definition.Seed);
        }

        private SkillCooldownSnapshot CopyHeroSkillCooldown(int slot)
        {
            if (slot >= _definition.Hero.Skills.Count || !_definition.Hero.Skills[slot].Enabled)
                return default;
            return new SkillCooldownSnapshot(_skillReadyAt[slot] - ElapsedTime, _definition.Hero.Skills[slot].Cooldown);
        }

        /// <summary>固定步提交入口。先等待旧 Job，再一次性裁决选择/命令/扣血，随后才运行移动与攻击。</summary>
        public void Execute(PlayerOperation operation, bool instantDeployment = false)
        {
            var em = _world.EntityManager;
            em.CompleteAllTrackedJobs();
            if (operation == PlayerOperation.Cancel)
            {
                ClearSelection();
                return;
            }
            var hero = _unitEntities[1];
            if (em.GetComponentData<Vitals>(hero).Health <= 0)
            {
                ClearSelection();
                return;
            }
            switch (operation)
            {
                case PlayerOperation.SkillOne: CastAreaSkill(0, hero); return;
                case PlayerOperation.SkillTwo: CastAreaSkill(1, hero); return;
                case PlayerOperation.NearbyTap:
                    if (Soldiers(true).Count == 0) SelectNearby(hero, instantDeployment);
                    else
                    {
                        CallSoldiers(Soldiers(true), hero, instantDeployment, followHero: false);
                        _selectingWhileMoving = false;
                    }
                    return;
                case PlayerOperation.NearbyHold:
                    var selected = Soldiers(true);
                    if (selected.Count == 0)
                    {
                        _selectingWhileMoving = true;
                        _instantSelectionDeployment = instantDeployment;
                        SelectNearby(hero, instantDeployment);
                    }
                    else
                    {
                        ClearSelection(station: true);
                    }
                    return;
                case PlayerOperation.NearbyRelease:
                    // 松键只结束范围采样；已经选中的士兵继续跟随。
                    _selectingWhileMoving = false;
                    return;
            }
            bool melee = operation == PlayerOperation.CallOneMelee || operation == PlayerOperation.CallAllMelee;
            bool ranged = operation == PlayerOperation.CallOneRanged || operation == PlayerOperation.CallAllRanged;
            var candidates = Soldiers(false, melee ? UnitKind.Soldier : ranged ? UnitKind.Archer : (UnitKind?)null);
            bool one = operation == PlayerOperation.CallOne || operation == PlayerOperation.CallOneMelee || operation == PlayerOperation.CallOneRanged;
            if (one && candidates.Count > 0)
            {
                // 连续短按逐个招入队伍，避免随机重复呼叫已经选中的成员。
                candidates.RemoveAll(entity => em.GetComponentData<SoldierState>(entity).IsSelected);
                // 先保留未驻扎的可用士兵；全部招入队伍后，后续按键才会进入驻扎候选池。
                bool hasUnstationed = candidates.Exists(entity => em.GetComponentData<SoldierState>(entity).Stance != CombatStance.HoldGround);
                if (hasUnstationed)
                {
                    candidates.RemoveAll(entity => em.GetComponentData<SoldierState>(entity).Stance == CombatStance.HoldGround);
                }
            }
            if (one && candidates.Count > 0)
            {
                var chosen = candidates[_callRandom.NextInt(candidates.Count)];
                candidates.Clear();
                candidates.Add(chosen);
            }
            CallSoldiers(candidates, hero, instantDeployment);
        }

        private List<Entity> Soldiers(bool selectedOnly, UnitKind? kind = null)
        {
            var result = new List<Entity>();
            var em = _world.EntityManager;
            using var query = em.CreateEntityQuery(typeof(SoldierState), typeof(Vitals), typeof(UnitIdentity));
            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                if (em.GetComponentData<Vitals>(entity).Health <= 0 ||
                    (selectedOnly && !em.GetComponentData<SoldierState>(entity).IsSelected) ||
                    (kind.HasValue && em.GetComponentData<UnitIdentity>(entity).Kind != kind.Value)) continue;
                result.Add(entity);
            }
            result.Sort((a, b) => em.GetComponentData<UnitIdentity>(a).Id.CompareTo(em.GetComponentData<UnitIdentity>(b).Id));
            return result;
        }

        private void SelectNearby(Entity hero, bool instantDeployment)
        {
            var em = _world.EntityManager;
            float2 center = em.GetComponentData<GroundTransform>(hero).Position;
            var recruits = new List<Entity>();
            foreach (var entity in Soldiers(false))
            {
                if (em.GetComponentData<SoldierState>(entity).IsSelected) continue;
                if (math.distancesq(center, em.GetComponentData<GroundTransform>(entity).Position) > SelectionRadius * SelectionRadius) continue;
                recruits.Add(entity);
            }
            if (recruits.Count > 0) CallSoldiers(recruits, hero, instantDeployment);
        }

        private void UpdateMovingSelection()
        {
            if (!_selectingWhileMoving) return;
            _world.EntityManager.CompleteAllTrackedJobs();
            if (_world.EntityManager.GetComponentData<Vitals>(_unitEntities[1]).Health <= 0) ClearSelection();
            else SelectNearby(_unitEntities[1], _instantSelectionDeployment);
        }

        private void ClearSelection(bool station = false)
        {
            // 取消选择与停止跟随一起提交，清空旧路径并把当前位置作为新驻守点。
            foreach (var entity in Soldiers(true))
            {
                var position = _world.EntityManager.GetComponentData<GroundTransform>(entity).Position;
                StationSoldier(entity, position, false, station);
            }
            _selectingWhileMoving = false;
        }

        private void CallSoldiers(List<Entity> soldiers, Entity hero, bool instant, bool followHero = true)
        {
            var em = _world.EntityManager;
            var center = em.GetComponentData<GroundTransform>(hero).Position;
            var map = _friendlySpawnMap;
            var pathfinder = new GridPathfinder(map);
            var cells = new List<GridCell>();
            var reserved = new List<(float2 Position, float Radius)>();
            // 所有活单位均参加净空判断，部署时也不能传送到墙内或覆盖尚未移动的单位。
            using var entities = _units.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
                if (em.GetComponentData<Vitals>(entity).Health > 0)
                    reserved.Add((em.GetComponentData<GroundTransform>(entity).Position, em.GetComponentData<UnitMotion>(entity).Radius));
            foreach (var entity in soldiers)
            {
                float radius = em.GetComponentData<UnitMotion>(entity).Radius;
                var position = em.GetComponentData<GroundTransform>(entity).Position;
                // 暂无合法落点时仍加入跟随；后续导航继续尝试接近英雄，不进行非法传送。
                var state = em.GetComponentData<SoldierState>(entity);
                if (state.Command != SoldierCommand.FollowHero)
                    state.FollowOffset = new float2(radius + em.GetComponentData<UnitMotion>(hero).Radius + 0.6f, 0);
                state.IsSelected = followHero;
                state.Command = followHero ? SoldierCommand.FollowHero : SoldierCommand.MoveToPosition;
                // 短按 R 放下队伍：落点在本次命令提交时冻结，之后不再追随英雄。
                // 若暂时没有编队净空，仍寻路接近英雄所在点，禁止强行传送。
                if (!followHero)
                    state.Rally = map.CanOccupy(center.x, center.y, radius) ? center : position;
                state.Stance = CombatStance.Alert;
                state.Behavior = UnitBehavior.Regrouping;
                state.RetaliationTarget = Entity.Null;
                em.SetComponentData(entity, state);
                em.SetComponentData(entity, new CombatTargetState());
                em.GetBuffer<NavigationWaypoint>(entity).Clear();
                em.SetComponentData(entity, new MonsterNavigationState { IsChasing = 1 });
                bool found = false;
                for (int ring = 1; ring <= 12 && !found; ring++)
                {
                    int samples = ring * 8;
                    for (int i = 0; i < samples; i++)
                    {
                        float angle = i * (math.PI * 2 / samples);
                        var candidate = center + new float2(math.cos(angle), math.sin(angle)) * (ring * (radius * 2 + 0.2f));
                        if (!map.CanOccupy(candidate.x, candidate.y, radius)) continue;
                        bool clear = true;
                        foreach (var other in reserved)
                            if (math.distance(candidate, other.Position) < radius + other.Radius + 0.08f) { clear = false; break; }
                        if (!clear) continue;
                        // 即使部署瞬移也只允许同一可达区域，防止从封闭墙内穿越出来。
                        if (!map.TryWorldToCell(position.x, position.y, out var start) ||
                            !map.TryWorldToCell(candidate.x, candidate.y, out var goal) ||
                            !pathfinder.TryFindPath(start, goal, cells)) continue;
                        bool deployInstantly = instant && em.GetComponentData<UnitStatusState>(entity).CanMoveAt(ElapsedTime);
                        StationSoldier(entity, candidate, deployInstantly);
                        state = em.GetComponentData<SoldierState>(entity);
                        state.IsSelected = followHero;
                        state.Command = followHero ? SoldierCommand.FollowHero : SoldierCommand.MoveToPosition;
                        state.Behavior = deployInstantly ? UnitBehavior.Idle : UnitBehavior.Regrouping;
                        state.FollowOffset = candidate - center;
                        em.SetComponentData(entity, state);
                        reserved.Add((candidate, radius));
                        found = true;
                        break;
                    }
                }
            }
        }

        private void StationSoldier(Entity entity, float2 destination, bool instant, bool station = false)
        {
            var em = _world.EntityManager;
            var state = em.GetComponentData<SoldierState>(entity);
            state.Rally = destination;
            state.Command = SoldierCommand.MoveToPosition;
            state.IsSelected = false;
            state.Stance = station ? CombatStance.HoldGround : CombatStance.Alert;
            state.Behavior = UnitBehavior.Idle;
            state.FollowCombatRadius = 0;
            state.RetaliationTarget = Entity.Null;
            em.SetComponentData(entity, state);
            em.SetComponentData(entity, new CombatTargetState());
            em.GetBuffer<NavigationWaypoint>(entity).Clear();
            em.SetComponentData(entity, new MonsterNavigationState { IsChasing = 1 });
            if (!instant || !em.GetComponentData<UnitStatusState>(entity).CanMoveAt(ElapsedTime)) return;
            var ground = em.GetComponentData<GroundTransform>(entity);
            ground.Position = ground.Previous = destination;
            em.SetComponentData(entity, ground);
        }

        private void CastAreaSkill(int slot, Entity hero)
        {
            if (slot >= _definition.Hero.Skills.Count || ElapsedTime < _skillReadyAt[slot]) return;
            var skill = _definition.Hero.Skills[slot];
            if (!skill.Enabled) return;
            var em = _world.EntityManager;
            if (!em.GetComponentData<UnitStatusState>(hero).CanCastAt(ElapsedTime)) return;
            var center = em.GetComponentData<GroundTransform>(hero).Position;
            using var query = em.CreateEntityQuery(typeof(MonsterTag), typeof(Vitals), typeof(GroundTransform));
            using var targets = query.ToEntityArray(Allocator.Temp);
            foreach (var target in targets)
            {
                var vital = em.GetComponentData<Vitals>(target);
                if (em.HasComponent<UnitStatusState>(target) && em.GetComponentData<UnitStatusState>(target).IsInvulnerableAt(ElapsedTime)) continue;
                if (vital.Health <= 0 || math.distancesq(center, em.GetComponentData<GroundTransform>(target).Position) > skill.Radius * skill.Radius) continue;
                // 示例语义为直接减血 y，不再扣防御；只影响存活敌人。
                if (skill.Damage > 0) InterruptHealthRegeneration(target, ref vital);
                vital.Health = ContactRules.ApplyDamage(vital.Health, skill.Damage);
                if (skill.Damage > 0) vital.LastHitTime = (float)ElapsedTime;
                em.SetComponentData(target, vital);
            }
            var heroVitals = em.GetComponentData<Vitals>(hero);
            InterruptHealthRegeneration(hero, ref heroVitals);
            heroVitals.LastAttackTime = (float)ElapsedTime;
            em.SetComponentData(hero, heroVitals);
            _skillReadyAt[slot] = ElapsedTime + skill.Cooldown;
        }
    }
}
