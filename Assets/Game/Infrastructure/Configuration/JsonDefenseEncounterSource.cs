using System;
using Game.Encounter.Domain.Definitions;
using UnityEngine;

namespace Game.Infrastructure.Configuration
{
    public static class JsonDefenseEncounterSource
    {
        public static DefenseEncounterDefinition Parse(string json)
        {
            var document = JsonUtility.FromJson<Document>(json);
            if (document == null || document.schemaVersion != 1)
                throw new FormatException("Invalid defense encounter configuration.");
            return new DefenseEncounterDefinition(document.firstWaveCount, document.secondWaveCount, document.waveInterval);
        }
        [Serializable]
        private sealed class Document
        {
            public int schemaVersion;
            public int firstWaveCount;
            public int secondWaveCount;
            public double waveInterval;
        }
    }
}
