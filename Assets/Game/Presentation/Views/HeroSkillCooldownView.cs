using Game.Battle.Contracts.ReadModels;
using UnityEngine;

namespace Game.Presentation.Views
{
    /// <summary>英雄血条上方的两个技能冷却条。短按青色、长按金色；只读取冷却快照，结束后独立隐藏。</summary>
    public sealed class HeroSkillCooldownView
    {
        private readonly Bar _first;
        private readonly Bar _second;
        private const float BarHeight = 0.065f;
        private readonly float _healthBarY;
        private readonly float _healthBarHeight;

        public HeroSkillCooldownView(Transform parent, Sprite white, float healthBarY, float healthBarHeight, string layer)
        {
            _healthBarY = healthBarY;
            _healthBarHeight = healthBarHeight;
            _first = new Bar(parent, "Skill one cooldown", white, new Color(0.3f, 0.75f, 1), layer);
            _second = new Bar(parent, "Skill two cooldown", white, new Color(1, 0.78f, 0.25f), layer);
        }

        public void Render(UnitSnapshot state, int depth, bool showHealthBar)
        {
            bool alive = state.Health > 0 && !state.IsGhost;
            // 只为可见条分配高度：边缘相接，隐藏血条时以其原中心作为第一条冷却条的位置。
            float nextY = showHealthBar ? _healthBarY + (_healthBarHeight + BarHeight) * 0.5f : _healthBarY;
            _first.Render(state.SkillOneCooldown, alive, depth, nextY);
            if (state.SkillOneCooldown.IsCoolingDown) nextY += BarHeight;
            _second.Render(state.SkillTwoCooldown, alive, depth, nextY);
        }

        private sealed class Bar
        {
            private readonly GameObject _root;
            private readonly SpriteRenderer _background;
            private readonly SpriteRenderer _fill;

            public Bar(Transform parent, string name, Sprite white, Color color, string layer)
            {
                _root = new GameObject(name);
                _root.transform.SetParent(parent, false);
                _root.transform.localScale = new Vector3(0.5f, BarHeight, 1);
                _background = _root.AddComponent<SpriteRenderer>();
                _background.sprite = white;
                _background.color = new Color(0.08f, 0.1f, 0.14f, 0.95f);
                _background.sortingLayerName = layer;
                var fillObject = new GameObject("Fill");
                fillObject.transform.SetParent(_root.transform, false);
                _fill = fillObject.AddComponent<SpriteRenderer>();
                _fill.sprite = white;
                _fill.color = color;
                _fill.sortingLayerName = layer;
                _root.SetActive(false);
            }

            public void Render(SkillCooldownSnapshot cooldown, bool alive, int depth, float y)
            {
                _root.SetActive(alive && cooldown.IsCoolingDown);
                if (!_root.activeSelf) return;
                _root.transform.localPosition = new Vector3(0, y, 0);
                _background.sortingOrder = depth + 4;
                _fill.sortingOrder = depth + 5;
                float progress = cooldown.Progress;
                _fill.transform.localScale = new Vector3(0.92f * progress, 0.6f, 1);
                _fill.transform.localPosition = new Vector3(-0.46f * (1 - progress), 0, 0);
            }
        }
    }
}
