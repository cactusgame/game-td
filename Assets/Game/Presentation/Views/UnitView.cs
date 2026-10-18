using Game.Battle.Contracts;
using Game.Battle.Domain;
using UnityEngine;

namespace Game.Presentation.Views
{
    /// <summary>一个单位的 Sprite、阴影与血条组合；只有表现状态，不保存第二份玩法生命或位置。</summary>
    public sealed class UnitView
    {
        private readonly GameObject _root;
        private readonly SpriteRenderer _body;
        private readonly Transform _healthFill;
        private readonly SpriteRenderer _healthRenderer, _healthBackground;
        private readonly float _height;
        private Vector2 _last;
        private float _walkPhase;
        private readonly UnitAnimationSet _animations;
        private readonly UnitDeathSettings _deathSettings;
        private double _deathStart = -1;
        public bool ReadyForRemoval { get; private set; }
        private double _lastRenderTime = -1, _loopStart;
        private bool _moving;
        public UnitView(Transform parent, Sprite sprite, Sprite white, Sprite circle,
            float height, float radius, Color teamColor, UnitAnimationSet animations = null,
            UnitDeathSettings deathSettings = null)
        {
            _animations = animations;
            _deathSettings = deathSettings;
            if (animations != null && animations.Idle.Length > 0) sprite = animations.Idle[0];
            _height = height;
            _root = new GameObject("UnitView"); _root.transform.SetParent(parent, false);
            var shadow = Child("GroundShadow", circle, new Color(0.07f, 0.11f, 0.07f, 0.3f));
            shadow.transform.localScale = new Vector3(radius * 2.3f, radius * 0.7f, 1);
            _body = Child("Sprite", sprite, Color.white);
            float scale = height / (sprite.bounds.size.y * (animations != null ? animations.BodyHeightFraction : 1));
            _body.transform.localScale = Vector3.one * scale;
            var back = Child("HealthBackground", white, new Color(0.1f, 0.12f, 0.09f, 1));
            back.transform.localPosition = new Vector3(0, height + 0.1f, 0);
            back.transform.localScale = new Vector3(0.72f, 0.095f, 1);
            var fill = new GameObject("HealthFill").AddComponent<SpriteRenderer>();
            fill.transform.SetParent(back.transform, false);
            fill.sprite = white; fill.color = teamColor;
            fill.transform.localScale = new Vector3(0.92f, 0.60f, 1);
            _healthFill = fill.transform;
            _healthRenderer = fill; _healthBackground = back;
        }

        private SpriteRenderer Child(string name, Sprite sprite, Color color)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(_root.transform, false);
            renderer.sprite = sprite; renderer.color = color;
            return renderer;
        }

        // ECS 的 Entity 与这里的 GameObject 是两个对象，通过快照 Id 在 BattleView 中关联。
        public void Render(UnitSnapshot state, double simulationTime, float deltaTime)
        {
            if (ReadyForRemoval) return;
            if (state.Health <= 0)
            {
                // 英雄保留现有隐藏行为；怪物死亡表现完成后由 BattleView 销毁视图。
                if (state.Kind == UnitKind.Hero) { _root.SetActive(false); return; }
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
            if (moving != _moving) _loopStart = simulationTime;
            _moving = moving;
            _last = position;
            if (moving) _walkPhase += deltaTime * 12;
            // 起伏只作用于 Sprite 子对象，逻辑地面位置和碰撞圆不会上下跳动。
            float bob = _animations == null && moving ? Mathf.Abs(Mathf.Sin(_walkPhase)) * 0.045f : 0;
            _root.transform.position = new Vector3(position.x, position.y, 0);
            _body.transform.localPosition = new Vector3(0, bob, 0);
            _body.flipX = state.FacingX < 0;
            if (_animations != null)
            {
                double hitAge = simulationTime - state.LastHitTime;
                double attackAge = simulationTime - state.LastAttackTime;
                // 同次接触同时挥击和受伤：先播完整挥击，再接受伤反馈，避免某一类动作永久被遮蔽。
                double hurtDelay = state.LastAttackTime == state.LastHitTime ? _animations.AttackDuration : 0;
                Sprite frame;
                if (attackAge >= 0 && attackAge < _animations.AttackDuration)
                    frame = _animations.Frame(_animations.Attack, attackAge, _animations.AttackFps, false);
                else if (hitAge >= hurtDelay && hitAge < hurtDelay + _animations.HurtDuration)
                    frame = _animations.Frame(_animations.Hurt, hitAge - hurtDelay, _animations.HurtFps, false);
                else
                    frame = _animations.Frame(moving ? _animations.Move : _animations.Idle,
                        simulationTime - _loopStart, moving ? _animations.MoveFps : _animations.IdleFps, true);
                if (frame != null) _body.sprite = frame;
            }
            // 受伤发生时点来自模拟；这里只决定反馈颜色，不通过动画回调决定伤害。
            _body.color = simulationTime - state.LastHitTime < 0.12 ? new Color(1, 0.45f, 0.4f) : Color.white;
            // 用地面 Y 排序，而不是图片中心或建筑视觉高度；靠下的对象显示在前。
            int order = 1000 - Mathf.RoundToInt(state.Y * 50);
            _body.sortingOrder = order;
            _healthRenderer.sortingOrder = order + 2;
            _healthBackground.sortingOrder = order + 1;
            // 血条只是生命快照的显示，改变长度不会改变生命值。
            float ratio = (float)state.Health / state.MaxHealth;
            _healthFill.localScale = new Vector3(0.92f * ratio, 0.60f, 1);
            _healthFill.localPosition = new Vector3(-0.46f * (1 - ratio), 0, 0);
        }

        private void RenderDeath(UnitSnapshot state, double simulationTime)
        {
            if (_deathSettings == null)
                throw new System.InvalidOperationException("Monster death presentation settings are missing.");
            if (_deathStart < 0) _deathStart = simulationTime;
            double age = System.Math.Max(0, simulationTime - _deathStart);
            _healthBackground.gameObject.SetActive(false);
            _body.color = Color.white;
            _root.transform.position = new Vector3(state.X, state.Y, 0);
            _body.transform.localPosition = Vector3.zero;
            _body.flipX = state.FacingX < 0;
            _body.sortingOrder = 1000 - Mathf.RoundToInt(state.Y * 50);
            if (_animations != null && _animations.Death != null && _animations.Death.Length > 0)
            {
                _body.transform.localScale = Vector3.one * (_height /
                    (_animations.Death[0].bounds.size.y * _animations.DeathBodyHeightFraction));
                float fps = _animations.Death.Length / _deathSettings.Duration;
                _body.sprite = _animations.Frame(_animations.Death, age, fps, false);
            }
            // 使用模拟时钟，暂停冻结倒地/停留计时；每次渲染不会重置死亡开始时刻。
            ReadyForRemoval = age >= _deathSettings.Duration + _deathSettings.Hold;
        }

        public void Destroy() => Object.Destroy(_root);
    }
}
