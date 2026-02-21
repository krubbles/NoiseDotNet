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
        /// </summary>
        public float Amplitude;

        /// <summary>
        /// The seed for the noise function.
        /// </summary>
        public int Seed;
    }
}