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

        public NoiseSettings(float xFrequency, float yFrequency, float zFrequency, float amplitude, float amplitude2, int seed)
        {
            XFrequency = xFrequency;
            YFrequency = yFrequency;
            ZFrequency = zFrequency;
            Amplitude = amplitude;
            Amplitude2 = amplitude2;
            Seed = seed;
        }

        public NoiseSettings(float xFrequency, float yFrequency, float amplitude, float amplitude2, int seed) : this(xFrequency, yFrequency, 0f, amplitude, amplitude2, seed) { }

        public NoiseSettings(float xFrequency, float yFrequency, int seed) : this(xFrequency, yFrequency, 0f, 1f, 1f, seed) { }

        public NoiseSettings(float xFrequency, float yFrequency, float zFrequency, int seed) : this(xFrequency, yFrequency, zFrequency, 1f, 1f, seed)  { }

        public void Deconstruct(out float xFrequency, out float yFrequency, out float zFrequency, out float amplitude, out float amplitude2, out int seed)
        {
            xFrequency = XFrequency;
            yFrequency = YFrequency;
            zFrequency = ZFrequency;
            amplitude = Amplitude;
            amplitude2 = Amplitude2;
            seed = Seed;
        }
    }
}