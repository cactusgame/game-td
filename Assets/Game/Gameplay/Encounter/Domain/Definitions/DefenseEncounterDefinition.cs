using System;

namespace Game.Encounter.Domain.Definitions
{
    /// <summary>首版两波挑战的只读规则；产品数值由 JSON 加载。</summary>
    public sealed class DefenseEncounterDefinition
    {
        public int FirstWaveCount { get; }
        public int SecondWaveCount { get; }
        public double WaveInterval { get; }
        public DefenseEncounterDefinition(int firstWaveCount = 5, int secondWaveCount = 10, double waveInterval = 10)
        {
            if (firstWaveCount <= 0 || secondWaveCount <= 0 || firstWaveCount > 100000 || secondWaveCount > 100000 || waveInterval <= 0 ||
                double.IsNaN(waveInterval) || double.IsInfinity(waveInterval))
                throw new ArgumentException("Invalid defense encounter rules.");
            FirstWaveCount = firstWaveCount;
            SecondWaveCount = secondWaveCount;
            WaveInterval = waveInterval;
        }
    }
}
