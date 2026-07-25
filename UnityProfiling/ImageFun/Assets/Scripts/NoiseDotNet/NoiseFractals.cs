using System;

namespace NoiseDotNet
{
    public static partial class Noise
    {
        /// <summary>
        /// Fractal 2D gradient noise using multiple octaves. 
        /// </summary>
        /// <param name="xCoords">The x-coordinates of the sample points.</param>
        /// <param name="yCoords">The y-coordinates of the sample points.</param>
        /// <param name="output">The output buffer evaluations are written into.</param>
        /// <param name="settings">Settings for the noise function.</param>
        /// <param name="fractalSettings">Settings for fractal octave evaluation.</param>
        public static void GradientNoise2DFractal(Span<float> xCoords, Span<float> yCoords, Span<float> output, NoiseSettings settings, FractalSettings fractalSettings)
        {
            (int octaves, float persistence, float lacunarity) = fractalSettings;

            float xFreq = settings.XFrequency;
            float yFreq = settings.YFrequency;
            float amplitude = settings.Amplitude;
            int seed = settings.Seed;
            bool accumulate = settings.Accumulate;

            for (int octave = 0; octave < octaves; ++octave)
            {
                // First octave respects settings.Accumulate, subsequent octaves always accumulate.
                GradientNoise2D(xCoords, yCoords, output, new NoiseSettings(xFreq, yFreq, 0f, amplitude, settings.Amplitude2, seed, accumulate));
                accumulate = true;
                seed++;
                xFreq *= lacunarity;
                yFreq *= lacunarity;
                amplitude *= persistence;
            }
        }

        /// <summary>
        /// Fractal 3D gradient noise using multiple octaves. 
        /// </summary>
        /// <param name="xCoords">The x-coordinates of the sample points.</param>
        /// <param name="yCoords">The y-coordinates of the sample points.</param>
        /// <param name="zCoords">The z-coordinates of the sample points.</param>
        /// <param name="output">The output buffer evaluations are written into.</param>
        /// <param name="settings">Settings for the noise function.</param>
        /// <param name="fractalSettings">Settings for fractal octave evaluation.</param>
        public static void GradientNoise3DFractal(Span<float> xCoords, Span<float> yCoords, Span<float> zCoords, Span<float> output, NoiseSettings settings, FractalSettings fractalSettings)
        {
            (int octaves, float persistence, float lacunarity) = fractalSettings;

            float xFreq = settings.XFrequency;
            float yFreq = settings.YFrequency;
            float zFreq = settings.ZFrequency;
            float amplitude = settings.Amplitude;
            int seed = settings.Seed;
            bool accumulate = settings.Accumulate;

            for (int octave = 0; octave < octaves; ++octave)
            {
                // First octave respects settings.Accumulate, subsequent octaves always accumulate.
                GradientNoise3D(xCoords, yCoords, zCoords, output, new NoiseSettings(xFreq, yFreq, zFreq, amplitude, settings.Amplitude2, seed, accumulate));
                accumulate = true;
                seed++;
                xFreq *= lacunarity;
                yFreq *= lacunarity;
                zFreq *= lacunarity;
                amplitude *= persistence;
            }
        }

        /// <summary>
        /// Fractal 2D cellular noise using multiple octaves.
        /// </summary>
        /// <param name="xCoords">The x-coordinates of the sample points.</param>
        /// <param name="yCoords">The y-coordinates of the sample points.</param>
        /// <param name="centerDistOutput">The output buffer cell center distances are written into.</param>
        /// <param name="edgeDistOutput">The output buffer cell edge distances are written into.</param>
        /// <param name="settings">Settings for the noise function.</param>
        /// <param name="fractalSettings">Settings for fractal octave evaluation.</param>
        public static void CellularNoise2DFractal(Span<float> xCoords, Span<float> yCoords, Span<float> centerDistOutput, Span<float> edgeDistOutput, NoiseSettings settings, FractalSettings fractalSettings)
        {
            (int octaves, float persistence, float lacunarity) = fractalSettings;

            float xFreq = settings.XFrequency;
            float yFreq = settings.YFrequency;
            float amplitude = settings.Amplitude;
            float amplitude2 = settings.Amplitude2;
            int seed = settings.Seed;
            bool accumulate = settings.Accumulate;

            for (int octave = 0; octave < octaves; ++octave)
            {
                // First octave respects settings.Accumulate, subsequent octaves always accumulate.
                CellularNoise2D(xCoords, yCoords, centerDistOutput, edgeDistOutput, new NoiseSettings(xFreq, yFreq, 0f, amplitude, amplitude2, seed, accumulate));
                accumulate = true;
                seed++;
                xFreq *= lacunarity;
                yFreq *= lacunarity;
                amplitude *= persistence;
                amplitude2 *= persistence;
            }
        }

        /// <summary>
        /// Fractal 3D cellular noise using multiple octaves.
        /// </summary>
        /// <param name="xCoords">The x-coordinates of the sample points.</param>
        /// <param name="yCoords">The y-coordinates of the sample points.</param>
        /// <param name="zCoords">The z-coordinates of the sample points.</param>
        /// <param name="centerDistOutput">The output buffer cell center distances are written into.</param>
        /// <param name="edgeDistOutput">The output buffer cell edge distances are written into.</param>
        /// <param name="settings">Settings for the noise function.</param>
        /// <param name="fractalSettings">Settings for fractal octave evaluation.</param>
        public static void CellularNoise3DFractal(ReadOnlySpan<float> xCoords, ReadOnlySpan<float> yCoords, ReadOnlySpan<float> zCoords, Span<float> centerDistOutput, Span<float> edgeDistOutput, NoiseSettings settings, FractalSettings fractalSettings)
        {
            (int octaves, float persistence, float lacunarity) = fractalSettings;
            
            float xFreq = settings.XFrequency;
            float yFreq = settings.YFrequency;
            float zFreq = settings.ZFrequency;
            float amplitude = settings.Amplitude;
            float amplitude2 = settings.Amplitude2;
            int seed = settings.Seed;
            bool accumulate = settings.Accumulate;

            for (int octave = 0; octave < octaves; ++octave)
            {
                // First octave respects settings.Accumulate, subsequent octaves always accumulate.
                CellularNoise3D(xCoords, yCoords, zCoords, centerDistOutput, edgeDistOutput, new NoiseSettings(xFreq, yFreq, zFreq, amplitude, amplitude2, seed, accumulate));
                accumulate = true;
                seed++;
                xFreq *= lacunarity;
                yFreq *= lacunarity;
                zFreq *= lacunarity;
                amplitude *= persistence;
                amplitude2 *= persistence;
            }
        }
    }
}
