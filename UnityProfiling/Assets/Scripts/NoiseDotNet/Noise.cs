// MIT License
// 
// Copyright (c) 2025-2026 Miles Oetzel
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

// GradientNoise2D() and GradientNoise3D() can either use Quadratic Noise or Perlin Noise as their underlying algorithm.
// Quadratic noise is better quality, but Perlin Noise is around 20% faster. Quadratic noise is recommended.
// If you would like to switch to Perlin noise, remove the #define QUADRATIC statement.
#define QUADRATIC

#if UNITY_2017_1_OR_NEWER
#define UNITY
#else
#define CORECLR
#endif

// This library is written to be compatible with both Unity and CoreCLR.
// In CoreCLR, vectorization is achieved using the System.Numerics.Vector<T> API.
// In Unity, vectorization is achieved using Burst auto-vectorization.
// So in CoreCLR, Int and Float represent Vector<int> and Vector<float>,
// while in Unity Int and Float simply represent int and float, since Burst will automatically preform vectorization.
// The benefit of this approach is it involves very little platform-specific vectorized code,
// so there is no need for multiple versions based on Fma/Avx2 support or ARM.

#if CORECLR
using System.Numerics;
using System.Runtime.Intrinsics;
using Int = System.Numerics.Vector<int>;
using Float = System.Numerics.Vector<float>;
using Util = System.Numerics.Vector;
#else
using Int = System.Int32;
using Float = System.Single;
using Util = NoiseDotNet.ScalarUtil;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
#endif

using System.Runtime.CompilerServices;
using System;

namespace NoiseDotNet
{
    public interface INoiseFunction 
    {
        public int Dimensions { get; }
        public int Outputs { get; }
        public void Evaluate(Float x, Float y, Float z, Int seed, out Float o1, out Float o2);
    }

    /// <summary>
    /// SIMD-accelerated implementations of coherent noise functions.
    /// </summary>
    public static partial class Noise
    {
        // In CoreCLR, we can simply call directly into the vectorized code,
        // However in Unity, we need to run the vectorized code in a Burst compiled job.
        // For this reason, there are two versions of each noise function in Unity:
        // A version that takes in pointers  which is called by the Burst job (Spans have limited support in Burst),
        // and a version that takes in Spans, which creates and runs the Burst job.

#if CORECLR
        /// <summary>
        /// <para> Vectorized 2D gradient noise function. Underlying algorithm is Quadratic noise, a modified version of Perlin noise. </para>
        /// <para> Output range is approximately -1.0 to 1.0, but in rare cases may exceed this range. </para>
        /// </summary>
        /// <param name="xCoords">The x-coordinates of the sample points.</param>
        /// <param name="yCoords">The y-coordinates of the sample points.</param>
        /// <param name="output">The output buffer evaluations are written into.</param>
        /// <param name="settings">The settings for the noise function.</param>
        public static void GradientNoise2D(Span<float> xCoords, Span<float> yCoords, Span<float> output, in NoiseSettings settings)
        {
            EvaluateNoiseFunction<GradientNoise2DFunction>(xCoords, yCoords, xCoords, output, output, settings);
        }
#else

        /// <summary>
        /// <para> Vectorized 2D gradient noise function. Underlying algorithm is Quadratic noise, a modified version of Perlin noise. </para>
        /// <para> Output range is approximately -1.0 to 1.0, but in rare cases may exceed this range. </para>
        /// <para> Runs a synchronous Burst job to accelerate evaluation. If calling from Burst compiled code, use Noise.GradientNoise2DBurst() instead.</para>
        /// </summary>
        /// <param name="xCoords">The x-coordinates of the sample points.</param>
        /// <param name="yCoords">The y-coordinates of the sample points.</param>
        /// <param name="output">The output buffer evaluations are written into.</param>
        /// <param name="settings">The settings for the noise function.</param>
        public static void GradientNoise2D(ReadOnlySpan<float> xCoords, ReadOnlySpan<float> yCoords, Span<float> output, NoiseSettings settings)
        {
            // RunJob handles input validation
            BurstNoiseJob.RunGradientNoise2DJob(xCoords, yCoords, output, settings);
        }

        /// <summary>
        /// <para> Vectorized 2D gradient noise function. Underlying algorithm is Quadratic noise, a modified version of Perlin noise. </para>
        /// <para> Output range is approximately -1.0 to 1.0, but in rare cases may exceed this range. </para>
        /// <para> This function is intended to be called in Burst compiled code. If not using Burst, use Noise.GradientNoise2D() instead. That function uses Burst internally to improve evaluation speed.</para>
        /// </summary>
        /// <param name="xCoords">The x-coordinates of the sample points.</param>
        /// <param name="yCoords">The y-coordinates of the sample points.</param>
        /// <param name="output">The output buffer evaluations are written into.</param>
        /// <param name="settings">The settings for the noise function.</param>
        public static unsafe void GradientNoise2DBurst([NoAlias] float* xCoords, [NoAlias] float* yCoords, [NoAlias] float* output, int length, in NoiseSettings settings)
        {
            // this will be auto-vectorized by Burst.
            for (int i = 0; i < length; ++i)
            {
                float value = GradientNoise2DVector(xCoords[i] * settings.XFrequency, yCoords[i] * settings.YFrequency, settings.Seed) * settings.Amplitude;
                if (settings.Accumulate)
                    output[i] += value;
                else
                    output[i] = value;
            }
        }
#endif

#if CORECLR
        /// <summary>
        /// <para> Vectorized 3D gradient noise function. Underlying algorithm is Quadratic noise, a modified version of Perlin noise. </para>
        /// <para> Output range is approximately -1.0 to 1.0, but in rare cases may exceed this range. </para>
        /// </summary>
        /// <param name="xCoords">The x-coordinates of the sample points.</param>
        /// <param name="yCoords">The y-coordinates of the sample points.</param>
        /// <param name="zCoords">The z-coordinates of the sample points.</param>
        /// <param name="output">The output buffer evaluations are written into.</param>
        /// <param name="settings">The settings for the noise function.</param>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void GradientNoise3D(Span<float> xCoords, Span<float> yCoords, Span<float> zCoords, Span<float> output, in NoiseSettings settings)
        {
            EvaluateNoiseFunction<GradientNoise3DFunction>(xCoords, yCoords, zCoords, output, output, settings);
        }
#else
        /// <summary>
        /// <para> Vectorized 3D gradient noise function. Underlying algorithm is Quadratic noise, a modified version of Perlin noise. </para>
        /// <para> Output range is approximately -1.0 to 1.0, but in rare cases may exceed this range. </para>
        /// <para> Runs a synchronous Burst job to accelerate evaluation. If calling from Burst compiled code, use Noise.GradientNoise3DBurst() instead.</para>
        /// </summary>
        /// <param name="xCoords">The x-coordinates of the sample points.</param>
        /// <param name="yCoords">The y-coordinates of the sample points.</param>
        /// <param name="zCoords">The z-coordinates of the sample points.</param>
        /// <param name="output">The output buffer evaluations are written into.</param>
        /// <param name="settings">The settings for the noise function.</param>
        public static void GradientNoise3D(ReadOnlySpan<float> xCoords, ReadOnlySpan<float> yCoords, ReadOnlySpan<float> zCoords, Span<float> output, in NoiseSettings settings)
        {
            // RunJob handles input validation
            BurstNoiseJob.RunGradientNoise3DJob(xCoords, yCoords, zCoords, output, settings);
        }

        /// <summary>
        /// <para> Vectorized 3D gradient noise function. Underlying algorithm is Quadratic noise, a modified version of Perlin noise. </para>
        /// <para> Output range is approximately -1.0 to 1.0, but in rare cases may exceed this range. </para>
        /// <para> This function is intended to be called in Burst compiled code. If not using Burst, use Noise.GradientNoise3D() instead. That function uses Burst internally to improve evaluation speed.</para>
        /// </summary>
        /// <param name="xCoords">The x-coordinates of the sample points.</param>
        /// <param name="yCoords">The y-coordinates of the sample points.</param>
        /// <param name="zCoords">The z-coordinates of the sample points.</param>
        /// <param name="output">The output buffer evaluations are written into.</param>
        /// <param name="settings">The settings for the noise function.</param>
        public static unsafe void GradientNoise3DBurst([NoAlias] float* xCoords, [NoAlias] float* yCoords, [NoAlias] float* zCoords, [NoAlias] float* output, int length, in NoiseSettings settings)
        {
            // this will be auto-vectorized by Burst.
            for (int i = 0; i < length; ++i)
            {
                float value = GradientNoise3DVector(xCoords[i] * settings.XFrequency, yCoords[i] * settings.YFrequency, zCoords[i] * settings.ZFrequency, settings.Seed) * settings.Amplitude;
                if (settings.Accumulate)
                    output[i] += value;
                else
                    output[i] = value;
            }
        }
#endif

#if CORECLR
        /// <summary>
        /// <para> Vectorized 2D cellular noise function.</para>
        /// <para> Outputs the distance to the center of the Voronoi cell and the distance to the edge of the Voronoi cell in two separate buffers. </para>
        /// </summary>
        /// <param name="xCoords">The x-coordinates of the sample points.</param>
        /// <param name="yCoords">The y-coordinates of the sample points.</param>
        /// <param name="centerDistOutput">The output buffer cell center distances are written into.</param>
        /// <param name="edgeDistOutput">The output buffer cell edge distances are written into.</param>
        /// <param name="settings">The settings for the noise function.</param>
        public static void CellularNoise2D(Span<float> xCoords, Span<float> yCoords, Span<float> centerDistOutput, Span<float> edgeDistOutput, in NoiseSettings settings)
        {
            EvaluateNoiseFunction<CellularNoise2DFunction>(xCoords, yCoords, xCoords, centerDistOutput, edgeDistOutput, settings);
        }
#else
        /// <summary>
        /// <para> Vectorized 2D cellular noise function.</para>
        /// <para> Outputs the distance to the center of the Voronoi cell and the distance to the edge of the Voronoi cell in two separate buffers. </para>
        /// <para> Runs a synchronous Burst job to accelerate evaluation. If calling from Burst compiled code, use Noise.CellularNoise2DBurst() instead.</para>
        /// </summary>
        /// <param name="xCoords">The x-coordinates of the sample points.</param>
        /// <param name="yCoords">The y-coordinates of the sample points.</param>
        /// <param name="centerDistOutput">The output buffer cell center distances are written into.</param>
        /// <param name="edgeDistOutput">The output buffer cell edge distances are written into.</param>
        /// <param name="settings">The settings for the noise function.</param>
        public static void CellularNoise2D(ReadOnlySpan<float> xCoords, ReadOnlySpan<float> yCoords, Span<float> centerDistOutput, Span<float> edgeDistOutput, in NoiseSettings settings)
        {
            BurstNoiseJob.RunCellularNoise2DJob(xCoords, yCoords, centerDistOutput, edgeDistOutput, settings);
        }

        /// <summary>
        /// <para> Vectorized 2D cellular noise function.</para>
        /// <para> Outputs the distance to the center of the Voronoi cell and the distance to the edge of the Voronoi cell in two separate buffers. </para>
        /// <para> This function is intended to be called in Burst compiled code. If not using Burst, use Noise.CellularNoise2D() instead. That function uses Burst internally to improve evaluation speed.</para>
        /// </summary>
        /// <param name="xCoords">The x-coordinates of the sample points.</param>
        /// <param name="yCoords">The y-coordinates of the sample points.</param>
        /// <param name="zCoords">The z-coordinates of the sample points.</param>
        /// <param name="centerDistOutput">The output buffer cell center distances are written into.</param>
        /// <param name="edgeDistOutput">The output buffer cell edge distances are written into.</param>
        /// <param name="settings">The settings for the noise function.</param>
        public static unsafe void CellularNoise2DBurst([NoAlias] float* xCoords, [NoAlias] float* yCoords, [NoAlias] float* centerDistOutput, [NoAlias] float* edgeDistOutput, int length, in NoiseSettings settings)
        {
            // this will be auto-vectorized by Burst.
            for (int i = 0; i < length; ++i)
            {
                (float centerDist, float edgeDist) = CellularNoise2DVector(xCoords[i] * settings.XFrequency, yCoords[i] * settings.YFrequency, settings.Seed);
                float centerValue = centerDist * settings.Amplitude;
                float edgeValue = edgeDist * settings.Amplitude2;
                if (settings.Accumulate)
                {
                    centerDistOutput[i] += centerValue;
                    edgeDistOutput[i] += edgeValue;
                }
                else
                {
                    centerDistOutput[i] = centerValue;
                    edgeDistOutput[i] = edgeValue;
                }
            }
        }
#endif

#if CORECLR
        /// <summary>
        /// <para> Vectorized 3D cellular noise function.</para>
        /// <para> Outputs the distance to the center of the Voronoi cell and the distance to the edge of the Voronoi cell in two separate buffers. </para>
        /// </summary>
        /// <param name="xCoords">The x-coordinates of the sample points.</param>
        /// <param name="yCoords">The y-coordinates of the sample points.</param>
        /// <param name="zCoords">The z-coordinates of the sample points.</param>
        /// <param name="centerDistOutput">The output buffer cell center distances are written into.</param>
        /// <param name="edgeDistOutput">The output buffer cell edge distances are written into.</param>
        /// <param name="settings">The settings for the noise function.</param>
        public static void CellularNoise3D(ReadOnlySpan<float> xCoords, ReadOnlySpan<float> yCoords, ReadOnlySpan<float> zCoords, Span<float> centerDistOutput, Span<float> edgeDistOutput, in NoiseSettings settings)
        {
            EvaluateNoiseFunction<CellularNoise3DFunction>(xCoords, yCoords, zCoords, centerDistOutput, edgeDistOutput, settings);
        }
#else
        /// <summary>
        /// <para> Vectorized 3D cellular noise function.</para>
        /// <para> Outputs the distance to the center of the Voronoi cell and the distance to the edge of the Voronoi cell in two separate buffers. </para>
        /// <para> Runs a synchronous Burst job to accelerate evaluation. If calling from Burst compiled code, use Noise.CellularNoise3DBurst() instead.</para>
        /// </summary>
        /// <param name="xCoords">The x-coordinates of the sample points.</param>
        /// <param name="yCoords">The y-coordinates of the sample points.</param>
        /// <param name="zCoords">The z-coordinates of the sample points.</param>
        /// <param name="centerDistOutput">The output buffer cell center distances are written into.</param>
        /// <param name="edgeDistOutput">The output buffer cell edge distances are written into.</param>
        /// <param name="settings">The settings for the noise function.</param>
        public static void CellularNoise3D(ReadOnlySpan<float> xCoords, ReadOnlySpan<float> yCoords, ReadOnlySpan<float> zCoords, Span<float> centerDistOutput, Span<float> edgeDistOutput, in NoiseSettings settings)
        {
            // RunJob handles input validation
            BurstNoiseJob.RunCellularNoise3DJob(xCoords, yCoords, zCoords, centerDistOutput, edgeDistOutput, settings);
        }

        /// <summary>
        /// <para> Vectorized 3D cellular noise function.</para>
        /// <para> Outputs the distance to the center of the Voronoi cell and the distance to the edge of the Voronoi cell in two separate buffers. </para>
        /// <para> This function is intended to be called in Burst compiled code. If not using Burst, use Noise.CellularNoise3D() instead. That function uses Burst internally to improve evaluation speed.</para>
        /// </summary>
        /// <param name="xCoords">The x-coordinates of the sample points.</param>
        /// <param name="yCoords">The y-coordinates of the sample points.</param>
        /// <param name="zCoords">The z-coordinates of the sample points.</param>
        /// <param name="centerDistOutput">The output buffer cell center distances are written into.</param>
        /// <param name="edgeDistOutput">The output buffer cell edge distances are written into.</param>
        /// <param name="settings">The settings for the noise function.</param>
        public static unsafe void CellularNoise3DBurst([NoAlias] float* xCoords, [NoAlias] float* yCoords, [NoAlias] float* zCoords, [NoAlias] float* centerDistOutput, [NoAlias] float* edgeDistOutput, int length, in NoiseSettings settings)
        {
            // this will be auto-vectorized by Burst.
            for (int i = 0; i < length; ++i)
            {
                (float centerDist, float edgeDist) = CellularNoise3DVector(xCoords[i] * settings.XFrequency, yCoords[i] * settings.YFrequency, zCoords[i] * settings.ZFrequency, settings.Seed);
                float centerValue = centerDist * settings.Amplitude;
                float edgeValue = edgeDist * settings.Amplitude2;
                if (settings.Accumulate)
                {
                    centerDistOutput[i] += centerValue;
                    edgeDistOutput[i] += edgeValue;
                }
                else
                {
                    centerDistOutput[i] = centerValue;
                    edgeDistOutput[i] = edgeValue;
                }
            }
        }
#endif

#if CORECLR
        public static void EvaluateNoiseFunction<TNoise>(
            ReadOnlySpan<float> xCoords, 
            ReadOnlySpan<float> yCoords, 
            ReadOnlySpan<float> zCoords, 
            Span<float> output1, 
            Span<float> output2, 
            in NoiseSettings settings)
            where TNoise : struct, INoiseFunction
        {
            TNoise noiseFunction = default;

            int length = output1.Length;
            if (length == 0)
                return;

#if DEBUG
            if (xCoords.Length != length)
                throw new ArgumentException($"Expected x buffer length {xCoords.Length} to equal output buffer length {length}");

            if (noiseFunction.Dimensions >= 2 && yCoords.Length != length)
                throw new ArgumentException($"Expected y buffer length {yCoords.Length} to equal output buffer length {length}");

            if (noiseFunction.Dimensions >= 3 && zCoords.Length != length)
                throw new ArgumentException($"Expected z buffer length {zCoords.Length} to equal output buffer length {length}");

            if (noiseFunction.Outputs >= 2 && output2.Length != length)
                throw new ArgumentException($"Expected secondary output buffer length {output2.Length} to equal output buffer length {length}");
#endif

            (float xFreq, float yFreq, float zFreq, float amp1, float amp2, int seed, bool accumulate) = settings;

            Float xfVec = Util.Create(xFreq);
            Float yfVec = Util.Create(yFreq);
            Float zfVec = Util.Create(zFreq);
            Float amp1Vec = Util.Create(amp1);
            Float amp2Vec = Util.Create(amp2);
            Int seedVec = Util.Create(seed);           

            int fullVectorLength = length - length % Float.Count;
            for (int i = 0; i < fullVectorLength; i += Float.Count)
            {
                Float xVec = Util.LoadUnsafe(in xCoords[i]) * xfVec;
                Float yVec = noiseFunction.Dimensions >= 2 ? Util.LoadUnsafe(in yCoords[i]) * yfVec : default;
                Float zVec = noiseFunction.Dimensions >= 3 ? Util.LoadUnsafe(in zCoords[i]) * zfVec : default;

                noiseFunction.Evaluate(xVec, yVec, zVec, seedVec, out Float out1Vec, out Float out2Vec);
                out1Vec *= amp1Vec;
                if (noiseFunction.Outputs >= 2)
                    out2Vec *= amp2Vec;
                if (accumulate)
                {
                    Float out1Current = Util.LoadUnsafe(ref output1[i]);
                    out1Vec += out1Current;
                    if (noiseFunction.Outputs >= 2)
                    {
                        Float out2Current = Util.LoadUnsafe(ref output2[i]);
                        out2Vec += out2Current;
                    }
                }
                out1Vec.StoreUnsafe(ref output1[i]);
                if (noiseFunction.Outputs >= 2)
                    out2Vec.StoreUnsafe(ref output2[i]);
            }

            int remainder = length - fullVectorLength;
            if (remainder > 0)
            {
                Float xVec = default, yVec = default, zVec = default;
                for (int i = 0; i < remainder; ++i)
                {
                    int sourceIndex = fullVectorLength + i;
                    xVec = xVec.WithElement(i, xCoords[sourceIndex]);
                    if (noiseFunction.Dimensions >= 2)
                        yVec = yVec.WithElement(i, yCoords[sourceIndex]);
                    if (noiseFunction.Dimensions >= 3)
                        zVec = zVec.WithElement(i, zCoords[sourceIndex]);
                }

                noiseFunction.Evaluate(xVec * xfVec, yVec * yfVec, zVec * zfVec, seedVec, out Float out1Vec, out Float out2Vec);

                out1Vec *= amp1Vec;
                if (noiseFunction.Outputs >= 2)
                    out2Vec *= amp2Vec;

                if (accumulate)
                {
                    for (int i = 0; i < remainder; ++i)
                    {
                        int targetIndex = fullVectorLength + i;
                        output1[targetIndex] += out1Vec.GetElement(i);
                        if (noiseFunction.Outputs >= 2)
                            output2[targetIndex] += out2Vec.GetElement(i);
                    }
                }
                else 
                {
                    for (int i = 0; i < remainder; ++i)
                    {
                        int targetIndex = fullVectorLength + i;
                        output1[targetIndex] = out1Vec.GetElement(i);
                        if (noiseFunction.Outputs >= 2)
                            output2[targetIndex] = out2Vec.GetElement(i);
                    }
                }
            }
        }
#endif
    }
}