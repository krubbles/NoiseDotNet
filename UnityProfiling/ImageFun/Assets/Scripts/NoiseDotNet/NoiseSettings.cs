namespace NoiseDotNet 
{
    /// <summary>
    /// Settings for noise function calls. 
    /// </summary>
    public struct NoiseSettings
    {
        /// <summary>
        /// x-coordinates are multiplied by this number before being passed to the noise function.
        /// </summary>
        public float XFrequency;

        /// <summary>
        /// y-coordinates are multiplied by this number before being passed to the noise function.
        /// </summary>
        public float YFrequency;

        /// <summary>
        /// z-coordinates are multiplied by this number before being passed to the noise function.
        /// Unused by 2D noise functions.
        /// </summary>
        public float ZFrequency;

        /// <summary>
        /// The output of the noise function is multiplied by this number before being written into the output buffer.
        /// For cellular noise functions, this applies to the cell center distance output.
        /// </summary>
        public float Amplitude;

        /// <summary>
        /// The output of the noise function is multiplied by this number before being written into the output buffer.
        /// Not used by gradient noise functions.
        /// For cellular noise functions, this applied to the cell edge distance output.
        /// </summary>
        public float Amplitude2;

        /// <summary>
        /// The seed for the noise function.
        /// </summary>
        public int Seed;

        /// <summary>
        /// If true, the output of the noise function is added to the existing value in the output buffer instead of replacing it.
        /// </summary>
        public bool Accumulate;

        public NoiseSettings(float xFreq, float yFreq, float zFreq, float amplitude, float amplitude2, int seed, bool accumulate = false)
        {
            XFrequency = xFreq;
            YFrequency = yFreq;
            ZFrequency = zFreq;
            Amplitude = amplitude;
            Accumulate = accumulate;
            Amplitude2 = amplitude2;
            Seed = seed;
        }

        public NoiseSettings(float xFreq, float yFreq, float amplitude, float amplitude2, int seed, bool accumulate = false) : this(xFreq, yFreq, 0f, amplitude, amplitude2, seed, accumulate) { }

        public NoiseSettings(float xFreq, float yFreq, int seed, bool accumulate = false) : this(xFreq, yFreq, 0f, 1f, 1f, seed, accumulate) { }

        public NoiseSettings(float xFreq, float yFreq, float zFreq, int seed, bool accumulate = false) : this(xFreq, yFreq, zFreq, 1f, 1f, seed, accumulate)  { }

        public void Deconstruct(out float xFreq, out float yFreq, out float zFreq, out float amplitude, out float amplitude2, out int seed, out bool accumulate)
        {
            xFreq = XFrequency;
            yFreq = YFrequency;
            zFreq = ZFrequency;
            amplitude = Amplitude;
            amplitude2 = Amplitude2;
            seed = Seed;
            accumulate = Accumulate;
        }
    }
}