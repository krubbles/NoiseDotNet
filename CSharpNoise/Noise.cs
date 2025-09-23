#define VECTOR
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;
#if VECTOR
using Int =  System.Numerics.Vector<int>;
using Float = System.Numerics.Vector<float>;
using Util = System.Numerics.Vector;
#else
using Int = int;
using Float = float;
using Util = CSharpNoise.ScalarUtil;
#endif

// using Vector = CSharpNoise.Scalar;
namespace CSharpNoise
{
    public static class Noise
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static unsafe void QuadraticNoise2D(Span<float> xCoords, Span<float> yCoords, float xFreq, float yFreq, float amplitude, int seed, Span<float> output)
        {
#if VECTOR
            int length = xCoords.Length;
            Int seedVec = Util.Create(seed);
            Float xfVec = Util.Create(xFreq), yfVec = Util.Create(yFreq);
            for (int i = 0; i < length - Float.Count; i += Float.Count)
            {
                Float xVec = Util.LoadUnsafe(ref xCoords[i]) * xfVec;
                Float yVec = Util.LoadUnsafe(ref yCoords[i]) * yfVec;
                Float result = QuadraticNoise2DVector(xVec, yVec, seedVec);
                result.StoreUnsafe(ref output[i]);
            }
            int endIndex = length - Float.Count;
            Float xEnd = Util.LoadUnsafe(ref xCoords[endIndex]) * xfVec;
            Float yEnd = Util.LoadUnsafe(ref yCoords[endIndex]) * yfVec;
            Float resultEnd = QuadraticNoise2DVector(xEnd, yEnd, seedVec);
            resultEnd.StoreUnsafe(ref output[endIndex]);
#else
            for (int i = 0; i < xCoords.Length; ++i)
            {
                output[i] = QuadraticNoise2DVector(xCoords[i] * xFreq, yCoords[i] * yFreq, seed) * amplitude;
            }
#endif
        }

        public static unsafe void QuadraticNoise3D(Span<float> xCoords, Span<float> yCoords, Span<float> zCoords, float xFreq, float yFreq, float zFreq, float amplitude, int seed, Span<float> output)
        {
#if VECTOR
            int length = xCoords.Length;
            Int seedVec = Util.Create(seed);
            Float xfVec = Util.Create(xFreq), yfVec = Util.Create(yFreq), zfVec = Util.Create(zFreq);
            for (int i = 0; i < length - Float.Count; i += Float.Count)
            {
                Float xVec = Util.LoadUnsafe(ref xCoords[i]) * xfVec;
                Float yVec = Util.LoadUnsafe(ref yCoords[i]) * yfVec;
                Float zVec = Util.LoadUnsafe(ref zCoords[i]) * zfVec;
                Float result = QuadraticNoise3DVector(xVec, yVec, zVec, seedVec);
                result.StoreUnsafe(ref output[i]);
            }
            int endIndex = length - Float.Count;
            Float xEnd = Util.LoadUnsafe(ref xCoords[endIndex]) * xfVec;
            Float yEnd = Util.LoadUnsafe(ref yCoords[endIndex]) * xfVec;
            Float zEnd = Util.LoadUnsafe(ref zCoords[endIndex]) * zfVec;
            Float resultEnd = QuadraticNoise3DVector(xEnd, yEnd, zEnd, seedVec);
            resultEnd.StoreUnsafe(ref output[endIndex]);
#else
            for (int i = 0; i < xCoords.Length; ++i)
            {
                output[i] = QuadraticNoise3DVector(xCoords[i] * xFreq, yCoords[i] * yFreq, zCoords[i] * zFreq, seed);
            }
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static unsafe void CellularNoise2D(Span<float> xCoords, Span<float> yCoords, float xFreq, float yFreq, float centerDistAmplitude, float edgeDistAmplitude, int seed, Span<float> centerDistOut, Span<float> edgeDistOut)
        {
#if VECTOR
            int length = xCoords.Length;
            Int seedVec = Util.Create(seed);
            Float xfVec = Util.Create(xFreq), yfVec = Util.Create(yFreq);
            for (int i = 0; i < length - Float.Count; i += Float.Count)
            {
                Float xVec = Util.LoadUnsafe(ref xCoords[i]) * xfVec;
                Float yVec = Util.LoadUnsafe(ref yCoords[i]) * yfVec;
                (Float centerDist, Float edgeDist) = CellularNoise2DVector(xVec, yVec, seedVec);
                centerDist *= centerDistAmplitude;
                edgeDist *= edgeDistAmplitude;
                centerDist.StoreUnsafe(ref centerDistOut[i]);
                edgeDist.StoreUnsafe(ref edgeDistOut[i]);
            }
            int endIndex = length - Float.Count;
            Float xEnd = Util.LoadUnsafe(ref xCoords[endIndex]) * xfVec;
            Float yEnd = Util.LoadUnsafe(ref yCoords[endIndex]) * yfVec;
            (Float centerDistEnd, Float edgeDistEnd) = CellularNoise2DVector(xEnd, yEnd, seedVec);
            centerDistEnd *= centerDistAmplitude;
            edgeDistEnd *= edgeDistAmplitude;
            centerDistEnd.StoreUnsafe(ref centerDistOut[endIndex]);
            edgeDistEnd.StoreUnsafe(ref edgeDistOut[endIndex]);
#else
            for (int i = 0; i < xCoords.Length; ++i)
            {
                (float centerDist, float edgeDist) = CellularNoise2DVector(xCoords[i] * xFreq, yCoords[i] * yFreq, seed);
                centerDistOut[i] = centerDist * centerDistAmplitude;
                edgeDistOut[i] = edgeDist * centerDistAmplitude;
            }
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static unsafe void CellularNoise3D(Span<float> xCoords, Span<float> yCoords, Span<float> zCoords, float xFreq, float yFreq, float zFreq, float centerDistAmplitude, float edgeDistAmplitude, int seed, Span<float> centerDistOut, Span<float> edgeDistOut)
        {
#if VECTOR
            int length = xCoords.Length;
            Int seedVec = Util.Create(seed);
            Float xfVec = Util.Create(xFreq), yfVec = Util.Create(yFreq), zfVec = Util.Create(zFreq);
            for (int i = 0; i < length - Float.Count; i += Float.Count)
            {
                Float xVec = Util.LoadUnsafe(ref xCoords[i]) * xfVec;
                Float yVec = Util.LoadUnsafe(ref yCoords[i]) * yfVec;
                Float zVec = Util.LoadUnsafe(ref zCoords[i]) * zfVec;

                (Float centerDist, Float edgeDist) = CellularNoise3DVector(xVec, yVec, zVec, seedVec);
                centerDist *= centerDistAmplitude;
                edgeDist *= edgeDistAmplitude;
                centerDist.StoreUnsafe(ref centerDistOut[i]);
                edgeDist.StoreUnsafe(ref edgeDistOut[i]);
            }
            int endIndex = length - Float.Count;
            Float xEnd = Util.LoadUnsafe(ref xCoords[endIndex]) * xfVec;
            Float yEnd = Util.LoadUnsafe(ref yCoords[endIndex]) * yfVec;
            Float zEnd = Util.LoadUnsafe(ref zCoords[endIndex]) * zfVec;
            (Float centerDistEnd, Float edgeDistEnd) = CellularNoise3DVector(xEnd, yEnd, zEnd, seedVec);
            centerDistEnd *= centerDistAmplitude;
            edgeDistEnd *= edgeDistAmplitude;
            centerDistEnd.StoreUnsafe(ref centerDistOut[endIndex]);
            edgeDistEnd.StoreUnsafe(ref edgeDistOut[endIndex]);
#else
            for (int i = 0; i < xCoords.Length; ++i)
            {
                (float centerDist, float edgeDist) = CellularNoise3DVector(xCoords[i] * xFreq, yCoords[i] * yFreq, zCoords[i] * yFreq, seed);
                centerDistOut[i] = centerDist * centerDistAmplitude;
                edgeDistOut[i] = edgeDist * centerDistAmplitude;
            }
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Float QuadraticNoise2DVector(Float x, Float y, Int seed)
        {
            Float xFloor = Util.Floor(x);
            Float yFloor = Util.Floor(y);
            Int ix = Util.ConvertToInt32Native(xFloor);
            Int iy = Util.ConvertToInt32Native(yFloor);
            Float fx = x - xFloor;
            Float fy = y - yFloor;

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

            const int GradShift1 = 1, GradShift2 = 20, GradShift3 = 11;
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


            // this is the quadratic part. Removing this gives you pure Perlin Noise. 
#if true
            llGrad = Util.MultiplyAddEstimate(llGrad, llGrad * Util.AsVectorSingle(llHash << GradShift3), llGrad);
            lrGrad = Util.MultiplyAddEstimate(lrGrad, lrGrad * Util.AsVectorSingle(lrHash << GradShift3), lrGrad);
            ulGrad = Util.MultiplyAddEstimate(ulGrad, ulGrad * Util.AsVectorSingle(ulHash << GradShift3), ulGrad);
            urGrad = Util.MultiplyAddEstimate(urGrad, urGrad * Util.AsVectorSingle(urHash << GradShift3), urGrad);
#endif

            Float sx = fx * fx * fx * Util.MultiplyAddEstimate(Util.MultiplyAddEstimate(fx, Util.Create(6f), Util.Create(-15f)), fx, Util.Create(10f));
            Float sy = fy * fy * fy * Util.MultiplyAddEstimate(Util.MultiplyAddEstimate(fy, Util.Create(6f), Util.Create(-15f)), fy, Util.Create(10f));

            Float lLerp = Util.MultiplyAddEstimate(lrGrad - llGrad, sx, llGrad);
            Float uLerp = Util.MultiplyAddEstimate(urGrad - ulGrad, sx, ulGrad);
            Float result = Util.MultiplyAddEstimate(uLerp - lLerp, sy, lLerp);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Float QuadraticNoise3DVector(Float x, Float y, Float z, Int seed)
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

            Int ConstX = Util.Create(180601904), ConstY = Util.Create(174181987), ConstZ = Util.Create(435040429), ConstXOR = Util.Create(203663684);

            // X: (lower/upper) Y: (left/right) Z: (back/front). 
            Int llbHash = ix * ConstX + iy * ConstY + iz * ConstZ + seed;
            Int lrbHash = llbHash + ConstX;
            Int ulbHash = llbHash + ConstY;
            Int urbHash = llbHash + ConstX + ConstY;
            Int llfHash = llbHash + ConstZ;
            Int lrfHash = llbHash + ConstZ + ConstX;
            Int ulfHash = llbHash + ConstZ + ConstY;
            Int urfHash = llbHash + ConstZ + ConstX + ConstY;

            llbHash *= llbHash ^ ConstXOR;
            lrbHash *= lrbHash ^ ConstXOR;
            ulbHash *= ulbHash ^ ConstXOR;
            urbHash *= urbHash ^ ConstXOR;
            llfHash *= llfHash ^ ConstXOR;
            lrfHash *= lrfHash ^ ConstXOR;
            ulfHash *= ulfHash ^ ConstXOR;
            urfHash *= urfHash ^ ConstXOR;

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
                fx,          Util.AsVectorSingle(llbHash << GradShift1), Util.MultiplyAddEstimate(
                fy,          Util.AsVectorSingle(llbHash << GradShift2),
                fz *         Util.AsVectorSingle(llbHash << GradShift3)));
            Float lrbGrad = Util.MultiplyAddEstimate(
                fx + negOne, Util.AsVectorSingle(lrbHash << GradShift1), Util.MultiplyAddEstimate(
                fy,          Util.AsVectorSingle(lrbHash << GradShift2),
                fz *         Util.AsVectorSingle(lrbHash << GradShift3)));
            Float lbLerp = Util.MultiplyAddEstimate(lrbGrad - llbGrad, sx, llbGrad);

            Float ulbGrad = Util.MultiplyAddEstimate(
                fx,          Util.AsVectorSingle(ulbHash << GradShift1), Util.MultiplyAddEstimate(
                fy + negOne, Util.AsVectorSingle(ulbHash << GradShift2),
                fz *         Util.AsVectorSingle(ulbHash << GradShift3)));
            Float urbGrad = Util.MultiplyAddEstimate(
                fx + negOne, Util.AsVectorSingle(urbHash << GradShift1), Util.MultiplyAddEstimate(
                fy + negOne, Util.AsVectorSingle(urbHash << GradShift2),
                fz *         Util.AsVectorSingle(urbHash << GradShift3)));
            Float ubLerp = Util.MultiplyAddEstimate(urbGrad - ulbGrad, sx, ulbGrad);

            fz += negOne;

            Float llfGrad = Util.MultiplyAddEstimate(
                fx,          Util.AsVectorSingle(llfHash << GradShift1), Util.MultiplyAddEstimate(
                fy,          Util.AsVectorSingle(llfHash << GradShift2),
                fz *         Util.AsVectorSingle(llfHash << GradShift3)));
            Float lrfGrad = Util.MultiplyAddEstimate(
                fx + negOne, Util.AsVectorSingle(lrfHash << GradShift1), Util.MultiplyAddEstimate(
                fy,          Util.AsVectorSingle(lrfHash << GradShift2),
                fz *         Util.AsVectorSingle(lrfHash << GradShift3)));
            Float lfLerp = Util.MultiplyAddEstimate(lrfGrad - llfGrad, sx, llfGrad);

            Float ulfGrad = Util.MultiplyAddEstimate(
                fx,          Util.AsVectorSingle(ulfHash << GradShift1), Util.MultiplyAddEstimate(
                fy + negOne, Util.AsVectorSingle(ulfHash << GradShift2),
                fz *         Util.AsVectorSingle(ulfHash << GradShift3)));
            Float urfGrad =  Util.MultiplyAddEstimate(
                fx + negOne, Util.AsVectorSingle(urfHash << GradShift1), Util.MultiplyAddEstimate(
                fy + negOne, Util.AsVectorSingle(urfHash << GradShift2),
                fz *         Util.AsVectorSingle(urfHash << GradShift3)));
            Float ufLerp = Util.MultiplyAddEstimate(urfGrad - ulfGrad, sx, ulfGrad);

            // this is the quadratic part. Removing this gives you pure Perlin Noise. 
#if true
            llfGrad = Util.MultiplyAddEstimate(llfGrad, llfGrad * Util.AsVectorSingle(llfHash << GradShift3), llfGrad);
            lrfGrad = Util.MultiplyAddEstimate(lrfGrad, lrfGrad * Util.AsVectorSingle(lrfHash << GradShift3), lrfGrad);
            ulfGrad = Util.MultiplyAddEstimate(ulfGrad, ulfGrad * Util.AsVectorSingle(ulfHash << GradShift3), ulfGrad);
            urfGrad = Util.MultiplyAddEstimate(urfGrad, urfGrad * Util.AsVectorSingle(urfHash << GradShift3), urfGrad);
            llbGrad = Util.MultiplyAddEstimate(llbGrad, llbGrad * Util.AsVectorSingle(llbHash << GradShift3), llbGrad);
            lrbGrad = Util.MultiplyAddEstimate(lrbGrad, lrbGrad * Util.AsVectorSingle(lrbHash << GradShift3), lrbGrad);
            ulbGrad = Util.MultiplyAddEstimate(ulbGrad, ulbGrad * Util.AsVectorSingle(ulbHash << GradShift3), ulbGrad);
            urbGrad = Util.MultiplyAddEstimate(urbGrad, urbGrad * Util.AsVectorSingle(urbHash << GradShift3), urbGrad);
#endif

            Float bLerp = Util.MultiplyAddEstimate(ubLerp - lbLerp, sy, lbLerp);
            Float fLerp = Util.MultiplyAddEstimate(ufLerp - lfLerp, sy, lfLerp);

            Float result = Util.MultiplyAddEstimate(fLerp - bLerp, sz, bLerp);
            return result;
        }

        static (Float centerDist, Float edgeDist) CellularNoise2DVector(Float x, Float y, Int seed)
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
            SingleCell(centerHash + ConstY, fx + one, fy, ref d1, ref d2);
            SingleCell(centerHash + ConstY - ConstX, fx + two, fy, ref d1, ref d2);
            SingleCell(centerHash + ConstY + ConstX, fx, fy, ref d1, ref d2);
            fy += one;
            SingleCell(centerHash, fx + one, fy, ref d1, ref d2);
            SingleCell(centerHash - ConstX, fx + two, fy, ref d1, ref d2);
            SingleCell(centerHash + ConstX, fx, fy, ref d1, ref d2);
            fy += one;
            SingleCell(centerHash - ConstY, fx + one, fy, ref d1, ref d2);
            SingleCell(centerHash - ConstY - ConstX, fx + two, fy, ref d1, ref d2);
            SingleCell(centerHash - ConstY + ConstX, fx, fy, ref d1, ref d2);

            d1 = Util.SquareRoot(d1);
            d2 = Util.SquareRoot(d2);

            Float edgeDist = d2 - d1;
            return (d1, edgeDist);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void SingleCell(Int hash, Float fx, Float fy, ref Float d1, ref Float d2)
        {
            Int ConstXOR = Util.Create(203663684);
            hash *= hash ^ ConstXOR;
            Int AndMask = Util.Create(unchecked((int)0b00000000011100000000011111111111));
            Int OrMask  = Util.Create(unchecked((int)0b00111111100000111111100000000000));
            hash = (hash & AndMask) | OrMask;
            Float dx = fx - Util.AsVectorSingle(hash);
            Float dy = fy - Util.AsVectorSingle(hash << 12);
            Float d = Util.MultiplyAddEstimate(dx, dx, dy * dy);
#if VECTOR
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

        static (Float centerDist, Float edgeDist) CellularNoise3DVector(Float x, Float y, Float z, Int seed)
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

            Int ConstX = Util.Create(180601904), ConstY = Util.Create(174181987), ConstZ = Util.Create(435040429);

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
        static void SingleCell3D(Int hash, Float fx, Float fy,Float fz, ref Float d1, ref Float d2)
        {
            Int ConstXOR = Util.Create(203663684);
            hash *= hash ^ ConstXOR;
            Int AndMask = Util.Create(unchecked((int)0b11100000000011100000000011111111));
            Int OrMask =  Util.Create(unchecked((int)0b00000111111100000111111100000000));
            hash = (hash & AndMask) | OrMask;
            Float dx = fx - Util.AsVectorSingle(hash << 3);
            Float dy = fy - Util.AsVectorSingle(hash << 15);
            Float dz = fz - Util.Multiply(Util.ConvertToSingle(hash.As<int, uint>()), 1f / uint.MaxValue);
            Float d = Util.MultiplyAddEstimate(dx, dx, Util.MultiplyAddEstimate(dy, dy, dz * dz));
#if VECTOR
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

#if VECTOR
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Float AsFloat(this Int vint)
        {
            return vint.As<int, float>();
        }
#endif


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Float BlendVPS(Int selector, Float a, Float b)
        {
#if VECTOR
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
            return selector < 0 ? a : b;
#endif
        }
    }

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
        public static int ConvertToInt32Native(float f) => (int)f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Floor(float f) => MathF.Floor(f);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SquareRoot(float f) => MathF.Sqrt(f);

    }
}
