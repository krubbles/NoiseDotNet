#if NETCOREAPP
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;
namespace CSharpNoise
{
    public static class NetCoreNoise
    {
        public static void QuadraticNoise2D(CoordinateGrid coordGrid, float frequency, float amplitude, int seed, Span<float> output)
        {
            float xStart = coordGrid.xStart, yStart = coordGrid.yStart;
            float xStep = coordGrid.xStep, yStep = coordGrid.yStep;
            int width = coordGrid.width, height = coordGrid.height;
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentException($"Coordinate grid has a width of {width} and height of {height}. Width and height must both be positive.");
            }
            Span<float> vecSpan = stackalloc float[Vector<float>.Count];
            for (int i = 0; i < vecSpan.Length; i++)
            {
                vecSpan[i] = i * xStep;
            }
            Vector<float> stepsVec = new(vecSpan);

            Vector<float> ampVec = new(amplitude);
            Vector<int> seedVec = new(seed);

            for (int iy = 0; iy < height; iy++)
            {
                float y = yStart + iy * yStep;
                Vector<float> yVec = new(yStart);

                for (int ix = 0; ix <= width - Vector<float>.Count; ix += Vector<float>.Count)
                {
                    Vector<float> xVec = new Vector<float>(xStart + ix * xStep) + stepsVec;
                    Vector<float> result = QuadraticNoiseVector(xVec, yVec, seedVec) * ampVec;
                    result.CopyTo(output[ix..(ix + Vector<float>.Count)]);
                }
            }
        }

        public static unsafe void QuadraticNoise2D(Span<float> xCoords, Span<float> yCoords, float frequency, float amplitude, int seed, Span<float> output)
        {
            int length = xCoords.Length;
            Vector<int> seedVec = new(seed);
            Vector<float> freqVec = new(frequency);
            for (int i = 0; i <= length - Vector<float>.Count; i += Vector<float>.Count)
            {
                Vector<float> xVec = Vector.LoadUnsafe(ref xCoords[i]) * freqVec;
                Vector<float> yVec = Vector.LoadUnsafe(ref yCoords[i]) * freqVec;
                Vector<float> result = QuadraticNoiseVector(xVec, yVec, seedVec);
                result.CopyTo(output[i..(i + Vector<float>.Count)]);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector<float> QuadraticNoiseVector(Vector<float> x, Vector<float> y, Vector<int> seed)
        {
            Vector<float> xFloor = Vector.Floor(x);
            Vector<float> yFloor = Vector.Floor(y);
            Vector<int> ix = Vector.ConvertToInt32Native(xFloor);
            Vector<int> iy = Vector.ConvertToInt32Native(yFloor);
            Vector<float> fx = x - xFloor;
            Vector<float> fy = y - yFloor;

            Vector<int> ConstX = new(180601904), ConstY = new(174181987), ConstXOR = new(203663684);

            Vector<int> llHash = ix * ConstX + iy * ConstY + seed;
            Vector<int> lrHash = llHash + ConstX;
            Vector<int> ulHash = llHash + ConstY;
            Vector<int> urHash = llHash + ConstX + ConstY;

            llHash *= llHash ^ ConstXOR;
            lrHash *= lrHash ^ ConstXOR;
            ulHash *= ulHash ^ ConstXOR;
            urHash *= urHash ^ ConstXOR;

            Vector<int> GradAndMask = new(unchecked((int)0b11000000001100000000100000000111));
            Vector<int> GradOrMask = new(unchecked((int)0b00011111100001111111001111101000));

            llHash = (llHash & GradAndMask) | GradOrMask;
            lrHash = (lrHash & GradAndMask) | GradOrMask;
            ulHash = (ulHash & GradAndMask) | GradOrMask;
            urHash = (urHash & GradAndMask) | GradOrMask;

            Vector<float> fxm1 = fx - new Vector<float>(1);
            Vector<float> fym1 = fy - new Vector<float>(1);

            const int GradShift1 = 1, GradShift2 = 20, GradShift3 = 11;
            Vector<float> llGrad = Vector.MultiplyAddEstimate(
                BlendVPS(llHash, fx, fy), (llHash << GradShift1).As<int, float>(),
                BlendVPS(llHash, fy, fx) * (llHash << GradShift2).As<int, float>());
            Vector<float> lrGrad = Vector.MultiplyAddEstimate(
                BlendVPS(lrHash, fxm1, fy), (lrHash << GradShift1).As<int, float>(),
                BlendVPS(lrHash, fy, fxm1) * (lrHash << GradShift2).As<int, float>());
            Vector<float> ulGrad = Vector.MultiplyAddEstimate(
                BlendVPS(ulHash, fx, fym1), (ulHash << GradShift1).As<int, float>(),
                BlendVPS(ulHash, fym1, fx) * (ulHash << GradShift2).As<int, float>());
            Vector<float> urGrad = Vector.MultiplyAddEstimate(
                BlendVPS(urHash, fxm1, fym1), (urHash << GradShift1).As<int, float>(),
                BlendVPS(urHash, fym1, fxm1) * (urHash << GradShift2).As<int, float>());


            // this is the quadratic part. Removing this gives you pure Perlin Noise. 
            //llGrad = Vector.MultiplyAddEstimate(llGrad, llGrad * (llHash << GradShift3).As<int, float>(), llGrad);
            //lrGrad = Vector.MultiplyAddEstimate(lrGrad, lrGrad * (lrHash << GradShift3).As<int, float>(), lrGrad);
            //ulGrad = Vector.MultiplyAddEstimate(ulGrad, ulGrad * (ulHash << GradShift3).As<int, float>(), ulGrad);
            //urGrad = Vector.MultiplyAddEstimate(urGrad, urGrad * (urHash << GradShift3).As<int, float>(), urGrad);

            Vector<float> sx = fx * fx * fx * Vector.MultiplyAddEstimate(Vector.MultiplyAddEstimate(fx, new(6f), new(-15f)), fx, new Vector<float>(10f));
            Vector<float> sy = fy * fy * fy * Vector.MultiplyAddEstimate(Vector.MultiplyAddEstimate(fy, new(6f), new(-15f)), fy, new Vector<float>(10f));

            Vector<float> lLerp = Vector.MultiplyAddEstimate(lrGrad - llGrad, sx, llGrad);
            Vector<float> uLerp = Vector.MultiplyAddEstimate(urGrad - ulGrad, sx, ulGrad);
            Vector<float> result = Vector.MultiplyAddEstimate(uLerp - lLerp, sy, lLerp);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector<float> BlendVPS(Vector<int> selector, Vector<float> a, Vector<float> b)
        {
            if (Avx.IsSupported)
            {
                return Avx.BlendVariable(a.AsVector256(), b.AsVector256(), selector.AsVector256().AsSingle()).AsVector();
            }
            else if (Sse41.IsSupported)
            {
                return Sse41.BlendVariable(a.AsVector128(), b.AsVector128(), selector.AsVector128().AsSingle()).AsVector();
            }
            else return Vector.ConditionalSelect(Vector.LessThan(selector, Vector.Create(0)), a, b); 
        }       
    }
}
#endif
