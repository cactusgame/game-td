using System;
using Game.Battle.Domain;
using UnityEngine;

namespace Game.Presentation.Views
{
    /// <summary>表现边界的 JSON 加载器；每次开局重新读取，不把 Unity 参数带入领域模型。</summary>
    [Serializable]
    public sealed class BattlePresentationSettings
    {
        public int schemaVersion;
        public UnitDeathSettings Death;
        public BattleMapViewSettings Map;
        public UnitAnimationSettings Knight, Goblin, Runner;

        public UnitAnimationSettings AnimationFor(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Hero: return Knight;
                case UnitKind.Goblin: return Goblin;
                case UnitKind.Runner: return Runner;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public static BattlePresentationSettings Parse(string json)
        {
            var data = JsonUtility.FromJson<BattlePresentationSettings>(json);
            if (data == null || data.schemaVersion != 1 || data.Death == null || data.Map == null
                || data.Knight == null || data.Goblin == null || data.Runner == null)
                throw new FormatException("Presentation JSON requires schemaVersion 1, Death, Map, Knight, Goblin and Runner.");
            Positive(data.Death.AnimationSeconds, "Death.AnimationSeconds");
            Nonnegative(data.Death.CorpseHoldSeconds, "Death.CorpseHoldSeconds");
            Positive(data.Map.VisibleWorldHeight, "Map.VisibleWorldHeight");
            Nonnegative(data.Map.EdgeMargin, "Map.EdgeMargin");
            Nonnegative(data.Map.TopEdgeMargin, "Map.TopEdgeMargin");
            Positive(data.Map.BorderThickness, "Map.BorderThickness");
            Positive(data.Map.GroundPatternSize, "Map.GroundPatternSize");
            ValidateColor(data.Map.GroundLight, "Map.GroundLight");
            ValidateColor(data.Map.GroundDark, "Map.GroundDark");
            ValidateColor(data.Map.BorderColor, "Map.BorderColor");
            foreach (var animation in new[] { data.Knight, data.Goblin, data.Runner })
            {
                Positive(animation.IdleFps, "IdleFps");
                Positive(animation.MoveFps, "MoveFps");
                Positive(animation.AttackFps, "AttackFps");
                Positive(animation.HurtFps, "HurtFps");
                Fraction(animation.BodyHeightFraction, "BodyHeightFraction");
                Fraction(animation.DeathBodyHeightFraction, "DeathBodyHeightFraction");
            }
            return data;
        }

        private static void Positive(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
                throw new FormatException(name + " must be finite and positive.");
        }

        private static void Nonnegative(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
                throw new FormatException(name + " must be finite and nonnegative.");
        }

        private static void Fraction(float value, string name)
        {
            Positive(value, name);
            if (value > 1) throw new FormatException(name + " must not exceed 1.");
        }

        private static void ValidateColor(Color color, string name)
        {
            for (int channel = 0; channel < 4; channel++)
            {
                Nonnegative(color[channel], name);
                if (color[channel] > 1) throw new FormatException(name + " channels must not exceed 1.");
            }
        }
    }
}
