using Game.Battle.Contracts.ReadModels;
using UnityEngine;

namespace Game.Presentation.Views
{
    /// <summary>固定在死亡位置的英雄尸体。只播放死亡动作并保留末帧，清场由所属单位视图或对局重开负责。</summary>
    public sealed class HeroCorpseView
    {
        private readonly GameObject _root;
        private readonly SpriteRenderer _body;
        private readonly UnitAnimationPlayer _animations;
        private readonly double _startedAt;
        private readonly float _height;
        private readonly float _facing;

        public HeroCorpseView(Transform parent, UnitSnapshot state, UnitAnimationPlayer animations, float height,
            System.Func<float, int> depth, string layer, double now)
        {
            _animations = animations;
            _height = height;
            _facing = state.DeathFacingX;
            _startedAt = now;
            _root = new GameObject("Hero corpse " + state.DeathSequence);
            _root.transform.SetParent(parent, false);
            _root.transform.position = new Vector3(state.DeathX, state.DeathY, 0);
            _body = _root.AddComponent<SpriteRenderer>();
            _body.sortingLayerName = layer;
            _body.sortingOrder = depth(state.DeathY);
            _body.flipX = (_facing < 0) != animations.FacesLeft;
            Render(now);
        }

        public void Render(double now)
        {
            var death = _animations.Require(UnitAction.Death);
            _body.sprite = death.Frame(System.Math.Max(0, now - _startedAt));
            _root.transform.localScale = Vector3.one * (_height / (_body.sprite.bounds.size.y * _animations.DeathBodyHeightFraction));
            if (_animations.UseFallbackDeath)
            {
                float progress = Mathf.Clamp01((float)((now - _startedAt) / death.Duration));
                _root.transform.localRotation = Quaternion.Euler(0, 0, 85 * progress * (_facing < 0 ? 1 : -1));
            }
        }

        public void Destroy() => Object.Destroy(_root);
    }
}
