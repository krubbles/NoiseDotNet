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
// or run another Unity Job (which is what the Span-based Noise.* entry points do in Unity).

#if CORECLR
using System.Numerics;
using Float = System.Numerics.Vector<float>;
using Util = System.Numerics.Vector;
#else
using Float = System.Single;
using Util = NoiseDotNet.ScalarUtil;
#endif

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NoiseDotNet
{
    /// <summary>
    /// Compilation and evaluation utilities for NoiseNode bytecode.
    /// </summary>
    public static partial class NoiseGraphByteCodeEval
    {
        public const byte CopyOpCode = byte.MaxValue;


        /// <summary>
        /// Evaluates compiled NoiseNode bytecode over a batch of values.
        /// The first input registers must already be populated in <paramref name="registerSpace"/>.
        /// On return, the first output registers contain the result. Each register occupies one
        /// contiguous <paramref name="batchSize"/>-element section of <paramref name="registerSpace"/>.
        /// </summary>
        /// <param name="bytecode">Bytecode produced by <see cref="Compile"/>.</param>
        /// <param name="seed">Evaluation seed combined with each compiled noise operation's seed.</param>
        /// <param name="registerSpace">Storage for all input, temporary, constant, and output registers.</param>
        /// <param name="batchSize">Number of values evaluated in each register.</param>
        public static void EvaluateByteCode(ReadOnlySpan<byte> bytecode, int seed, Span<float> registerSpace, int batchSize)
        {
            if (batchSize < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(batchSize),
                    batchSize,
                    "Batch size cannot be negative.");
            }

            int offset = 0;
            ByteCodeInfo info = Read<ByteCodeInfo>(bytecode, ref offset);
            ValidateByteCodeInfo(info);

            long requiredRegisterSpaceLong = (long)info.RegisterCount * batchSize;
            if (requiredRegisterSpaceLong > int.MaxValue)
            {
                throw new ArgumentException(
                    $"The compiled graph requires {info.RegisterCount} registers and the requested batch size is {batchSize}, " +
                    $"which would require {requiredRegisterSpaceLong} float values. A Span can contain at most {int.MaxValue} values; " +
                    "use a smaller batch size or compile a graph that requires fewer registers.",
                    nameof(batchSize));
            }
            int requiredRegisterSpace = (int)requiredRegisterSpaceLong;

            if (registerSpace.Length < requiredRegisterSpace)
            {
                throw new ArgumentException(
                    $"Register space contains {registerSpace.Length} values, but the bytecode requires at least {requiredRegisterSpace}.",
                    nameof(registerSpace));
            }

            // init constants
            for (int constantIndex = 0; constantIndex < info.ConstantCount; constantIndex++)
            {
                float value = Read<float>(bytecode, ref offset);
                GetRegister(registerSpace, info.InputCount + constantIndex, batchSize).Fill(value);
            }

            Span<int> inputScratch = stackalloc int[8];
            Span<int> outputScratch = stackalloc int[8];
            while (offset < bytecode.Length)
            {
                byte opCode = Read<byte>(bytecode, ref offset);
                if (opCode == CopyOpCode)
                {
                    int source = ReadRegisterIndex(bytecode, ref offset, info.RegisterCount);
                    int destination = ReadRegisterIndex(bytecode, ref offset, info.RegisterCount);
                    GetRegister(registerSpace, source, batchSize).CopyTo(
                        GetRegister(registerSpace, destination, batchSize));
                    continue;
                }

                NoiseNodeType type = (NoiseNodeType)opCode;
                if (!IsExecutable(type))
                    throw new ArgumentException($"Bytecode contains unsupported opcode {opCode}.", nameof(bytecode));

                NoiseOpInfo noiseInfo = default;
                if (type.IsNoise())
                    noiseInfo = Read<NoiseOpInfo>(bytecode, ref offset);

                int inputCount = type.GetInputCount();
                int outputCount = type.GetOutputCount();
                Span<int> inputs = inputCount <= inputScratch.Length
                    ? inputScratch[..inputCount]
                    : new int[inputCount];
                Span<int> outputs = outputCount <= outputScratch.Length
                    ? outputScratch[..outputCount]
                    : new int[outputCount];

                for (int i = 0; i < inputs.Length; i++)
                    inputs[i] = ReadRegisterIndex(bytecode, ref offset, info.RegisterCount);
                for (int i = 0; i < outputs.Length; i++)
                    outputs[i] = ReadRegisterIndex(bytecode, ref offset, info.RegisterCount);

                EvaluateInstruction(type, noiseInfo, seed, registerSpace, batchSize, inputs, outputs);
            }
        }

        static unsafe void EvaluateInstruction(
            NoiseNodeType type,
            NoiseOpInfo noiseInfo,
            int evaluationSeed,
            Span<float> registerSpace,
            int batchSize,
            ReadOnlySpan<int> inputs,
            ReadOnlySpan<int> outputs)
        {
            Span<float> output0 = GetRegister(registerSpace, outputs[0], batchSize);

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

                // Noise instructions can't share an implementation: in CoreCLR, Noise.GradientNoise2D etc.
                // are already SIMD-vectorized (see Noise.EvaluateNoiseFunction) and take Spans directly.
                // In Unity, this method runs inside a Burst job (BurstNoiseByteCodeJob), and Burst compiled
                // code cannot schedule or run another Unity Job, which is what those Span-based entry points
                // do in Unity. So the pointer-based *Burst functions are used instead.
                case NoiseNodeType.Perlin2D_noise__x_y__noise:
                    {
                        Span<float> x = GetRegister(registerSpace, inputs[0], batchSize);
                        Span<float> y = GetRegister(registerSpace, inputs[1], batchSize);
#if CORECLR
                        Noise.GradientNoise2D(x, y, output0, CreateNoiseSettings(noiseInfo, evaluationSeed));
#else
                        fixed (float* xPtr = x, yPtr = y, outPtr = output0)
                        {
                            Noise.GradientNoise2DBurst(xPtr, yPtr, outPtr, batchSize, CreateNoiseSettings(noiseInfo, evaluationSeed));
                        }
#endif
                        break;
                    }
                case NoiseNodeType.Perlin3D_noise__x_y_z__noise:
                    {
                        Span<float> x = GetRegister(registerSpace, inputs[0], batchSize);
                        Span<float> y = GetRegister(registerSpace, inputs[1], batchSize);
                        Span<float> z = GetRegister(registerSpace, inputs[2], batchSize);
#if CORECLR
                        Noise.GradientNoise3D(x, y, z, output0, CreateNoiseSettings(noiseInfo, evaluationSeed));
#else
                        fixed (float* xPtr = x, yPtr = y, zPtr = z, outPtr = output0)
                        {
                            Noise.GradientNoise3DBurst(xPtr, yPtr, zPtr, outPtr, batchSize, CreateNoiseSettings(noiseInfo, evaluationSeed));
                        }
#endif
                        break;
                    }
                case NoiseNodeType.Cellular2_noise__x_y__center_edge:
                    {
                        Span<float> x = GetRegister(registerSpace, inputs[0], batchSize);
                        Span<float> y = GetRegister(registerSpace, inputs[1], batchSize);
                        Span<float> edgeOutput = GetRegister(registerSpace, outputs[1], batchSize);
#if CORECLR
                        Noise.CellularNoise2D(x, y, output0, edgeOutput, CreateNoiseSettings(noiseInfo, evaluationSeed));
#else
                        fixed (float* xPtr = x, yPtr = y, centerOutPtr = output0, edgeOutPtr = edgeOutput)
                        {
                            Noise.CellularNoise2DBurst(xPtr, yPtr, centerOutPtr, edgeOutPtr, batchSize, CreateNoiseSettings(noiseInfo, evaluationSeed));
                        }
#endif
                        break;
                    }
                case NoiseNodeType.Cellular3_noise__x_y_z__center_edge:
                    {
                        Span<float> x = GetRegister(registerSpace, inputs[0], batchSize);
                        Span<float> y = GetRegister(registerSpace, inputs[1], batchSize);
                        Span<float> z = GetRegister(registerSpace, inputs[2], batchSize);
                        Span<float> edgeOutput = GetRegister(registerSpace, outputs[1], batchSize);
#if CORECLR
                        Noise.CellularNoise3D(x, y, z, output0, edgeOutput, CreateNoiseSettings(noiseInfo, evaluationSeed));
#else
                        fixed (float* xPtr = x, yPtr = y, zPtr = z, centerOutPtr = output0, edgeOutPtr = edgeOutput)
                        {
                            Noise.CellularNoise3DBurst(xPtr, yPtr, zPtr, centerOutPtr, edgeOutPtr, batchSize, CreateNoiseSettings(noiseInfo, evaluationSeed));
                        }
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
                return MathF.Pow(a, b);
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
        static void EvaluateUnaryOp<TOp>(Span<float> a, Span<float> output, int batchSize) where TOp : struct, IUnaryVectorOp
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

        static void EvaluateBinaryOp<TOp>(Span<float> a, Span<float> b, Span<float> output, int batchSize) where TOp : struct, IBinaryVectorOp
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

        static void EvaluateTernaryOp<TOp>(Span<float> a, Span<float> b, Span<float> c, Span<float> output, int batchSize) where TOp : struct, ITernaryVectorOp
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

        static Span<float> GetRegister(Span<float> registerSpace, int register, int batchSize) =>
            registerSpace.Slice(checked(register * batchSize), batchSize);

        static int ReadRegisterIndex(ReadOnlySpan<byte> bytecode, ref int offset, int registerCount)
        {
            int register = Read<int>(bytecode, ref offset);
            if ((uint)register >= (uint)registerCount)
                throw new ArgumentException($"Bytecode references invalid register {register}.", nameof(bytecode));
            return register;
        }

        static T Read<T>(ReadOnlySpan<byte> bytecode, ref int offset) where T : unmanaged
        {
            int size = Unsafe.SizeOf<T>();
            if (offset < 0 || bytecode.Length - offset < size)
                throw new ArgumentException("Bytecode ended in the middle of an instruction.", nameof(bytecode));

            T value = MemoryMarshal.Read<T>(bytecode.Slice(offset, size));
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

            int fixedRegisterCount;
            try
            {
                fixedRegisterCount = checked(info.InputCount + info.ConstantCount);
            }
            catch (OverflowException exception)
            {
                throw new ArgumentException("Bytecode contains an invalid header.", exception);
            }

            if (info.RegisterCount < Math.Max(fixedRegisterCount, info.OutputCount))
                throw new ArgumentException("Bytecode contains an invalid header.");
        }

        internal static bool IsExecutable(NoiseNodeType type) =>
            type != NoiseNodeType.Null && Enum.IsDefined(type);

    }
}
