namespace NoiseDotNet
{
    /// <summary>
    /// Settings for fractal noise function calls.
    /// </summary>
    public struct FractalSettings
    {
        /// <summary>
        /// The number of octaves to evaluate.
        /// </summary>
        public int Octaves;

        /// <summary>
        /// Amplitude multiplier applied after each octave.
        /// </summary>
        public float Persistence;

        /// <summary>
        /// Frequency multiplier applied after each octave.
        /// </summary>
        public float Lacunarity;

        public FractalSettings(int octaves, float persistence = 0.5f, float lacunarity = 2f)
        {
            Octaves = octaves;
            Persistence = persistence;
            Lacunarity = lacunarity;
        }

        public void Deconstruct(out int octaves, out float persistence, out float lacunarity)
        {
            octaves = Octaves;
            persistence = Persistence;
            lacunarity = Lacunarity;
        }
    }
}
