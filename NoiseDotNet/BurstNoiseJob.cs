
#if UNITY_2017_1_OR_NEWER

using Unity.Burst;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using System;

namespace NoiseDotNet
{
    public enum NoiseType
    {
        GradientNoise2D,
        GradientNoise3D,
        CellularNoise2D,
        CellularNoise3D
    }
    
    /// <summary>
    /// Burst Job for evaluating noise functions from the <see cref="Noise"/> class.
    /// Used by the functions in the <see cref="Noise"/> class internally, however if you want to run the job asynchronously you can use this struct.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    public unsafe struct BurstNoiseJob : IJob
    {
        public NoiseType noiseType;

        public NoiseSettings noiseSettings;


        [NoAlias]
        [NativeDisableUnsafePtrRestriction]
        public float* xBuffer, yBuffer, zBuffer, output1Buffer, output2Buffer;
        public int length;

        public unsafe void Execute()
        {
            switch (noiseType)
            {
                case NoiseType.GradientNoise2D:
                    Noise.GradientNoise2DBurst(xBuffer, yBuffer, output1Buffer, length, noiseSettings);
                    break;
                case NoiseType.GradientNoise3D:
                    Noise.GradientNoise3DBurst(xBuffer, yBuffer, zBuffer, output1Buffer, length, noiseSettings);
                    break;
                case NoiseType.CellularNoise2D:
                    Noise.CellularNoise2DBurst(xBuffer, yBuffer, output1Buffer, output2Buffer, length, noiseSettings);
                    break;
                case NoiseType.CellularNoise3D:
                    Noise.CellularNoise3DBurst(xBuffer, yBuffer, zBuffer, output1Buffer, output2Buffer, length, noiseSettings);
                    break;
            }
        }

        public static void RunGradientNoise2DJob(ReadOnlySpan<float> x, ReadOnlySpan<float> y, Span<float> output, in NoiseSettings settings)
        {
            CreateGradientNoise2DJob(x, y, output, settings).Run();
        }
    
        public static BurstNoiseJob CreateGradientNoise2DJob(ReadOnlySpan<float> x, ReadOnlySpan<float> y, Span<float> output, in NoiseSettings settings)
        {
            if (output.Length == 0)
                throw new ArgumentException($"Output buffer length was 0. Expected > 0.");
            if (output.Length != x.Length)
                throw new ArgumentException($"Expected x buffer length {x.Length} to equal output buffer length {output.Length}");
            if (output.Length != y.Length)
                throw new ArgumentException($"Expected y buffer length {y.Length} to equal output buffer length {output.Length}");

            fixed (float* xPtr = x)
            {
                fixed (float* yPtr = y)
                {
                    fixed (float* outPtr = output)
                    {
                        BurstNoiseJob job = new()
                        {
                            noiseType = NoiseType.GradientNoise2D,
                            xBuffer = xPtr,
                            yBuffer = yPtr,
                            output1Buffer = outPtr,
                            length = x.Length,
                            noiseSettings = settings
                        };
                        return job;
                    }
                }
            }
        }

        public static void RunGradientNoise3DJob(ReadOnlySpan<float> x, ReadOnlySpan<float> y, ReadOnlySpan<float> z, Span<float> output, in NoiseSettings settings)
        {
            CreateGradientNoise3DJob(x, y, z, output, settings).Run();
        }
        
        public static BurstNoiseJob CreateGradientNoise3DJob(ReadOnlySpan<float> x, ReadOnlySpan<float> y, ReadOnlySpan<float> z, Span<float> output, in NoiseSettings settings)
        {
            if (output.Length == 0)
                throw new ArgumentException($"Output buffer length was 0. Expected > 0.");
            if (output.Length != x.Length)
                throw new ArgumentException($"Expected x buffer length {x.Length} to equal output buffer length {output.Length}");
            if (output.Length != y.Length)
                throw new ArgumentException($"Expected y buffer length {y.Length} to equal output buffer length {output.Length}");
            if (output.Length != z.Length)
                throw new ArgumentException($"Expected z buffer length {z.Length} to equal output buffer length {output.Length}");

            fixed (float* xPtr = x)
            {
                fixed (float* yPtr = y)
                {
                    fixed (float* zPtr = z)
                    {
                        fixed (float* outPtr = output)
                        {
                            BurstNoiseJob job = new()
                            {
                                noiseType = NoiseType.GradientNoise3D,
                                xBuffer = xPtr,
                                yBuffer = yPtr,
                                zBuffer = zPtr,
                                output1Buffer = outPtr,
                                length = x.Length,
                                noiseSettings = settings
                            };
                            return job;
                        }
                    }
                }
            }
        }

        public static void RunCellularNoise2DJob(ReadOnlySpan<float> x, ReadOnlySpan<float> y,
            Span<float> centerDistOutput, Span<float> edgeDistOutput, in NoiseSettings settings)
        {
            CreateCellularNoise2DJob(x, y, centerDistOutput, edgeDistOutput, settings).Run();
        }

        public static BurstNoiseJob CreateCellularNoise2DJob(ReadOnlySpan<float> x, ReadOnlySpan<float> y, Span<float> centerDistOutput, Span<float> edgeDistOutput, in NoiseSettings settings)
        {
            if (centerDistOutput.Length != edgeDistOutput.Length)
                throw new ArgumentException($"Expected center dist output buffer length {centerDistOutput.Length} to equal edge dist output buffer length {edgeDistOutput.Length}");
            if (centerDistOutput.Length == 0)
                throw new ArgumentException($"Output buffer length was 0. Expected > 0.");
            if (centerDistOutput.Length != x.Length)
                throw new ArgumentException($"Expected x buffer length {x.Length} to equal output buffer length {centerDistOutput.Length}");
            if (centerDistOutput.Length != y.Length)
                throw new ArgumentException($"Expected y buffer length {y.Length} to equal output buffer length {centerDistOutput.Length}");

            fixed (float* xPtr = x)
            {
                fixed (float* yPtr = y)
                {
                    fixed (float* centerOutPtr = centerDistOutput)
                    {
                        fixed (float* edgeOutPtr = edgeDistOutput)
                        {
                            BurstNoiseJob job = new()
                            {
                                noiseType = NoiseType.CellularNoise2D,
                                xBuffer = xPtr,
                                yBuffer = yPtr,
                                output1Buffer = centerOutPtr,
                                output2Buffer = edgeOutPtr,
                                length = centerDistOutput.Length,
                                noiseSettings = settings
                            };
                            return job;
                        }
                    }
                }
            }
        }

        public static void RunCellularNoise3DJob(ReadOnlySpan<float> x, ReadOnlySpan<float> y, ReadOnlySpan<float> z,
            Span<float> centerDistOutput, Span<float> edgeDistOutput, in NoiseSettings settings)
        {
            CreateCellularNoise3DJob(x, y, z, centerDistOutput, edgeDistOutput, settings).Run();
        }

        public static BurstNoiseJob CreateCellularNoise3DJob(ReadOnlySpan<float> x, ReadOnlySpan<float> y, ReadOnlySpan<float> z, Span<float> centerDistOutput, Span<float> edgeDistOutput, in NoiseSettings settings)
        {
            if (centerDistOutput.Length != edgeDistOutput.Length)
                throw new ArgumentException($"Expected center dist output buffer length {centerDistOutput.Length} to equal edge dist output buffer length {edgeDistOutput.Length}");
            if (centerDistOutput.Length == 0)
                throw new ArgumentException($"Output buffer length was 0. Expected > 0.");
            if (centerDistOutput.Length != x.Length)
                throw new ArgumentException($"Expected x buffer length {x.Length} to equal output buffer length {centerDistOutput.Length}");
            if (centerDistOutput.Length != y.Length)
                throw new ArgumentException($"Expected y buffer length {y.Length} to equal output buffer length {centerDistOutput.Length}");
            if (centerDistOutput.Length != z.Length)
                throw new ArgumentException($"Expected z buffer length {z.Length} to equal output buffer length {centerDistOutput.Length}");

            fixed (float* xPtr = x)
            {
                fixed (float* yPtr = y)
                {
                    fixed (float* zPtr = z)
                    {
                        fixed (float* centerOutPtr = centerDistOutput)
                        {
                            fixed (float* edgeOutPtr = edgeDistOutput)
                            {
                                BurstNoiseJob job = new()
                                {
                                    noiseType = NoiseType.CellularNoise3D,
                                    xBuffer = xPtr,
                                    yBuffer = yPtr,
                                    zBuffer = zPtr,
                                    output1Buffer = centerOutPtr,
                                    output2Buffer = edgeOutPtr,
                                    length = centerDistOutput.Length,
                                    noiseSettings = settings
                                };
                                return job;
                            }
                        }
                    }
                }
            }
        }
    }
}
#endif