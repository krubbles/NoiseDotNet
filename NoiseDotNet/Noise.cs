// MIT License
// 
// Copyright (c) 2025 Miles Oetzel
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
using System.Runtime.Intrinsics.X86;
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
    /// <summary>
    /// SIMD-accelerated implementations of coherent noise functions.
    /// </summary>
    public static class Noise
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
        /// <param name="xFreq">x-coordinates are multiplied by this number before being used.</param>
        /// <param name="yFreq">y-coordinates are multiplied by this number before being used.</param>
        /// <param name="amplitude">The output of the noise function is multiplied by this number before being written into the output buffer.</param>
        /// <param name="seed">The seed for the noise function.</param>
        public static unsafe void GradientNoise2D(Span<float> xCoords, Span<float> yCoords, Span<float> output, float xFreq, float yFreq, float amplitude, int seed)
        {
            Int seedVec = Util.Create(seed);
            Float xfVec = Util.Create(xFreq), yfVec = Util.Create(yFreq), ampVec = Util.Create(amplitude);
            int length = output.Length;
            if (length < Float.Count)
            {
                // if the buffer doesn't have enough elements to fit into a vector,
                // we can't use the load and store instructions, so we have to build the vector element by element instead.
                Float xVec = default, yVec = default;
                for (int i = 0; i < length; ++i)
                {
                    xVec = xVec.WithElement(i, xCoords[i]);
                    yVec = yVec.WithElement(i, yCoords[i]);
                }
                Float result = GradientNoise2DVector(xVec * xfVec, yVec * yfVec, seedVec) * ampVec;
                for (int i = 0; i < length; ++i)
                {
                    output[i] = result.GetElement(i);
                }
            }
            else
            {
                for (int i = 0; i < length - Float.Count; i += Float.Count)
                {
                    Float xVec = Util.LoadUnsafe(ref xCoords[i]);
                    Float yVec = Util.LoadUnsafe(ref yCoords[i]);
                    Float result = GradientNoise2DVector(xVec * xfVec, yVec * yfVec, seedVec) * ampVec;
                    result.StoreUnsafe(ref output[i]);
                }
                {
                    int i = length - Float.Count;
                    Float xVec = Util.LoadUnsafe(ref xCoords[i]);
                    Float yVec = Util.LoadUnsafe(ref yCoords[i]);
                    Float result = GradientNoise2DVector(xVec * xfVec, yVec * yfVec, seedVec) * ampVec;
                    result.StoreUnsafe(ref output[i]);
                }
            }
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
        /// <param name="xFreq">x-coordinates are multiplied by this number before being used.</param>
        /// <param name="yFreq">y-coordinates are multiplied by this number before being used.</param>
        /// <param name="amplitude">The output of the noise function is multiplied by this number before being written into the output buffer.</param>
        /// <param name="seed">The seed for the noise function.</param>
        public static unsafe void GradientNoise2D(ReadOnlySpan<float> xCoords, ReadOnlySpan<float> yCoords, Span<float> output, float xFreq, float yFreq, float amplitude, int seed)
        {
            // RunJob handles input validation
            BurstNoiseJob.RunGradientNoise2DJob(xCoords, yCoords, output, xFreq, yFreq, amplitude, seed);
        }

        /// <summary>
        /// <para> Vectorized 2D gradient noise function. Underlying algorithm is Quadratic noise, a modified version of Perlin noise. </para>
        /// <para> Output range is approximately -1.0 to 1.0, but in rare cases may exceed this range. </para>
        /// <para> This function is intended to be called in Burst compiled code. If not using Burst, use Noise.GradientNoise2D() instead. That function uses Burst internally to improve evaluation speed.</para>
        /// </summary>
        /// <param name="xCoords">The x-coordinates of the sample points.</param>
        /// <param name="yCoords">The y-coordinates of the sample points.</param>
        /// <param name="output">The output buffer evaluations are written into.</param>
        /// <param name="xFreq">x-coordinates are multiplied by this number before being used.</param>
        /// <param name="yFreq">y-coordinates are multiplied by this number before being used.</param>
        /// <param name="amplitude">The output of the noise function is multiplied by this number before being written into the output buffer.</param>
        /// <param name="seed">The seed for the noise function.</param>
        public static unsafe void GradientNoise2DBurst([NoAlias] float* xCoords, [NoAlias] float* yCoords, [NoAlias] float* output, int length, float xFreq, float yFreq, float amplitude, int seed)
        {
            // this will be auto-vectorized by Burst.
            for (int i = 0; i < length; ++i)
            {
                output[i] = GradientNoise2DVector(xCoords[i] * xFreq, yCoords[i] * yFreq, seed) * amplitude;
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
        /// <param name="xFreq">x-coordinates are multiplied by this number before being used.</param>
        /// <param name="yFreq">y-coordinates are multiplied by this number before being used.</param>
        /// <param name="zFreq">z-coordinates are multiplied by this number before being used.</param>
        /// <param name="amplitude">The output of the noise function is multiplied by this number before being written into the output buffer.</param>
        /// <param name="seed">The seed for the noise function.</param>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static unsafe void GradientNoise3D(Span<float> xCoords, Span<float> yCoords, Span<float> zCoords, Span<float> output, float xFreq, float yFreq, float zFreq, float amplitude, int seed)
        {
            Int seedVec = Util.Create(seed);
            Float xfVec = Util.Create(xFreq), yfVec = Util.Create(yFreq), zfVec = Util.Create(zFreq), ampVec = Util.Create(amplitude);
            int length = output.Length;
            if (length < Float.Count)
            {
                // if the buffer doesn't have enough elements to fit into a vector,
                // we can't use the load and store instructions, so we have to build the vector element by element instead.
                Float xVec = default, yVec = default, zVec = default;
                for (int i = 0; i < length; ++i)
                {
                    xVec = xVec.WithElement(i, xCoords[i]);
                    yVec = yVec.WithElement(i, yCoords[i]);
                    zVec = zVec.WithElement(i, zCoords[i]);
                }
                Float result = GradientNoise3DVector(xVec * xfVec, yVec * yfVec, zVec * zfVec, seedVec) * ampVec;
                for (int i = 0; i < length; ++i)
                {
                    output[i] = result.GetElement(i);
                }
            }
            else
            {
                for (int i = 0; i < length - Float.Count; i += Float.Count)
                {
                    Float xVec = Util.LoadUnsafe(ref xCoords[i]) * xfVec;
                    Float yVec = Util.LoadUnsafe(ref yCoords[i]) * yfVec;
                    Float zVec = Util.LoadUnsafe(ref zCoords[i]) * zfVec;
                    Float result = GradientNoise3DVector(xVec, yVec, zVec, seedVec) * ampVec;
                    result.StoreUnsafe(ref output[i]);
                }
                {
                    int i = length - Float.Count;
                    Float xVec = Util.LoadUnsafe(ref xCoords[i]) * xfVec;
                    Float yVec = Util.LoadUnsafe(ref yCoords[i]) * yfVec;
                    Float zVec = Util.LoadUnsafe(ref zCoords[i]) * zfVec;
                    Float result = GradientNoise3DVector(xVec, yVec, zVec, seedVec) * ampVec;
                    result.StoreUnsafe(ref output[i]);
                }
            }
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
        /// <param name="xFreq">x-coordinates are multiplied by this number before being used.</param>
        /// <param name="yFreq">y-coordinates are multiplied by this number before being used.</param>
        /// <param name="zFreq">z-coordinates are multiplied by this number before being used.</param>
        /// <param name="amplitude">The output of the noise function is multiplied by this number before being written into the output buffer.</param>
        /// <param name="seed">The seed for the noise function.</param>
        public static unsafe void GradientNoise3D(ReadOnlySpan<float> xCoords, ReadOnlySpan<float> yCoords, ReadOnlySpan<float> zCoords, Span<float> output, float xFreq, float yFreq, float zFreq, float amplitude, int seed)
        {
            // RunJob handles input validation
            BurstNoiseJob.RunGradientNoise3DJob(xCoords, yCoords, zCoords, output, xFreq, yFreq, zFreq, amplitude, seed);
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
        /// <param name="xFreq">x-coordinates are multiplied by this number before being used.</param>
        /// <param name="yFreq">y-coordinates are multiplied by this number before being used.</param>
        /// <param name="zFreq">z-coordinates are multiplied by this number before being used.</param>
        /// <param name="amplitude">The output of the noise function is multiplied by this number before being written into the output buffer.</param>
        /// <param name="seed">The seed for the noise function.</param>
        public static unsafe void GradientNoise3DBurst([NoAlias] float* xCoords, [NoAlias] float* yCoords, [NoAlias] float* zCoords, [NoAlias] float* output, int length, float xFreq, float yFreq, float zFreq, float amplitude, int seed)
        {
            // this will be auto-vectorized by Burst.
            for (int i = 0; i < length; ++i)
            {
                output[i] = GradientNoise3DVector(xCoords[i] * xFreq, yCoords[i] * yFreq, zCoords[i] * zFreq, seed) * amplitude;
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
        /// <param name="xFreq">x-coordinates are multiplied by this number before being used.</param>
        /// <param name="yFreq">y-coordinates are multiplied by this number before being used.</param>
        /// <param name="centerDistAmplitude">Center distance outputs are multiplied by this number before being written into the output buffer.</param>
        /// <param name="edgeDistAmplitude">Edge distance outputs are multiplied by this number before being written into the output buffer.</param>
        /// <param name="seed">The seed for the noise function.</param>
        public static unsafe void CellularNoise2D(Span<float> xCoords, Span<float> yCoords, Span<float> centerDistOutput, Span<float> edgeDistOutput, float xFreq, float yFreq, float centerDistAmplitude, float edgeDistAmplitude, int seed)
        {
            Int seedVec = Util.Create(seed);
            Float xfVec = Util.Create(xFreq), yfVec = Util.Create(yFreq);
            Float centerAmpVec = Util.Create(centerDistAmplitude), edgeAmpVec = Util.Create(edgeDistAmplitude);
            int length = centerDistOutput.Length;
            if (length < Float.Count)
            {
                // if the buffer doesn't have enough elements to fit into a vector,
                // we can't use the load and store instructions, so we have to build the vector element by element instead.
                Float xVec = default, yVec = default;
                for (int i = 0; i < length; ++i)
                {
                    xVec = xVec.WithElement(i, xCoords[i]);
                    yVec = yVec.WithElement(i, yCoords[i]);
                }
                (Float centerDist, Float edgeDist) = CellularNoise2DVector(xVec * xfVec, yVec * yfVec, seedVec);
                centerDist *= centerAmpVec;
                edgeDist *= edgeAmpVec;
                for (int i = 0; i < length; ++i)
                {
                    centerDistOutput[i] = centerDist.GetElement(i);
                    edgeDistOutput[i] = edgeDist.GetElement(i);
                }
            }
            else
            {
                for (int i = 0; i < length - Float.Count; i += Float.Count)
                {
                    Float xVec = Util.LoadUnsafe(ref xCoords[i]);
                    Float yVec = Util.LoadUnsafe(ref yCoords[i]);
                    (Float centerDist, Float edgeDist) = CellularNoise2DVector(xVec * xfVec, yVec * yfVec, seedVec);
                    centerDist *= centerAmpVec;
                    edgeDist *= edgeAmpVec;
                    centerDist.StoreUnsafe(ref centerDistOutput[i]);
                    edgeDist.StoreUnsafe(ref edgeDistOutput[i]);
                }
                {
                    int i = length - Float.Count;
                    Float xVec = Util.LoadUnsafe(ref xCoords[i]);
                    Float yVec = Util.LoadUnsafe(ref yCoords[i]);
                    (Float centerDist, Float edgeDist) = CellularNoise2DVector(xVec * xfVec, yVec * yfVec, seedVec);
                    centerDist *= centerAmpVec;
                    edgeDist *= edgeAmpVec;
                    centerDist.StoreUnsafe(ref centerDistOutput[i]);
                    edgeDist.StoreUnsafe(ref edgeDistOutput[i]);
                }
            }
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
        /// <param name="xFreq">x-coordinates are multiplied by this number before being used.</param>
        /// <param name="yFreq">y-coordinates are multiplied by this number before being used.</param>
        /// <param name="centerDistAmplitude">Center distance outputs are multiplied by this number before being written into the output buffer.</param>
        /// <param name="edgeDistAmplitude">Edge distance outputs are multiplied by this number before being written into the output buffer.</param>
        /// <param name="seed">The seed for the noise function.</param>
        public static unsafe void CellularNoise2D(ReadOnlySpan<float> xCoords, ReadOnlySpan<float> yCoords, Span<float> centerDistOutput, Span<float> edgeDistOutput, float xFreq, float yFreq, float centerDistAmplitude, float edgeDistAmplitude, int seed)
        {
            BurstNoiseJob.RunCellularNoise2DJob(xCoords, yCoords, centerDistOutput, edgeDistOutput, xFreq, yFreq, centerDistAmplitude, edgeDistAmplitude, seed);
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
        /// <param name="xFreq">x-coordinates are multiplied by this number before being used.</param>
        /// <param name="yFreq">y-coordinates are multiplied by this number before being used.</param>
        /// <param name="zFreq">z-coordinates are multiplied by this number before being used.</param>
        /// <param name="centerDistAmplitude">Center distance outputs are multiplied by this number before being written into the output buffer.</param>
        /// <param name="edgeDistAmplitude">Edge distance outputs are multiplied by this number before being written into the output buffer.</param>
        /// <param name="seed">The seed for the noise function.</param>
        public static unsafe void CellularNoise2DBurst([NoAlias] float* xCoords, [NoAlias] float* yCoords, [NoAlias] float* centerDistOutput, [NoAlias] float* edgeDistOutput, int length, float xFreq, float yFreq, float centerDistAmplitude, float edgeDistAmplitude, int seed)
        {
            // this will be auto-vectorized by Burst.
            for (int i = 0; i < length; ++i)
            {
                (float centerDist, float edgeDist) = CellularNoise2DVector(xCoords[i] * xFreq, yCoords[i] * yFreq, seed);
                centerDistOutput[i] = centerDist * centerDistAmplitude;
                edgeDistOutput[i] = edgeDist * edgeDistAmplitude;
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
        /// <param name="xFreq">x-coordinates are multiplied by this number before being used.</param>
        /// <param name="yFreq">y-coordinates are multiplied by this number before being used.</param>
        /// <param name="zFreq">z-coordinates are multiplied by this number before being used.</param>
        /// <param name="centerDistAmplitude">Center distance outputs are multiplied by this number before being written into the output buffer.</param>
        /// <param name="edgeDistAmplitude">Edge distance outputs are multiplied by this number before being written into the output buffer.</param>
        /// <param name="seed">The seed for the noise function.</param>
        public static unsafe void CellularNoise3D(ReadOnlySpan<float> xCoords, ReadOnlySpan<float> yCoords, ReadOnlySpan<float> zCoords, Span<float> centerDistOutput, Span<float> edgeDistOutput, float xFreq, float yFreq, float zFreq, float centerDistAmplitude, float edgeDistAmplitude, int seed)
        {
            Int seedVec = Util.Create(seed);
            Float xfVec = Util.Create(xFreq), yfVec = Util.Create(yFreq), zfVec = Util.Create(zFreq);
            Float centerAmpVec = Util.Create(centerDistAmplitude), edgeAmpVec = Util.Create(edgeDistAmplitude);
            int length = centerDistOutput.Length;
            if (length < Float.Count)
            {
                // if the buffer doesn't have enough elements to fit into a vector,
                // we can't use the load and store instructions, so we have to build the vector element by element instead.
                Float xVec = default, yVec = default, zVec = default;
                for (int i = 0; i < length; ++i)
                {
                    xVec = xVec.WithElement(i, xCoords[i]);
                    yVec = yVec.WithElement(i, yCoords[i]);
                    zVec = zVec.WithElement(i, zCoords[i]);
                }
                (Float centerDist, Float edgeDist) = CellularNoise3DVector(xVec * xfVec, yVec * yfVec, zVec * zfVec, seedVec);
                centerDist *= centerAmpVec;
                edgeDist *= edgeAmpVec;
                for (int i = 0; i < length; ++i)
                {
                    centerDistOutput[i] = centerDist.GetElement(i);
                    edgeDistOutput[i] = edgeDist.GetElement(i);
                }
            }
            else
            {
                for (int i = 0; i < length - Float.Count; i += Float.Count)
                {
                    Float xVec = Util.LoadUnsafe(in xCoords[i]);
                    Float yVec = Util.LoadUnsafe(in yCoords[i]);
                    Float zVec = Util.LoadUnsafe(in zCoords[i]);

                    (Float centerDist, Float edgeDist) = CellularNoise3DVector(xVec * xfVec, yVec * yfVec, zVec * zfVec, seedVec);
                    centerDist *= centerAmpVec;
                    edgeDist *= edgeAmpVec;
                    centerDist.StoreUnsafe(ref centerDistOutput[i]);
                    edgeDist.StoreUnsafe(ref edgeDistOutput[i]);
                }
                {
                    int i = length - Float.Count;

                    Float xVec = Util.LoadUnsafe(in xCoords[i]);
                    Float yVec = Util.LoadUnsafe(in yCoords[i]);
                    Float zVec = Util.LoadUnsafe(in zCoords[i]);

                    (Float centerDist, Float edgeDist) = CellularNoise3DVector(xVec * xfVec, yVec * yfVec, zVec * zfVec, seedVec);
                    centerDist *= centerAmpVec;
                    edgeDist *= edgeAmpVec;
                    centerDist.StoreUnsafe(ref centerDistOutput[i]);
                    edgeDist.StoreUnsafe(ref edgeDistOutput[i]);
                }
            }
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
        /// <param name="xFreq">x-coordinates are multiplied by this number before being used.</param>
        /// <param name="yFreq">y-coordinates are multiplied by this number before being used.</param>
        /// <param name="zFreq">z-coordinates are multiplied by this number before being used.</param>
        /// <param name="centerDistAmplitude">Center distance outputs are multiplied by this number before being written into the output buffer.</param>
        /// <param name="edgeDistAmplitude">Edge distance outputs are multiplied by this number before being written into the output buffer.</param>
        /// <param name="seed">The seed for the noise function.</param>
        public static unsafe void CellularNoise3D(ReadOnlySpan<float> xCoords, ReadOnlySpan<float> yCoords, ReadOnlySpan<float> zCoords, Span<float> centerDistOutput, Span<float> edgeDistOutput, float xFreq, float yFreq, float zFreq, float centerDistAmplitude, float edgeDistAmplitude, int seed)
        {
            // RunJob handles input validation
            BurstNoiseJob.RunCellularNoise3DJob(xCoords, yCoords, zCoords, centerDistOutput, edgeDistOutput, xFreq, yFreq, zFreq, centerDistAmplitude, edgeDistAmplitude, seed);
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
        /// <param name="xFreq">x-coordinates are multiplied by this number before being used.</param>
        /// <param name="yFreq">y-coordinates are multiplied by this number before being used.</param>
        /// <param name="zFreq">z-coordinates are multiplied by this number before being used.</param>
        /// <param name="centerDistAmplitude">Center distance outputs are multiplied by this number before being written into the output buffer.</param>
        /// <param name="edgeDistAmplitude">Edge distance outputs are multiplied by this number before being written into the output buffer.</param>
        /// <param name="seed">The seed for the noise function.</param>
        public static unsafe void CellularNoise3DBurst([NoAlias] float* xCoords, [NoAlias] float* yCoords, [NoAlias] float* zCoords, [NoAlias] float* centerDistOutput, [NoAlias] float* edgeDistOutput, int length, float xFreq, float yFreq, float zFreq, float centerDistAmplitude, float edgeDistAmplitude, int seed)
        {
            // this will be auto-vectorized by Burst.
            for (int i = 0; i < length; ++i)
            {
                (float centerDist, float edgeDist) = CellularNoise3DVector(xCoords[i] * xFreq, yCoords[i] * yFreq, zCoords[i] * zFreq, seed);
                centerDistOutput[i] = centerDist * centerDistAmplitude;
                edgeDistOutput[i] = edgeDist * edgeDistAmplitude;
            }
        }
#endif

        // All noise function implementations must be inlined so they can be auto-vectorized by Burst. 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Float GradientNoise2DVector(Float x, Float y, Int seed)
        {
            Float xFloor = Util.Floor(x);
            Float yFloor = Util.Floor(y);
            // In CoreCLR, Vector.ConvertToInt32() adds additional instructions to make sure that 
            // that the conversion behaves is consistent across platforms when the float is outside the range of an int.
            // Since in practical use of this function it will never be out of bounds, we can use ConvertToInt32Native, which avoids this overhead.
            Int ix = Util.ConvertToInt32Native(xFloor);
            Int iy = Util.ConvertToInt32Native(yFloor);
            Float fx = x - xFloor;
            Float fy = y - yFloor;

            // These constants were chosen using an optimizer to avoid visually obvious non-random effects in the hash result.
            Int ConstX = Util.Create(180601904), ConstY = Util.Create(174181987), ConstXOR = Util.Create(203663684);

            Int llHash = ix * ConstX + iy * ConstY + seed;
            Int lrHash = llHash + ConstX;
            Int ulHash = llHash + ConstY;
            Int urHash = llHash + ConstX + ConstY;

            llHash *= llHash ^ ConstXOR;
            lrHash *= lrHash ^ ConstXOR;
            ulHash *= ulHash ^ ConstXOR;
            urHash *= urHash ^ ConstXOR;

            Int GradAndMask = Util.Create(unchecked((int)0b11000000001100000000100000000111));
            Int GradOrMask = Util.Create(unchecked((int)0b00011111100001111111001111101000));

            llHash = (llHash & GradAndMask) | GradOrMask;
            lrHash = (lrHash & GradAndMask) | GradOrMask;
            ulHash = (ulHash & GradAndMask) | GradOrMask;
            urHash = (urHash & GradAndMask) | GradOrMask;

            Float fxm1 = fx - Util.Create(1f);
            Float fym1 = fy - Util.Create(1f);

            const int GradShift1 = 1, GradShift2 = 20;
#if QUADRATIC
            const int GradShift3 = 11;
#endif

            /// CoreCLR won't generate the vblendvps instruction here without explicitly calling it using the System.Runtime.Intrinsics API
            Float llGrad = Util.MultiplyAddEstimate(
                BlendVPS(llHash, fx, fy), Util.AsVectorSingle(llHash << GradShift1),
                BlendVPS(llHash, fy, fx) * Util.AsVectorSingle(llHash << GradShift2));
            Float lrGrad = Util.MultiplyAddEstimate(
                BlendVPS(lrHash, fxm1, fy), Util.AsVectorSingle(lrHash << GradShift1),
                BlendVPS(lrHash, fy, fxm1) * Util.AsVectorSingle(lrHash << GradShift2));
            Float ulGrad = Util.MultiplyAddEstimate(
                BlendVPS(ulHash, fx, fym1), Util.AsVectorSingle(ulHash << GradShift1),
                BlendVPS(ulHash, fym1, fx) * Util.AsVectorSingle(ulHash << GradShift2));
            Float urGrad = Util.MultiplyAddEstimate(
                BlendVPS(urHash, fxm1, fym1), Util.AsVectorSingle(urHash << GradShift1),
                BlendVPS(urHash, fym1, fxm1) * Util.AsVectorSingle(urHash << GradShift2));

#if QUADRATIC
            llGrad = Util.MultiplyAddEstimate(llGrad, llGrad * Util.AsVectorSingle(llHash << GradShift3), llGrad);
            lrGrad = Util.MultiplyAddEstimate(lrGrad, lrGrad * Util.AsVectorSingle(lrHash << GradShift3), lrGrad);
            ulGrad = Util.MultiplyAddEstimate(ulGrad, ulGrad * Util.AsVectorSingle(ulHash << GradShift3), ulGrad);
            urGrad = Util.MultiplyAddEstimate(urGrad, urGrad * Util.AsVectorSingle(urHash << GradShift3), urGrad);
#endif

            // smootherstep interpolation
            Float sx = fx * fx * fx * Util.MultiplyAddEstimate(Util.MultiplyAddEstimate(fx, Util.Create(6f), Util.Create(-15f)), fx, Util.Create(10f));
            Float sy = fy * fy * fy * Util.MultiplyAddEstimate(Util.MultiplyAddEstimate(fy, Util.Create(6f), Util.Create(-15f)), fy, Util.Create(10f));

            Float lLerp = Util.MultiplyAddEstimate(lrGrad - llGrad, sx, llGrad);
            Float uLerp = Util.MultiplyAddEstimate(urGrad - ulGrad, sx, ulGrad);
            Float result = Util.MultiplyAddEstimate(uLerp - lLerp, sy, lLerp);
            return result;
        }

        /// <summary>
        /// Implementation of the x86 vblendvps instruction with fallbacks for targets that don't support that instruction. 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Float BlendVPS(Int selector, Float a, Float b)
        {
#if CORECLR
            if (Avx.IsSupported)
            {
                return Avx.BlendVariable(a.AsVector256(), b.AsVector256(), selector.AsVector256().AsSingle()).AsVector();
            }
            else if (Sse41.IsSupported)
            {
                return Sse41.BlendVariable(a.AsVector128(), b.AsVector128(), selector.AsVector128().AsSingle()).AsVector();
            }
            else return Util.ConditionalSelect(Util.LessThan(selector, Util.Create(0)), a, b);
#else
            // Burst does recognize that it can use vblendbps here.
            return selector < 0 ? a : b;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Float GradientNoise3DVector(Float x, Float y, Float z, Int seed)
        {
            Float xFloor = Util.Floor(x);
            Float yFloor = Util.Floor(y);
            Float zFloor = Util.Floor(z);
            Int ix = Util.ConvertToInt32Native(xFloor);
            Int iy = Util.ConvertToInt32Native(yFloor);
            Int iz = Util.ConvertToInt32Native(zFloor);
            Float fx = x - xFloor;
            Float fy = y - yFloor;
            Float fz = z - zFloor;

            Int ConstX = Util.Create(180601904), ConstY = Util.Create(174181987), ConstZ = Util.Create(738599801), ConstXOR = Util.Create(203663684);

            // X: (lower/upper) Y: (left/right) Z: (back/front). 
            Int llbHash = ix * ConstX + iy * ConstY + iz * ConstZ + seed;
            Int lrbHash = llbHash + ConstX;
            Int ulbHash = llbHash + ConstY;
            Int urbHash = llbHash + ConstX + ConstY;
            Int llfHash = llbHash + ConstZ;
            Int lrfHash = llbHash + ConstZ + ConstX;
            Int ulfHash = llbHash + ConstZ + ConstY;
            Int urfHash = llbHash + ConstZ + ConstX + ConstY;

            // This extra bit shift compared to the 2D version is needed because without it,
            // low-significance bits don't have high quality randomness.
            // In 2D, it is possible to avoid using these bits when generating a gradient vector,
            // but in 3D, it is more difficult to achieve. 
            llbHash *= (llbHash ^ ConstXOR) >> 16;
            lrbHash *= (lrbHash ^ ConstXOR) >> 16;
            ulbHash *= (ulbHash ^ ConstXOR) >> 16;
            urbHash *= (urbHash ^ ConstXOR) >> 16;
            llfHash *= (llfHash ^ ConstXOR) >> 16;
            lrfHash *= (lrfHash ^ ConstXOR) >> 16;
            ulfHash *= (ulfHash ^ ConstXOR) >> 16;
            urfHash *= (urfHash ^ ConstXOR) >> 16;

            Int GradAndMask = Util.Create(unchecked((int)0b11000000001100000000100000000111));
            Int GradOrMask = Util.Create(unchecked((int)0b00011111100001111110001111110000));

            llbHash = (llbHash & GradAndMask) | GradOrMask;
            lrbHash = (lrbHash & GradAndMask) | GradOrMask;
            ulbHash = (ulbHash & GradAndMask) | GradOrMask;
            urbHash = (urbHash & GradAndMask) | GradOrMask;
            llfHash = (llfHash & GradAndMask) | GradOrMask;
            lrfHash = (lrfHash & GradAndMask) | GradOrMask;
            ulfHash = (ulfHash & GradAndMask) | GradOrMask;
            urfHash = (urfHash & GradAndMask) | GradOrMask;

            const int GradShift1 = 1, GradShift2 = 20, GradShift3 = 11;
            Float negOne = Util.Create(-1f);

            Float sx = fx * fx * fx * Util.MultiplyAddEstimate(Util.MultiplyAddEstimate(fx, Util.Create(6f), Util.Create(-15f)), fx, Util.Create(10f));
            Float sz = fz * fz * fz * Util.MultiplyAddEstimate(Util.MultiplyAddEstimate(fz, Util.Create(6f), Util.Create(-15f)), fz, Util.Create(10f));
            Float sy = fy * fy * fy * Util.MultiplyAddEstimate(Util.MultiplyAddEstimate(fy, Util.Create(6f), Util.Create(-15f)), fy, Util.Create(10f));

            Float llbGrad = Util.MultiplyAddEstimate(
                fx, Util.AsVectorSingle(llbHash << GradShift1), Util.MultiplyAddEstimate(
                fy, Util.AsVectorSingle(llbHash << GradShift2),
                fz * Util.AsVectorSingle(llbHash << GradShift3)));
            Float lrbGrad = Util.MultiplyAddEstimate(
                fx + negOne, Util.AsVectorSingle(lrbHash << GradShift1), Util.MultiplyAddEstimate(
                fy, Util.AsVectorSingle(lrbHash << GradShift2),
                fz * Util.AsVectorSingle(lrbHash << GradShift3)));
#if QUADRATIC
            llbGrad = Util.MultiplyAddEstimate(llbGrad, llbGrad * Util.AsVectorSingle(Util.AsVectorInt32(negOne) ^ (llbHash & Util.Create(1 << 31))), llbGrad);
            lrbGrad = Util.MultiplyAddEstimate(lrbGrad, lrbGrad * Util.AsVectorSingle(Util.AsVectorInt32(negOne) ^ (lrbHash & Util.Create(1 << 31))), lrbGrad);
#endif
            Float lbLerp = Util.MultiplyAddEstimate(lrbGrad - llbGrad, sx, llbGrad);

            Float ulbGrad = Util.MultiplyAddEstimate(
                fx, Util.AsVectorSingle(ulbHash << GradShift1), Util.MultiplyAddEstimate(
                fy + negOne, Util.AsVectorSingle(ulbHash << GradShift2),
                fz * Util.AsVectorSingle(ulbHash << GradShift3)));
            Float urbGrad = Util.MultiplyAddEstimate(
                fx + negOne, Util.AsVectorSingle(urbHash << GradShift1), Util.MultiplyAddEstimate(
                fy + negOne, Util.AsVectorSingle(urbHash << GradShift2),
                fz * Util.AsVectorSingle(urbHash << GradShift3)));
#if QUADRATIC
            ulbGrad = Util.MultiplyAddEstimate(ulbGrad, ulbGrad * Util.AsVectorSingle(Util.AsVectorInt32(negOne) ^ (ulbHash & Util.Create(1 << 31))), ulbGrad);
            urbGrad = Util.MultiplyAddEstimate(urbGrad, urbGrad * Util.AsVectorSingle(Util.AsVectorInt32(negOne) ^ (urbHash & Util.Create(1 << 31))), urbGrad);
#endif
            Float ubLerp = Util.MultiplyAddEstimate(urbGrad - ulbGrad, sx, ulbGrad);

            fz += negOne;

            Float llfGrad = Util.MultiplyAddEstimate(
                fx, Util.AsVectorSingle(llfHash << GradShift1), Util.MultiplyAddEstimate(
                fy, Util.AsVectorSingle(llfHash << GradShift2),
                fz * Util.AsVectorSingle(llfHash << GradShift3)));
            Float lrfGrad = Util.MultiplyAddEstimate(
                fx + negOne, Util.AsVectorSingle(lrfHash << GradShift1), Util.MultiplyAddEstimate(
                fy, Util.AsVectorSingle(lrfHash << GradShift2),
                fz * Util.AsVectorSingle(lrfHash << GradShift3)));
#if QUADRATIC
            llfGrad = Util.MultiplyAddEstimate(llfGrad, llfGrad * Util.AsVectorSingle(Util.AsVectorInt32(negOne) ^ (llfHash & Util.Create(1 << 31))), llfGrad);
            lrfGrad = Util.MultiplyAddEstimate(lrfGrad, lrfGrad * Util.AsVectorSingle(Util.AsVectorInt32(negOne) ^ (lrfHash & Util.Create(1 << 31))), lrfGrad);
#endif
            Float lfLerp = Util.MultiplyAddEstimate(lrfGrad - llfGrad, sx, llfGrad);

            Float ulfGrad = Util.MultiplyAddEstimate(
                fx, Util.AsVectorSingle(ulfHash << GradShift1), Util.MultiplyAddEstimate(
                fy + negOne, Util.AsVectorSingle(ulfHash << GradShift2),
                fz * Util.AsVectorSingle(ulfHash << GradShift3)));
            Float urfGrad = Util.MultiplyAddEstimate(
                fx + negOne, Util.AsVectorSingle(urfHash << GradShift1), Util.MultiplyAddEstimate(
                fy + negOne, Util.AsVectorSingle(urfHash << GradShift2),
                fz * Util.AsVectorSingle(urfHash << GradShift3)));
#if QUADRATIC
            ulfGrad = Util.MultiplyAddEstimate(ulfGrad, ulfGrad * Util.AsVectorSingle(Util.AsVectorInt32(negOne) ^ (ulfHash & Util.Create(1 << 31))), ulfGrad);
            urfGrad = Util.MultiplyAddEstimate(urfGrad, urfGrad * Util.AsVectorSingle(Util.AsVectorInt32(negOne) ^ (urfHash & Util.Create(1 << 31))), urfGrad);
#endif
            Float ufLerp = Util.MultiplyAddEstimate(urfGrad - ulfGrad, sx, ulfGrad);

            Float bLerp = Util.MultiplyAddEstimate(ubLerp - lbLerp, sy, lbLerp);
            Float fLerp = Util.MultiplyAddEstimate(ufLerp - lfLerp, sy, lfLerp);

            Float result = Util.MultiplyAddEstimate(fLerp - bLerp, sz, bLerp);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (Float centerDist, Float edgeDist) CellularNoise2DVector(Float x, Float y, Int seed)
        {
            Float xFloor = Util.Floor(x);
            Float yFloor = Util.Floor(y);
            Int ix = Util.ConvertToInt32Native(xFloor);
            Int iy = Util.ConvertToInt32Native(yFloor);
            Float fx = x - xFloor;
            Float fy = y - yFloor;

            Int ConstX = Util.Create(180601904), ConstY = Util.Create(174181987);

            Int centerHash = ix * ConstX + iy * ConstY + seed;

            Float d1 = Util.Create(2f), d2 = Util.Create(2f);
            Float one = Util.Create(1f), two = Util.Create(2f);
            SingleCell2D(centerHash + ConstY, fx + one, fy, ref d1, ref d2);
            SingleCell2D(centerHash + ConstY - ConstX, fx + two, fy, ref d1, ref d2);
            SingleCell2D(centerHash + ConstY + ConstX, fx, fy, ref d1, ref d2);
            fy += one;
            SingleCell2D(centerHash, fx + one, fy, ref d1, ref d2);
            SingleCell2D(centerHash - ConstX, fx + two, fy, ref d1, ref d2);
            SingleCell2D(centerHash + ConstX, fx, fy, ref d1, ref d2);
            fy += one;
            SingleCell2D(centerHash - ConstY, fx + one, fy, ref d1, ref d2);
            SingleCell2D(centerHash - ConstY - ConstX, fx + two, fy, ref d1, ref d2);
            SingleCell2D(centerHash - ConstY + ConstX, fx, fy, ref d1, ref d2);

            d1 = Util.SquareRoot(d1);
            d2 = Util.SquareRoot(d2);

            Float edgeDist = d2 - d1;
            return (d1, edgeDist);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void SingleCell2D(Int hash, Float fx, Float fy, ref Float d1, ref Float d2)
        {
            Int ConstXOR = Util.Create(203663684);
            hash *= hash ^ ConstXOR;
            Int AndMask = Util.Create(unchecked((int)0b00000000011100000000011111111111));
            Int OrMask = Util.Create(unchecked((int)0b00111111100000111111100000000000));
            hash = (hash & AndMask) | OrMask;
            Float dx = fx - Util.AsVectorSingle(hash);
            Float dy = fy - Util.AsVectorSingle(hash << 12);
            Float d = Util.MultiplyAddEstimate(dx, dx, dy * dy);
#if CORECLR
            Int smallest = Util.LessThan(d, d1);
            Int secondSmallest = Util.LessThan(d, d2);
            d2 = Util.ConditionalSelect(smallest, d1, Util.ConditionalSelect(secondSmallest, d, d2));
            d1 = Util.ConditionalSelect(smallest, d, d1);
#else
            bool smallest = d < d1;
            d2 = smallest ? d1 : d < d2 ? d : d2;
            d1 = smallest ? d : d1;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (Float centerDist, Float edgeDist) CellularNoise3DVector(Float x, Float y, Float z, Int seed)
        {
            Float xFloor = Util.Floor(x);
            Float yFloor = Util.Floor(y);
            Float zFloor = Util.Floor(z);
            Int ix = Util.ConvertToInt32Native(xFloor);
            Int iy = Util.ConvertToInt32Native(yFloor);
            Int iz = Util.ConvertToInt32Native(zFloor);
            Float fx = x - xFloor;
            Float fy = y - yFloor;
            Float fz = z - zFloor;

            Int ConstX = Util.Create(180601904), ConstY = Util.Create(174181987), ConstZ = Util.Create(598742741);

            Int centerHash = ix * ConstX + iy * ConstY + iz * ConstZ + seed;

            Float d1 = Util.Create(2f), d2 = Util.Create(2f);
            Float one = Util.Create(1f), two = Util.Create(2f);
            SingleCell3D(centerHash + ConstY, fx + one, fy, fz, ref d1, ref d2);
            SingleCell3D(centerHash + ConstY - ConstX, fx + two, fy, fz, ref d1, ref d2);
            SingleCell3D(centerHash + ConstY + ConstX, fx, fy, fz, ref d1, ref d2);
            fy += one;
            SingleCell3D(centerHash, fx + one, fy, fz, ref d1, ref d2);
            SingleCell3D(centerHash - ConstX, fx + two, fy, fz, ref d1, ref d2);
            SingleCell3D(centerHash + ConstX, fx, fy, fz, ref d1, ref d2);
            fy += one;
            SingleCell3D(centerHash - ConstY, fx + one, fy, fz, ref d1, ref d2);
            SingleCell3D(centerHash - ConstY - ConstX, fx + two, fy, fz, ref d1, ref d2);
            SingleCell3D(centerHash - ConstY + ConstX, fx, fy, fz, ref d1, ref d2);

            fz += one;
            centerHash -= ConstZ;

            SingleCell3D(centerHash - ConstY, fx + one, fy, fz, ref d1, ref d2);
            SingleCell3D(centerHash - ConstY - ConstX, fx + two, fy, fz, ref d1, ref d2);
            SingleCell3D(centerHash - ConstY + ConstX, fx, fy, fz, ref d1, ref d2);
            fy -= one;
            SingleCell3D(centerHash, fx + one, fy, fz, ref d1, ref d2);
            SingleCell3D(centerHash - ConstX, fx + two, fy, fz, ref d1, ref d2);
            SingleCell3D(centerHash + ConstX, fx, fy, fz, ref d1, ref d2);
            fy -= one;
            SingleCell3D(centerHash + ConstY, fx + one, fy, fz, ref d1, ref d2);
            SingleCell3D(centerHash + ConstY - ConstX, fx + two, fy, fz, ref d1, ref d2);
            SingleCell3D(centerHash + ConstY + ConstX, fx, fy, fz, ref d1, ref d2);

            fz -= Util.Create(2f);
            centerHash += ConstZ * 2;

            SingleCell3D(centerHash + ConstY, fx + one, fy, fz, ref d1, ref d2);
            SingleCell3D(centerHash + ConstY - ConstX, fx + two, fy, fz, ref d1, ref d2);
            SingleCell3D(centerHash + ConstY + ConstX, fx, fy, fz, ref d1, ref d2);
            fy += one;
            SingleCell3D(centerHash, fx + one, fy, fz, ref d1, ref d2);
            SingleCell3D(centerHash - ConstX, fx + two, fy, fz, ref d1, ref d2);
            SingleCell3D(centerHash + ConstX, fx, fy, fz, ref d1, ref d2);
            fy += one;
            SingleCell3D(centerHash - ConstY, fx + one, fy, fz, ref d1, ref d2);
            SingleCell3D(centerHash - ConstY - ConstX, fx + two, fy, fz, ref d1, ref d2);
            SingleCell3D(centerHash - ConstY + ConstX, fx, fy, fz, ref d1, ref d2);

            d1 = Util.SquareRoot(d1);
            d2 = Util.SquareRoot(d2);

            Float edgeDist = d2 - d1;
            return (d1, edgeDist);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void SingleCell3D(Int hash, Float fx, Float fy, Float fz, ref Float d1, ref Float d2)
        {
            Int ConstXOR = Util.Create(203663684);
            hash *= (hash ^ ConstXOR) >> 16;
            Int AndMask = Util.Create(unchecked((int)0b11000000000110000000001111111111));
            Int OrMask =  Util.Create(unchecked((int)0b00001111111000011111110000000000));
            hash = (hash & AndMask) | OrMask;
            Float dx = fx - Util.AsVectorSingle(hash << 2);
            Float dy = fy - Util.AsVectorSingle(hash << 13);
#if CORECLR
            Float dz = fz - Util.Multiply(Util.ConvertToSingle(hash.As<int, uint>()), 1f / uint.MaxValue);
#else
            Float dz = fz - (uint)hash * 1f / uint.MaxValue;
#endif
            Float d = Util.MultiplyAddEstimate(dx, dx, Util.MultiplyAddEstimate(dy, dy, dz * dz));
#if CORECLR
            Int smallest = Util.LessThan(d, d1);
            Int secondSmallest = Util.LessThan(d, d2);
            d2 = Util.ConditionalSelect(smallest, d1, Util.ConditionalSelect(secondSmallest, d, d2));
            d1 = Util.ConditionalSelect(smallest, d, d1);
#else
            bool smallest = d < d1;
            d2 = smallest ? d1 : d < d2 ? d : d2;
            d1 = smallest ? d : d1;
#endif
        }
    }

    /// <summary>
    /// Scalar versions of functions in the System.Numerics.Vector class.
    /// </summary>
    static class ScalarUtil
    {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MultiplyAddEstimate(float a, float b, float c) => a * b + c;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Create(float f) => f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Create(int i) => i;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe float AsVectorSingle(int i) => *(float*)&i;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int AsVectorInt32(float f) => *(int*)&f;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ConvertToInt32Native(float f) => (int)f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if UNITY
        public static float Floor(float f) => math.floor(f);
#else
        public static float Floor(float f) => MathF.Floor(f);

#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if UNITY
        public static float SquareRoot(float f) => math.sqrt(f);
#else
        public static float SquareRoot(float f) => MathF.Sqrt(f);
#endif
    }

#if UNITY

    /// <summary>
    /// Burst Job for evaluating noise functions from the <see cref="Noise"/> class.
    /// Used by the functions in the <see cref="Noise"/> class internally, however if you want to run the job asynchronously you can use this struct.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    public unsafe struct BurstNoiseJob : IJob
    {
        public NoiseType noiseType;
        public int seed;
        public float xFrequency, yFrequency, zFrequency;
        public float amplitude1, amplitude2;

        [NoAlias]
        [NativeDisableUnsafePtrRestriction]
        public float* xBuffer, yBuffer, zBuffer, output1Buffer, output2Buffer;
        public int length;

        public unsafe void Execute()
        {
            switch (noiseType)
            {
                case NoiseType.GradientNoise2D:
                    Noise.GradientNoise2DBurst(xBuffer, yBuffer, output1Buffer, length, xFrequency, yFrequency, amplitude1, seed);
                    break;
                case NoiseType.GradientNoise3D:
                    Noise.GradientNoise3DBurst(xBuffer, yBuffer, zBuffer, output1Buffer, length, xFrequency, yFrequency,zFrequency, amplitude1, seed);
                    break;
                case NoiseType.CellularNoise2D:
                    Noise.CellularNoise2DBurst(xBuffer, yBuffer, output1Buffer, output2Buffer, length, xFrequency, yFrequency, amplitude1, amplitude2, seed);
                    break;
                case NoiseType.CellularNoise3D:
                    Noise.CellularNoise3DBurst(xBuffer, yBuffer, zBuffer, output1Buffer, output2Buffer, length, xFrequency, yFrequency, zFrequency, amplitude1, amplitude2, seed);
                    break;
            }
        }

        public static void RunGradientNoise2DJob(ReadOnlySpan<float> x, ReadOnlySpan<float> y, Span<float> output,
            float xFreq, float yFreq, float amplitude, int seed)
        {
            CreateGradientNoise2DJob(x, y, output, xFreq, yFreq, amplitude, seed).Run();
        }

        public static BurstNoiseJob CreateGradientNoise2DJob(ReadOnlySpan<float> x, ReadOnlySpan<float> y, Span<float> output, float xFreq, float yFreq, float amplitude, int seed)
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
                        BurstNoiseJob job = new();
                        job.noiseType = NoiseType.GradientNoise2D;
                        job.xBuffer = xPtr;
                        job.yBuffer = yPtr;
                        job.output1Buffer = outPtr;
                        job.length = x.Length;
                        job.amplitude1 = amplitude;
                        job.xFrequency = xFreq;
                        job.yFrequency = yFreq;
                        job.seed = seed;
                        return job;
                    }
                }
            }
        }

        public static void RunGradientNoise3DJob(ReadOnlySpan<float> x, ReadOnlySpan<float> y, ReadOnlySpan<float> z, Span<float> output, float xFreq, float yFreq, float zFreq, float amplitude, int seed)
        {
            CreateGradientNoise3DJob(x, y, z, output, xFreq, yFreq, zFreq, amplitude, seed).Run();
        }
        
        public static BurstNoiseJob CreateGradientNoise3DJob(ReadOnlySpan<float> x, ReadOnlySpan<float> y, ReadOnlySpan<float> z, Span<float> output, float xFreq, float yFreq, float zFreq, float amplitude, int seed)
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
                            BurstNoiseJob job = new();
                            job.noiseType = NoiseType.GradientNoise3D;
                            job.xBuffer = xPtr;
                            job.yBuffer = yPtr;
                            job.zBuffer = zPtr;
                            job.output1Buffer = outPtr;
                            job.length = x.Length;
                            job.amplitude1 = amplitude;
                            job.xFrequency = xFreq;
                            job.yFrequency = yFreq;
                            job.zFrequency = zFreq;
                            job.seed = seed;
                            return job;
                        }
                    }
                }
            }
        }

        public static void RunCellularNoise2DJob(ReadOnlySpan<float> x, ReadOnlySpan<float> y,
            Span<float> centerDistOutput, Span<float> edgeDistOutput, float xFreq, float yFreq,
            float centerDistAmplitude, float edgeDistAmplitude, int seed)
        {
            CreateCellularNoise2DJob(x, y, centerDistOutput, edgeDistOutput, xFreq, yFreq, centerDistAmplitude,
                edgeDistAmplitude, seed).Run();
        }

        public static BurstNoiseJob CreateCellularNoise2DJob(ReadOnlySpan<float> x, ReadOnlySpan<float> y, Span<float> centerDistOutput, Span<float> edgeDistOutput, float xFreq, float yFreq, float centerDistAmplitude, float edgeDistAmplitude, int seed)
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
                            BurstNoiseJob job = new();
                            job.noiseType = NoiseType.CellularNoise2D;
                            job.xBuffer = xPtr;
                            job.yBuffer = yPtr;
                            job.output1Buffer = centerOutPtr;
                            job.output2Buffer = edgeOutPtr;
                            job.length = centerDistOutput.Length;
                            job.amplitude1 = centerDistAmplitude;
                            job.amplitude2 = edgeDistAmplitude;
                            job.xFrequency = xFreq;
                            job.yFrequency = yFreq;
                            job.seed = seed;
                            return job;
                        }
                    }
                }
            }
        }

        public static void RunCellularNoise3DJob(ReadOnlySpan<float> x, ReadOnlySpan<float> y, ReadOnlySpan<float> z,
            Span<float> centerDistOutput, Span<float> edgeDistOutput, float xFreq, float yFreq, float zFreq,
            float centerDistAmplitude, float edgeDistAmplitude, int seed)
        {
            CreateCellularNoise3DJob(x, y, z, centerDistOutput, edgeDistOutput, xFreq, yFreq, zFreq,
                centerDistAmplitude, edgeDistAmplitude, seed).Run();
        }

        public static BurstNoiseJob CreateCellularNoise3DJob(ReadOnlySpan<float> x, ReadOnlySpan<float> y, ReadOnlySpan<float> z, Span<float> centerDistOutput, Span<float> edgeDistOutput, float xFreq, float yFreq, float zFreq, float centerDistAmplitude, float edgeDistAmplitude, int seed)
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
                                BurstNoiseJob job = new();
                                job.noiseType = NoiseType.CellularNoise3D;
                                job.xBuffer = xPtr;
                                job.yBuffer = yPtr;
                                job.zBuffer = zPtr;
                                job.output1Buffer = centerOutPtr;
                                job.output2Buffer = edgeOutPtr;
                                job.length = centerDistOutput.Length;
                                job.amplitude1 = centerDistAmplitude;
                                job.amplitude2 = edgeDistAmplitude;
                                job.xFrequency = xFreq;
                                job.yFrequency = yFreq;
                                job.zFrequency = zFreq;
                                job.seed = seed;
                                return job;
                            }
                        }
                    }
                }
            }
        }
    }
#endif
}