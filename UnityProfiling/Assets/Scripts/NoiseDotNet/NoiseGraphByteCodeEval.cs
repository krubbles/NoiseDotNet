#if UNITY_2017_1_OR_NEWER
#define UNITY
#else
#define CORECLR
#endif

// As with Noise.cs, this file is written to be compatible with both Unity and CoreCLR.
// In CoreCLR, vectorization is achieved using the System.Numerics.Vector<T> API.
// In Unity, vectorization is achieved using Burst auto-vectorization, so the instruction
// loop below is run inside a Burst job (see BurstNoiseByteCodeJob) and noise instructions
// call the pointer-based *Burst noise functions, since Burst compiled code cannot schedule
// or run another Unity Job.

#if CORECLR
using System.Numerics;
using Float = System.Numerics.Vector<float>;
using Util = System.Numerics.Vector;
#else
using Float = System.Single;
using Util = NoiseDotNet.ScalarUtil;
#endif

using System;
using System.Runtime.CompilerServices;

namespace NoiseDotNet
{
    /// <summary>
    /// Compilation and evaluation utilities for NoiseNode bytecode.
    /// </summary>
    public static unsafe partial class NoiseGraphByteCodeEval
    {
        public const byte CopyOpCode = byte.MaxValue;

        // bump if needed
        const int MaxInputCount = 4;
        const int MaxOutputCount = 4;

        /// <summary>
        /// Evaluates compiled NoiseNode bytecode over a batch of values.
        /// <para>
        /// Each register occupies one contiguous section of <paramref name="registerBuffer"/> with length <paramref name="batchSize"/>.
        /// The value for a given register index and sample index is at index (register index * batchSize + sample index) of <paramref name="registerBuffer"/>.
        /// </para>
        /// <para>
        /// The first inputCount registers are inputs. They should be prepopulated in <paramref name="registerBuffer"/> before calling.
        /// On return, the first outputCount registers contain the result.
        /// </para>
        /// </summary>
        /// <param name="bytecode">Bytecode produced by <see cref="Compile"/>.</param>
        /// <param name="seed">Evaluation seed.</param>
        /// <param name="registerBuffer">Register memory storage. size should be registerCount * batchSize.</param>
        /// <param name="batchSize">Number of evaluations.</param>
        public static void EvaluateByteCode(ReadOnlySpan<byte> bytecode, int seed, Span<float> registerBuffer, int batchSize)
        {
            fixed (byte* bytecodePtr = bytecode)
            fixed (float* registerSpacePtr = registerBuffer)
            {
                EvaluateByteCode(bytecodePtr, bytecode.Length, seed, registerSpacePtr, registerBuffer.Length, batchSize);
            }
        }

        /// <summary>
        /// Evaluates compiled NoiseNode bytecode over a batch of values.
        /// <para>
        /// Each register occupies one contiguous section of <paramref name="registerBuffer"/> with length <paramref name="batchSize"/>.
        /// The value for a given register index and sample index is at index (register index * batchSize + sample index) of <paramref name="registerBuffer"/>.
        /// </para>
        /// <para>
        /// The first inputCount registers are inputs. They should be prepopulated in <paramref name="registerBuffer"/> before calling.
        /// On return, the first outputCount registers contain the result.
        /// </para>
        /// </summary>
        /// <param name="bytecode">Bytecode produced by <see cref="Compile"/>.</param>
        /// <param name="seed">Evaluation seed.</param>
        /// <param name="registerBuffer">Register memory storage. size should be registerCount * batchSize.</param>
        /// <param name="batchSize">Number of evaluations.</param>
        public static void EvaluateByteCode(byte* bytecode, int bytecodeLength, int seed, float* registerBuffer, int registerSpaceLength, int batchSize)
        {
            if (batchSize < 0)
                throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Batch size cannot be negative.");

            int offset = 0;
            ByteCodeInfo info = Read<ByteCodeInfo>(bytecode, bytecodeLength, ref offset);
            ValidateByteCodeInfo(info);

            long requiredRegisterSpaceLong = (long)info.RegisterCount * batchSize;
            if (requiredRegisterSpaceLong > int.MaxValue)
            {
                throw new ArgumentException(
                    $"The compiled graph requires {info.RegisterCount} registers and the requested batch size is {batchSize}, " +
                    $"which would require a registerBuffer of length {requiredRegisterSpaceLong}. Maximum register buffer length is {int.MaxValue}; " +
                    "use a smaller batch size or compile a graph that requires fewer registers.",
                    nameof(batchSize));
            }
            int requiredRegisterSpace = (int)requiredRegisterSpaceLong;

            if (registerSpaceLength < requiredRegisterSpace)
            {
                throw new ArgumentException(
                    $"Register buffer contains {registerSpaceLength} values, but the bytecode requires at least {requiredRegisterSpace}.",
                    nameof(registerBuffer));
            }

            // init constants
            for (int constantIndex = 0; constantIndex < info.ConstantCount; constantIndex++)
            {
                float value = Read<float>(bytecode, bytecodeLength, ref offset);
                Fill(GetRegister(registerBuffer, info.InputCount + constantIndex, batchSize), batchSize, value);
            }

            int* inputs = stackalloc int[MaxInputCount];
            int* outputs = stackalloc int[MaxOutputCount];
            while (offset < bytecodeLength)
            {
                byte opCode = Read<byte>(bytecode, bytecodeLength, ref offset);
                if (opCode == CopyOpCode)
                {
                    int source = ReadRegisterIndex(bytecode, bytecodeLength, ref offset, info.RegisterCount);
                    int destination = ReadRegisterIndex(bytecode, bytecodeLength, ref offset, info.RegisterCount);
                    Copy(
                        GetRegister(registerBuffer, source, batchSize),
                        GetRegister(registerBuffer, destination, batchSize),
                        batchSize);
                    continue;
                }

                NoiseNodeType type = (NoiseNodeType)opCode;
                if (!IsExecutable(type))
                    throw new ArgumentException($"Bytecode contains unsupported opcode {opCode}.", nameof(bytecode));

                NoiseOpInfo noiseInfo = default;
                if (type.IsNoise())
                    noiseInfo = Read<NoiseOpInfo>(bytecode, bytecodeLength, ref offset);

                int inputCount = type.GetInputCount();
                int outputCount = type.GetOutputCount();
                if (inputCount > MaxInputCount || outputCount > MaxOutputCount)
                {
                    throw new ArgumentException(
                        $"NoiseNodeType {type} requires more registers than the bytecode interpreter supports.",
                        nameof(bytecode));
                }

                for (int i = 0; i < inputCount; i++)
                    inputs[i] = ReadRegisterIndex(bytecode, bytecodeLength, ref offset, info.RegisterCount);
                for (int i = 0; i < outputCount; i++)
                    outputs[i] = ReadRegisterIndex(bytecode, bytecodeLength, ref offset, info.RegisterCount);

                EvaluateInstruction(type, noiseInfo, seed, registerBuffer, batchSize, inputs, outputs);
            }
        }

        static void EvaluateInstruction(
            NoiseNodeType type,
            NoiseOpInfo noiseInfo,
            int evaluationSeed,
            float* registerSpace,
            int batchSize,
            int* inputs,
            int* outputs)
        {
            float* output0 = GetRegister(registerSpace, outputs[0], batchSize);

            switch (type)
            {
                case NoiseNodeType.Add__a_b__sum:
                    EvaluateBinaryOp<AddOp>(
                        GetRegister(registerSpace, inputs[0], batchSize),
                        GetRegister(registerSpace, inputs[1], batchSize),
                        output0, batchSize);
                    break;
                case NoiseNodeType.Min__a_b__min:
                    EvaluateBinaryOp<MinOp>(
                        GetRegister(registerSpace, inputs[0], batchSize),
                        GetRegister(registerSpace, inputs[1], batchSize),
                        output0, batchSize);
                    break;
                case NoiseNodeType.Max__a_b__max:
                    EvaluateBinaryOp<MaxOp>(
                        GetRegister(registerSpace, inputs[0], batchSize),
                        GetRegister(registerSpace, inputs[1], batchSize),
                        output0, batchSize);
                    break;
                case NoiseNodeType.Pow__value_power__result:
                    EvaluateBinaryOp<PowOp>(
                        GetRegister(registerSpace, inputs[0], batchSize),
                        GetRegister(registerSpace, inputs[1], batchSize),
                        output0, batchSize);
                    break;
                case NoiseNodeType.SmoothStep01__value__result:
                    EvaluateUnaryOp<SmoothStep01Op>(
                        GetRegister(registerSpace, inputs[0], batchSize),
                        output0, batchSize);
                    break;
                case NoiseNodeType.Lerp__a_b_t__result:
                    EvaluateTernaryOp<LerpOp>(
                        GetRegister(registerSpace, inputs[0], batchSize),
                        GetRegister(registerSpace, inputs[1], batchSize),
                        GetRegister(registerSpace, inputs[2], batchSize),
                        output0, batchSize);
                    break;
                case NoiseNodeType.Floor__value__result:
                    EvaluateUnaryOp<FloorOp>(
                        GetRegister(registerSpace, inputs[0], batchSize),
                        output0, batchSize);
                    break;
                case NoiseNodeType.Negate__value__negated:
                    EvaluateUnaryOp<NegateOp>(
                        GetRegister(registerSpace, inputs[0], batchSize),
                        output0, batchSize);
                    break;
                case NoiseNodeType.Multiply__a_b__product:
                    EvaluateBinaryOp<MultiplyOp>(
                        GetRegister(registerSpace, inputs[0], batchSize),
                        GetRegister(registerSpace, inputs[1], batchSize),
                        output0, batchSize);
                    break;
                case NoiseNodeType.Inverse__value__inverse:
                    EvaluateUnaryOp<InverseOp>(
                        GetRegister(registerSpace, inputs[0], batchSize),
                        output0, batchSize);
                    break;

                case NoiseNodeType.Perlin2D_noise__x_y__noise:
                    {
                        float* x = GetRegister(registerSpace, inputs[0], batchSize);
                        float* y = GetRegister(registerSpace, inputs[1], batchSize);
#if CORECLR
                        Noise.GradientNoise2D(
                            new Span<float>(x, batchSize),
                            new Span<float>(y, batchSize),
                            new Span<float>(output0, batchSize),
                            CreateNoiseSettings(noiseInfo, evaluationSeed));
#else
                        Noise.GradientNoise2DBurst(x, y, output0, batchSize, CreateNoiseSettings(noiseInfo, evaluationSeed));
#endif
                        break;
                    }
                case NoiseNodeType.Perlin3D_noise__x_y_z__noise:
                    {
                        float* x = GetRegister(registerSpace, inputs[0], batchSize);
                        float* y = GetRegister(registerSpace, inputs[1], batchSize);
                        float* z = GetRegister(registerSpace, inputs[2], batchSize);
#if CORECLR
                        Noise.GradientNoise3D(
                            new Span<float>(x, batchSize),
                            new Span<float>(y, batchSize),
                            new Span<float>(z, batchSize),
                            new Span<float>(output0, batchSize),
                            CreateNoiseSettings(noiseInfo, evaluationSeed));
#else
                        Noise.GradientNoise3DBurst(x, y, z, output0, batchSize, CreateNoiseSettings(noiseInfo, evaluationSeed));
#endif
                        break;
                    }
                case NoiseNodeType.Cellular2_noise__x_y__center_edge:
                    {
                        float* x = GetRegister(registerSpace, inputs[0], batchSize);
                        float* y = GetRegister(registerSpace, inputs[1], batchSize);
                        float* edgeOutput = GetRegister(registerSpace, outputs[1], batchSize);
#if CORECLR
                        Noise.CellularNoise2D(
                            new Span<float>(x, batchSize),
                            new Span<float>(y, batchSize),
                            new Span<float>(output0, batchSize),
                            new Span<float>(edgeOutput, batchSize),
                            CreateNoiseSettings(noiseInfo, evaluationSeed));
#else
                        Noise.CellularNoise2DBurst(x, y, output0, edgeOutput, batchSize, CreateNoiseSettings(noiseInfo, evaluationSeed));
#endif
                        break;
                    }
                case NoiseNodeType.Cellular3_noise__x_y_z__center_edge:
                    {
                        float* x = GetRegister(registerSpace, inputs[0], batchSize);
                        float* y = GetRegister(registerSpace, inputs[1], batchSize);
                        float* z = GetRegister(registerSpace, inputs[2], batchSize);
                        float* edgeOutput = GetRegister(registerSpace, outputs[1], batchSize);
#if CORECLR
                        Noise.CellularNoise3D(
                            new Span<float>(x, batchSize),
                            new Span<float>(y, batchSize),
                            new Span<float>(z, batchSize),
                            new Span<float>(output0, batchSize),
                            new Span<float>(edgeOutput, batchSize),
                            CreateNoiseSettings(noiseInfo, evaluationSeed));
#else
                        Noise.CellularNoise3DBurst(x, y, z, output0, edgeOutput, batchSize, CreateNoiseSettings(noiseInfo, evaluationSeed));
#endif
                        break;
                    }
                default:
                    throw new ArgumentException($"Unsupported executable NoiseNodeType {type}.");
            }
        }

        interface IUnaryVectorOp
        {
            Float Apply(Float a);
        }

        interface IBinaryVectorOp
        {
            Float Apply(Float a, Float b);
        }

        interface ITernaryVectorOp
        {
            Float Apply(Float a, Float b, Float c);
        }

        readonly struct AddOp : IBinaryVectorOp
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Float Apply(Float a, Float b) => a + b;
        }

        readonly struct MinOp : IBinaryVectorOp
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Float Apply(Float a, Float b) => Util.Min(a, b);
        }

        readonly struct MaxOp : IBinaryVectorOp
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Float Apply(Float a, Float b) => Util.Max(a, b);
        }

        readonly struct MultiplyOp : IBinaryVectorOp
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Float Apply(Float a, Float b) => a * b;
        }

        readonly struct PowOp : IBinaryVectorOp
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Float Apply(Float a, Float b)
            {
#if CORECLR
                // There is no vectorized MathF.Pow, so each lane is computed individually.
                Float result = default;
                for (int lane = 0; lane < Float.Count; lane++)
                    result = result.WithElement(lane, MathF.Pow(a.GetElement(lane), b.GetElement(lane)));
                return result;
#else
                return Unity.Mathematics.math.pow(a, b);
#endif
            }
        }

        readonly struct NegateOp : IUnaryVectorOp
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Float Apply(Float a) => -a;
        }

        readonly struct InverseOp : IUnaryVectorOp
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Float Apply(Float a) => Util.Create(1f) / a;
        }

        readonly struct FloorOp : IUnaryVectorOp
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Float Apply(Float a) => Util.Floor(a);
        }

        readonly struct SmoothStep01Op : IUnaryVectorOp
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Float Apply(Float a)
            {
                Float clamped = Util.Min(Util.Max(a, Util.Create(0f)), Util.Create(1f));
                return clamped * clamped * (Util.Create(3f) - Util.Create(2f) * clamped);
            }
        }

        readonly struct LerpOp : ITernaryVectorOp
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Float Apply(Float a, Float b, Float t) => a + (b - a) * t;
        }

        // In CoreCLR, each op processes registers in Float.Count-wide chunks using explicit SIMD,
        // with a scalar remainder loop for the tail (mirrors Noise.EvaluateNoiseFunction).
        // In Unity, each op is applied per element in a plain loop, which Burst auto-vectorizes.
        static void EvaluateUnaryOp<TOp>(float* a, float* output, int batchSize) where TOp : struct, IUnaryVectorOp
        {
            TOp op = default;
#if CORECLR
            int fullVectorLength = batchSize - batchSize % Float.Count;
            for (int i = 0; i < fullVectorLength; i += Float.Count)
                op.Apply(Util.LoadUnsafe(in a[i])).StoreUnsafe(ref output[i]);

            int remainder = batchSize - fullVectorLength;
            if (remainder > 0)
            {
                Float aVec = default;
                for (int i = 0; i < remainder; i++)
                    aVec = aVec.WithElement(i, a[fullVectorLength + i]);

                Float result = op.Apply(aVec);
                for (int i = 0; i < remainder; i++)
                    output[fullVectorLength + i] = result.GetElement(i);
            }
#else
            for (int i = 0; i < batchSize; i++)
                output[i] = op.Apply(a[i]);
#endif
        }

        static void EvaluateBinaryOp<TOp>(float* a, float* b, float* output, int batchSize) where TOp : struct, IBinaryVectorOp
        {
            TOp op = default;
#if CORECLR
            int fullVectorLength = batchSize - batchSize % Float.Count;
            for (int i = 0; i < fullVectorLength; i += Float.Count)
                op.Apply(Util.LoadUnsafe(in a[i]), Util.LoadUnsafe(in b[i])).StoreUnsafe(ref output[i]);

            int remainder = batchSize - fullVectorLength;
            if (remainder > 0)
            {
                Float aVec = default, bVec = default;
                for (int i = 0; i < remainder; i++)
                {
                    aVec = aVec.WithElement(i, a[fullVectorLength + i]);
                    bVec = bVec.WithElement(i, b[fullVectorLength + i]);
                }

                Float result = op.Apply(aVec, bVec);
                for (int i = 0; i < remainder; i++)
                    output[fullVectorLength + i] = result.GetElement(i);
            }
#else
            for (int i = 0; i < batchSize; i++)
                output[i] = op.Apply(a[i], b[i]);
#endif
        }

        static void EvaluateTernaryOp<TOp>(float* a, float* b, float* c, float* output, int batchSize) where TOp : struct, ITernaryVectorOp
        {
            TOp op = default;
#if CORECLR
            int fullVectorLength = batchSize - batchSize % Float.Count;
            for (int i = 0; i < fullVectorLength; i += Float.Count)
                op.Apply(Util.LoadUnsafe(in a[i]), Util.LoadUnsafe(in b[i]), Util.LoadUnsafe(in c[i])).StoreUnsafe(ref output[i]);

            int remainder = batchSize - fullVectorLength;
            if (remainder > 0)
            {
                Float aVec = default, bVec = default, cVec = default;
                for (int i = 0; i < remainder; i++)
                {
                    aVec = aVec.WithElement(i, a[fullVectorLength + i]);
                    bVec = bVec.WithElement(i, b[fullVectorLength + i]);
                    cVec = cVec.WithElement(i, c[fullVectorLength + i]);
                }

                Float result = op.Apply(aVec, bVec, cVec);
                for (int i = 0; i < remainder; i++)
                    output[fullVectorLength + i] = result.GetElement(i);
            }
#else
            for (int i = 0; i < batchSize; i++)
                output[i] = op.Apply(a[i], b[i], c[i]);
#endif
        }

        static NoiseSettings CreateNoiseSettings(NoiseOpInfo info, int evaluationSeed) => new(
            xFreq: info.XFrequency,
            yFreq: info.YFrequency,
            zFreq: info.ZFrequency,
            amplitude: 1f,
            amplitude2: 1f,
            seed: evaluationSeed + info.Seed,
            accumulate: info.Accumulate);

        static float* GetRegister(float* registerSpace, int register, int batchSize) =>
            registerSpace + checked(register * batchSize);

        static void Fill(float* register, int batchSize, float value)
        {
            for (int i = 0; i < batchSize; i++)
                register[i] = value;
        }

        static void Copy(float* source, float* destination, int batchSize)
        {
            for (int i = 0; i < batchSize; i++)
                destination[i] = source[i];
        }

        static int ReadRegisterIndex(byte* bytecode, int bytecodeLength, ref int offset, int registerCount)
        {
            int register = Read<int>(bytecode, bytecodeLength, ref offset);
            if ((uint)register >= (uint)registerCount)
                throw new ArgumentException($"Bytecode references invalid register {register}.", nameof(bytecode));
            return register;
        }

        static T Read<T>(byte* bytecode, int bytecodeLength, ref int offset) where T : unmanaged
        {
            int size = Unsafe.SizeOf<T>();
            if (offset < 0 || bytecodeLength - offset < size)
                throw new ArgumentException("Bytecode ended in the middle of an instruction.", nameof(bytecode));

            T value = Unsafe.ReadUnaligned<T>(bytecode + offset);
            offset += size;
            return value;
        }

        static void ValidateByteCodeInfo(ByteCodeInfo info)
        {
            if (info.InputCount < 0 ||
                info.OutputCount < 0 ||
                info.RegisterCount < 0 ||
                info.ConstantCount < 0)
            {
                throw new ArgumentException("Bytecode contains an invalid header.");
            }

            long fixedRegisterCountLong = (long)info.InputCount + info.ConstantCount;
            if (fixedRegisterCountLong > int.MaxValue)
                throw new ArgumentException("Bytecode contains an invalid header.");
            int fixedRegisterCount = (int)fixedRegisterCountLong;

            if (info.RegisterCount < Math.Max(fixedRegisterCount, info.OutputCount))
                throw new ArgumentException("Bytecode contains an invalid header.");
        }

        internal static bool IsExecutable(NoiseNodeType type) =>
            type != NoiseNodeType.Null && NoiseNodeTypeExtensions.IsDefined(type);

    }
}
