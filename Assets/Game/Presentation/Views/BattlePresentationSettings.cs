using System;
using Game.Battle.Domain.ValueObjects;
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
        public UnitAnimationSettings[] Units;

        public UnitAnimationSettings AnimationFor(UnitKind kind)
        {
            foreach (var unit in Units)
            {
                if (unit.UnitKind == kind.ToString())
                    return unit;
            }
            throw new FormatException("Missing animation profile for unit kind: " + kind);
        }

        public static BattlePresentationSettings Parse(string json)
        {
            var data = JsonUtility.FromJson<BattlePresentationSettings>(json);
            if (data == null || data.schemaVersion != 3 || data.Death == null || data.Map == null || data.Units == null || data.Units.Length == 0)
                throw new FormatException("Presentation JSON requires schemaVersion 3, Death, Map and Units.");
            Nonnegative(data.Death.CorpseHoldSeconds, "Death.CorpseHoldSeconds");
            Positive(data.Map.VisibleWorldHeight, "Map.VisibleWorldHeight");
            Nonnegative(data.Map.EdgeMargin, "Map.EdgeMargin");
            Nonnegative(data.Map.TopEdgeMargin, "Map.TopEdgeMargin");
            Positive(data.Map.BorderThickness, "Map.BorderThickness");
            Positive(data.Map.GroundPatternSize, "Map.GroundPatternSize");
            ValidateColor(data.Map.GroundLight, "Map.GroundLight");
            ValidateColor(data.Map.GroundDark, "Map.GroundDark");
            ValidateColor(data.Map.BorderColor, "Map.BorderColor");
            var kinds = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var animation in data.Units)
            {
                if (animation == null || !Enum.TryParse<UnitKind>(animation.UnitKind, out var kind) ||
                    !Enum.IsDefined(typeof(UnitKind), kind) || animation.UnitKind != kind.ToString() || !kinds.Add(animation.UnitKind) ||
                    string.IsNullOrWhiteSpace(animation.ResourceId))
                    throw new FormatException("Unit visuals require unique known UnitKind and ResourceId.");
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

        private static void ValidateColor(Color color, string name)
        {
            for (int channel = 0; channel < 4; channel++)
            {
                Nonnegative(color[channel], name);
                if (color[channel] > 1)
                    throw new FormatException(name + " channels must not exceed 1.");
            }
        }
    }
}
