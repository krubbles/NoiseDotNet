namespace NoiseDotNet
{
    public static partial class Noise
    {
        public static void GradientNoise2DFractal(Span<float> xCoords, Span<float> yCoords, Span<float> output, in NoiseSettings settings, int octaves, float persistence = 0.5f, float lacunarity = 2f)
        {
            if (octaves <= 0)
                return;

            float xFreq = settings.XFrequency;
            float yFreq = settings.YFrequency;
            float amplitude = settings.Amplitude;
            bool accumulate = settings.Accumulate;

            for (int octave = 0; octave < octaves; ++octave)
            {
                GradientNoise2D(xCoords, yCoords, output, new NoiseSettings(xFreq, yFreq, 0f, amplitude, settings.Amplitude2, settings.Seed, accumulate));
                accumulate = true;
                xFreq *= lacunarity;
                yFreq *= lacunarity;
                amplitude *= persistence;
            }
        }

        public static void GradientNoise3DFractal(Span<float> xCoords, Span<float> yCoords, Span<float> zCoords, Span<float> output, in NoiseSettings settings, int octaves, float persistence = 0.5f, float lacunarity = 2f)
        {
            if (octaves <= 0)
                return;

            float xFreq = settings.XFrequency;
            float yFreq = settings.YFrequency;
            float zFreq = settings.ZFrequency;
            float amplitude = settings.Amplitude;
            bool accumulate = settings.Accumulate;

            for (int octave = 0; octave < octaves; ++octave)
            {
                GradientNoise3D(xCoords, yCoords, zCoords, output, new NoiseSettings(xFreq, yFreq, zFreq, amplitude, settings.Amplitude2, settings.Seed, accumulate));
                accumulate = true;
                xFreq *= lacunarity;
                yFreq *= lacunarity;
                zFreq *= lacunarity;
                amplitude *= persistence;
            }
        }

        public static void CellularNoise2DFractal(Span<float> xCoords, Span<float> yCoords, Span<float> centerDistOutput, Span<float> edgeDistOutput, in NoiseSettings settings, int octaves, float persistence = 0.5f, float lacunarity = 2f)
        {
            if (octaves <= 0)
                return;

            float xFreq = settings.XFrequency;
            float yFreq = settings.YFrequency;
            float amplitude = settings.Amplitude;
            float amplitude2 = settings.Amplitude2;
            bool accumulate = settings.Accumulate;

            for (int octave = 0; octave < octaves; ++octave)
            {
                CellularNoise2D(xCoords, yCoords, centerDistOutput, edgeDistOutput, new NoiseSettings(xFreq, yFreq, 0f, amplitude, amplitude2, settings.Seed, accumulate));
                accumulate = true;
                xFreq *= lacunarity;
                yFreq *= lacunarity;
                amplitude *= persistence;
                amplitude2 *= persistence;
            }
        }

        public static void CellularNoise3DFractal(ReadOnlySpan<float> xCoords, ReadOnlySpan<float> yCoords, ReadOnlySpan<float> zCoords, Span<float> centerDistOutput, Span<float> edgeDistOutput, in NoiseSettings settings, int octaves, float persistence = 0.5f, float lacunarity = 2f)
        {
            if (octaves <= 0)
                return;

            float xFreq = settings.XFrequency;
            float yFreq = settings.YFrequency;
            float zFreq = settings.ZFrequency;
            float amplitude = settings.Amplitude;
            float amplitude2 = settings.Amplitude2;
            bool accumulate = settings.Accumulate;

            for (int octave = 0; octave < octaves; ++octave)
            {
                CellularNoise3D(xCoords, yCoords, zCoords, centerDistOutput, edgeDistOutput, new NoiseSettings(xFreq, yFreq, zFreq, amplitude, amplitude2, settings.Seed, accumulate));
                accumulate = true;
                xFreq *= lacunarity;
                yFreq *= lacunarity;
                zFreq *= lacunarity;
                amplitude *= persistence;
                amplitude2 *= persistence;
            }
        }
    }
}
