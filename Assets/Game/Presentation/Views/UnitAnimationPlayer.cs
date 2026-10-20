using System;
using System.Collections.Generic;

namespace Game.Presentation.Views
{
    /// <summary>只读的标准动作库，完全从素材资产解析；每类素材共享，不参与玩法裁决。</summary>
    public sealed class UnitAnimationPlayer
    {
        private readonly Dictionary<UnitAction, UnitAnimationClip> _clips = new Dictionary<UnitAction, UnitAnimationClip>();
        public float BodyHeightFraction { get; }
        public float DeathBodyHeightFraction { get; }
        public bool FacesLeft { get; }

        public UnitAnimationPlayer(UnitAnimationSet resources)
        {
            if (resources == null)
                throw new ArgumentNullException(nameof(resources));
            ValidateFraction(resources.BodyHeightFraction, nameof(resources.BodyHeightFraction));
            ValidateFraction(resources.DeathBodyHeightFraction, nameof(resources.DeathBodyHeightFraction));
            BodyHeightFraction = resources.BodyHeightFraction;
            DeathBodyHeightFraction = resources.DeathBodyHeightFraction;
            FacesLeft = resources.FacesLeft;
            foreach (UnitAction action in Enum.GetValues(typeof(UnitAction)))
            {
                var sequence = resources.GetSequence(action);
                if (sequence == null || sequence.IsEmpty)
                    continue;
                _clips.Add(action, new UnitAnimationClip(resources, sequence));
            }
            Require(UnitAction.Idle);
            Require(UnitAction.Walk);
            Require(UnitAction.Attack);
        }

        public UnitAnimationClip Find(UnitAction action)
        {
            return _clips.TryGetValue(action, out var clip) ? clip : null;
        }

        public UnitAnimationClip Require(UnitAction action)
        {
            return Find(action) ?? throw new FormatException("Missing required animation action: " + action);
        }

        private static void ValidateFraction(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0 || value > 1)
                throw new FormatException(name + " must be finite and within (0, 1].");
        }
    }
}
