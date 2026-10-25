using Game.Battle.Contracts.ReadModels;
using Game.Battle.Domain.ValueObjects;
using UnityEngine;

namespace Game.Presentation.Views
{
    /// <summary>一个单位的 Sprite、阴影与血条组合；只有表现状态，不保存第二份玩法生命或位置。</summary>
    public sealed class UnitView
    {
        private readonly GameObject _root;
        private readonly SpriteRenderer _body;
        private readonly SpriteRenderer _shadow;
        private readonly Transform _healthFill;
        private readonly SpriteRenderer _healthRenderer, _healthBackground;
        private readonly float _height;
        private readonly System.Func<float, int> _depth;
        private readonly string _layer;
        private Vector2 _last;
        private float _walkPhase;
        private readonly UnitAnimationPlayer _animations;
        private readonly UnitDeathSettings _deathSettings;
        private double AttackDuration => _animations.Find(UnitAction.Attack)?.Duration ?? 0;
        private double HurtDuration => _animations.Find(UnitAction.Hurt)?.Duration ?? 0;
        private double _deathStart = -1;
        public bool IsReadyForRemoval { get; private set; }

        private double _lastRenderTime = -1, _loopStart;
        private bool _moving;
        private bool _wasGhost;
        private uint _lastDeathSequence;
        private readonly System.Collections.Generic.List<HeroCorpseView> _corpses = new System.Collections.Generic.List<HeroCorpseView>();
        private InvulnerabilityShieldView _shield;
        public UnitView(
            Transform parent, Sprite sprite, Sprite white, Sprite circle,
            float height, float radius, Color teamColor,
            UnitAnimationPlayer animations = null,
            UnitDeathSettings deathSettings = null, System.Func<float, int> depth = null, string layer = null)
        {
            _depth = depth ?? BattleRenderSorting.UnitDepth;
            _layer = layer ?? BattleRenderSorting.UnitLayer;
            _animations = animations;
            _deathSettings = deathSettings;
            if (animations != null)
            {
                sprite = _animations.Require(UnitAction.Idle).Frame(0);
            }
            _height = height;
            _root = new GameObject("UnitView");
            _root.transform.SetParent(parent, false);
            _shadow = Child("GroundShadow", circle, new Color(0.07f, 0.11f, 0.07f, 0.3f));
            _shadow.sortingLayerName = layer ?? BattleRenderSorting.ShadowLayer;
            _shadow.transform.localScale = new Vector3(radius * 2.3f, radius * 0.7f, 1);
            _body = Child("Sprite", sprite, Color.white);
            float scale = height / (sprite.bounds.size.y * (animations != null ? animations.BodyHeightFraction : 1));
            _body.transform.localScale = Vector3.one * scale;
            var back = Child("HealthBackground", white, new Color(0.1f, 0.12f, 0.09f, 1));
            back.transform.localPosition = new Vector3(0, height + 0.1f, 0);
            back.transform.localScale = new Vector3(0.50f, 0.095f, 1);
            var fill = new GameObject("HealthFill").AddComponent<SpriteRenderer>();
            fill.transform.SetParent(back.transform, false);
            fill.sprite = white;
            fill.color = teamColor;
            fill.sortingLayerName = _layer;
            fill.transform.localScale = new Vector3(0.92f, 0.60f, 1);
            _healthFill = fill.transform;
            _healthRenderer = fill;
            _healthBackground = back;
        }

        private SpriteRenderer Child(string name, Sprite sprite, Color color)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(_root.transform, false);
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingLayerName = _layer;
            return renderer;
        }

        // ECS 的 Entity 与这里的 GameObject 是两个对象，通过快照 Id 在 BattleView 中关联。
        public void Render(UnitSnapshot state, double simulationTime, float deltaTime)
        {
            if (state.Kind == UnitKind.Hero && state.DeathSequence > _lastDeathSequence && _animations != null)
            {
                _corpses.Add(new HeroCorpseView(_root.transform.parent, state, _animations, _height, _depth, _layer, simulationTime));
                _lastDeathSequence = state.DeathSequence;
            }
            foreach (var corpse in _corpses) corpse.Render(simulationTime);
            if (state.IsGhost)
            {
                RenderGhost(state, simulationTime);
                return;
            }
            if (_wasGhost)
            {
                _wasGhost = false;
                _shadow.gameObject.SetActive(true);
                _healthBackground.gameObject.SetActive(true);
                _lastRenderTime = -1;
                _moving = false;
                _loopStart = simulationTime;
            }
            RenderShield(state);
            // 复活时间可能短于尸体停留时间；同一视图复用时也必须恢复动作、朝向变换和血条。
            if (state.Kind == UnitKind.Hero && state.Health > 0 && _deathStart >= 0)
            {
                _deathStart = -1;
                IsReadyForRemoval = false;
                _lastRenderTime = -1;
                _moving = false;
                _loopStart = simulationTime;
                _body.transform.localRotation = Quaternion.identity;
                var idle = _animations?.Require(UnitAction.Idle).Frame(0) ?? _body.sprite;
                _body.sprite = idle;
                _body.transform.localScale = Vector3.one * (_height / (idle.bounds.size.y * (_animations?.BodyHeightFraction ?? 1)));
                _healthBackground.gameObject.SetActive(true);
            }
            if (IsReadyForRemoval)
                return;
            if (state.Health <= 0)
            {
                RenderDeath(state, simulationTime);
                return;
            }

            _root.SetActive(true);
            var position = new Vector2(state.X, state.Y);
            // 模拟暂停或本渲染帧没有新步长时，保留上次移动状态，不误切回待机。
            bool moving = _moving;
            if (simulationTime != _lastRenderTime)
            {
                moving = _lastRenderTime >= 0 && (position - _last).sqrMagnitude > 0.000001f;
                _lastRenderTime = simulationTime;
            }

            if (moving != _moving)
                _loopStart = simulationTime;
            _moving = moving;
            _last = position;
            if (moving)
                _walkPhase += deltaTime * 12;
            // 起伏只作用于 Sprite 子对象，逻辑地面位置和碰撞圆不会上下跳动。
            bool staticWalk = _animations == null || _animations.Require(UnitAction.Walk).FrameCount == 1;
            float bob = staticWalk && moving ? Mathf.Abs(Mathf.Sin(_walkPhase)) * 0.045f : 0;
            _root.transform.position = new Vector3(position.x, position.y, 0);
            _body.transform.localPosition = new Vector3(0, bob, 0);
            _body.flipX = (state.FacingX < 0) != (_animations?.FacesLeft ?? false);
            if (_animations != null)
            {
                double attackAge = simulationTime - state.LastAttackTime;
                // 挥击过程中受伤，保留反馈到挥击结束；不只处理两个事件恰好同一时刻的情况。
                double hurtStart = state.LastHitTime;
                double attackEnd = state.LastAttackTime + AttackDuration;
                if (state.LastHitTime <= attackEnd && state.LastHitTime + HurtDuration >= state.LastAttackTime)
                    hurtStart = System.Math.Max(hurtStart, attackEnd);
                double hurtAge = simulationTime - hurtStart;
                Sprite frame;
                if (attackAge >= 0 && attackAge < AttackDuration)
                    frame = _animations.Require(UnitAction.Attack).Frame(attackAge);
                else if (hurtAge >= 0 && hurtAge < HurtDuration)
                {
                    frame = _animations.Require(UnitAction.Hurt).Frame(hurtAge);
                    // 部分怪物素材只有一帧受击姿势。仅移动图像形成短促后仰，地面位置和碰撞不变。
                    float recoil = -Mathf.Sign(state.FacingX) * 0.06f * Mathf.Sin((float)(hurtAge / HurtDuration) * Mathf.PI);
                    _body.transform.localPosition = new Vector3(recoil, bob, 0);
                }
                else
                    frame = _animations.Require(moving ? UnitAction.Walk : UnitAction.Idle).Frame(simulationTime - _loopStart);
                if (frame != null)
                    _body.sprite = frame;
            }

            // 受伤发生时点来自模拟；这里只决定反馈颜色，不通过动画回调决定伤害。
            _body.color = simulationTime - state.LastHitTime < 0.12 ? new Color(1, 0.45f, 0.4f) : Color.white;
            // 用地面 Y 排序，而不是图片中心或建筑视觉高度；靠下的对象显示在前。
            SetDepth(state.Y);
            // 血条只是生命快照的显示，改变长度不会改变生命值。
            float ratio = (float)state.Health / state.MaxHealth;
            _healthFill.localScale = new Vector3(0.92f * ratio, 0.60f, 1);
            _healthFill.localPosition = new Vector3(-0.46f * (1 - ratio), 0, 0);
        }

        private void RenderGhost(UnitSnapshot state, double now)
        {
            _wasGhost = true;
            IsReadyForRemoval = false;
            _root.SetActive(true);
            _root.transform.position = new Vector3(state.X, state.Y, 0);
            _healthBackground.gameObject.SetActive(false);
            _shadow.gameObject.SetActive(false);
            if (_shield != null) _shield.Render(false, _depth(state.Y));
            if (_animations != null)
                _body.sprite = _animations.Require(UnitAction.Idle).Frame(now);
            _body.transform.localScale = Vector3.one * (_height / (_body.sprite.bounds.size.y * (_animations?.BodyHeightFraction ?? 1)));
            _body.transform.localRotation = Quaternion.identity;
            _body.transform.localPosition = new Vector3(0, 0.12f + Mathf.Sin((float)now * 3) * 0.04f, 0);
            _body.flipX = (state.FacingX < 0) != (_animations?.FacesLeft ?? false);
            _body.color = new Color(0.5f, 0.85f, 1, 0.38f);
            SetDepth(state.Y);
        }

        private void RenderShield(UnitSnapshot state)
        {
            if (state.IsInvulnerable && state.Health > 0 && _shield == null)
            {
                var child = new GameObject("Invulnerability shield");
                child.transform.SetParent(_root.transform, false);
                _shield = child.AddComponent<InvulnerabilityShieldView>();
                _shield.Initialize(_height, _layer);
            }
            if (_shield != null) _shield.Render(state.IsInvulnerable && state.Health > 0, _depth(state.Y));
        }

        private void RenderDeath(UnitSnapshot state, double simulationTime)
        {
            if (_deathSettings == null)
                throw new System.InvalidOperationException("Monster death presentation settings are missing.");
            if (_deathStart < 0)
                _deathStart = simulationTime;
            double age = System.Math.Max(0, simulationTime - _deathStart);
            _healthBackground.gameObject.SetActive(false);
            _body.color = Color.white;
            _root.transform.position = new Vector3(state.X, state.Y, 0);
            _body.transform.localPosition = Vector3.zero;
            _body.flipX = (state.FacingX < 0) != (_animations?.FacesLeft ?? false);
            SetDepth(state.Y);
            var death = _animations?.Find(UnitAction.Death);
            if (death != null)
            {
                _body.transform.localScale = Vector3.one * (_height / (death.Frame(0).bounds.size.y * _animations.DeathBodyHeightFraction));
                _body.sprite = death.Frame(age);
                if (_animations.UseFallbackDeath)
                {
                    float progress = Mathf.Clamp01((float)(age / death.Duration));
                    _body.transform.localRotation = Quaternion.Euler(0, 0, 85 * progress * (state.FacingX < 0 ? 1 : -1));
                    _body.transform.localPosition = new Vector3(0, -0.08f * progress, 0);
                }
            }

            // 使用模拟时钟，暂停冻结倒地/停留计时；每次渲染不会重置死亡开始时刻。
            IsReadyForRemoval = age >= (death?.Duration ?? 0) + _deathSettings.Hold;
        }

        public void Destroy()
        {
            foreach (var corpse in _corpses) corpse.Destroy();
            _corpses.Clear();
            Object.Destroy(_root);
        }
        private void SetDepth(float groundY)
        {
            int order = _depth(groundY);
            _body.sortingOrder = order;
            _shadow.sortingOrder = order;
            _healthRenderer.sortingOrder = order + 2;
            _healthBackground.sortingOrder = order + 1;
        }
    }
}
