using Game.Battle.Contracts;
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
        public UnitView(Transform parent, Sprite sprite, Sprite white, Sprite circle,
            float height, float radius, Color teamColor)
        {
            _height = height;
            _root = new GameObject("UnitView"); _root.transform.SetParent(parent, false);
            var shadow = Child("GroundShadow", circle, new Color(0.07f, 0.11f, 0.07f, 0.3f));
            shadow.transform.localScale = new Vector3(radius * 2.3f, radius * 0.7f, 1);
            _body = Child("Sprite", sprite, Color.white);
            float scale = height / sprite.bounds.size.y;
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
            // 隐藏视图不会销毁 ECS 实体；原型的死亡实体保留到重启。
            _root.SetActive(state.Health > 0);
            if (state.Health <= 0) return;
            var position = new Vector2(state.X, state.Y);
            bool moving = (position - _last).sqrMagnitude > 0.000001f;
            _last = position;
            if (moving) _walkPhase += deltaTime * 12;
            // 起伏只作用于 Sprite 子对象，逻辑地面位置和碰撞圆不会上下跳动。
            float bob = moving ? Mathf.Abs(Mathf.Sin(_walkPhase)) * 0.045f : 0;
            _root.transform.position = new Vector3(position.x, position.y, 0);
            _body.transform.localPosition = new Vector3(0, bob, 0);
            _body.flipX = state.FacingX < 0;
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
    }
}
