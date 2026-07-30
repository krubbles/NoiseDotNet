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
        /// Evaluates one scalar graph over two-dimensional coordinates.
        /// </summary>
        public static void Evaluate2D(
            NoiseScalar channel1,
            ReadOnlySpan<float> xCoords,
            ReadOnlySpan<float> yCoords,
            Span<float> output1,
            int seed = 0)
        {
            float[] registers = Evaluate2D(
                new NoiseScalar[] { channel1 },
                xCoords,
                yCoords,
                seed);
            CopyEvaluationOutput(registers, xCoords.Length, 0, output1);
        }

        /// <summary>
        /// Evaluates two scalar graphs over two-dimensional coordinates.
        /// </summary>
        public static void Evaluate2D(
            NoiseScalar channel1,
            NoiseScalar channel2,
            ReadOnlySpan<float> xCoords,
            ReadOnlySpan<float> yCoords,
            Span<float> output1,
            Span<float> output2,
            int seed = 0)
        {
            float[] registers = Evaluate2D(
                new NoiseScalar[] { channel1, channel2 },
                xCoords,
                yCoords,
                seed);
            int batchSize = xCoords.Length;
            CopyEvaluationOutput(registers, batchSize, 0, output1);
            CopyEvaluationOutput(registers, batchSize, 1, output2);
        }

        /// <summary>
        /// Evaluates three scalar graphs over two-dimensional coordinates.
        /// </summary>
        public static void Evaluate2D(
            NoiseScalar channel1,
            NoiseScalar channel2,
            NoiseScalar channel3,
            ReadOnlySpan<float> xCoords,
            ReadOnlySpan<float> yCoords,
            Span<float> output1,
            Span<float> output2,
            Span<float> output3,
            int seed = 0)
        {
            float[] registers = Evaluate2D(
                new NoiseScalar[] { channel1, channel2, channel3 },
                xCoords,
                yCoords,
                seed);
            int batchSize = xCoords.Length;
            CopyEvaluationOutput(registers, batchSize, 0, output1);
            CopyEvaluationOutput(registers, batchSize, 1, output2);
            CopyEvaluationOutput(registers, batchSize, 2, output3);
        }

        /// <summary>
        /// Evaluates four scalar graphs over two-dimensional coordinates.
        /// </summary>
        public static void Evaluate2D(
            NoiseScalar channel1,
            NoiseScalar channel2,
            NoiseScalar channel3,
            NoiseScalar channel4,
            ReadOnlySpan<float> xCoords,
            ReadOnlySpan<float> yCoords,
            Span<float> output1,
            Span<float> output2,
            Span<float> output3,
            Span<float> output4,
            int seed = 0)
        {
            float[] registers = Evaluate2D(
                new NoiseScalar[] { channel1, channel2, channel3, channel4 },
                xCoords,
                yCoords,
                seed);
            int batchSize = xCoords.Length;
            CopyEvaluationOutput(registers, batchSize, 0, output1);
            CopyEvaluationOutput(registers, batchSize, 1, output2);
            CopyEvaluationOutput(registers, batchSize, 2, output3);
            CopyEvaluationOutput(registers, batchSize, 3, output4);
        }

        /// <summary>
        /// Evaluates one scalar graph over three-dimensional coordinates.
        /// </summary>
        public static void Evaluate3D(
            NoiseScalar channel1,
            ReadOnlySpan<float> xCoords,
            ReadOnlySpan<float> yCoords,
            ReadOnlySpan<float> zCoords,
            Span<float> output1,
            int seed = 0)
        {
            float[] registers = Evaluate3D(
                new NoiseScalar[] { channel1 },
                xCoords,
                yCoords,
                zCoords,
                seed);
            CopyEvaluationOutput(registers, xCoords.Length, 0, output1);
        }

        /// <summary>
        /// Evaluates two scalar graphs over three-dimensional coordinates.
        /// </summary>
        public static void Evaluate3D(
            NoiseScalar channel1,
            NoiseScalar channel2,
            ReadOnlySpan<float> xCoords,
            ReadOnlySpan<float> yCoords,
            ReadOnlySpan<float> zCoords,
            Span<float> output1,
            Span<float> output2,
            int seed = 0)
        {
            float[] registers = Evaluate3D(
                new NoiseScalar[] { channel1, channel2 },
                xCoords,
                yCoords,
                zCoords,
                seed);
            int batchSize = xCoords.Length;
            CopyEvaluationOutput(registers, batchSize, 0, output1);
            CopyEvaluationOutput(registers, batchSize, 1, output2);
        }

        /// <summary>
        /// Evaluates three scalar graphs over three-dimensional coordinates.
        /// </summary>
        public static void Evaluate3D(
            NoiseScalar channel1,
            NoiseScalar channel2,
            NoiseScalar channel3,
            ReadOnlySpan<float> xCoords,
            ReadOnlySpan<float> yCoords,
            ReadOnlySpan<float> zCoords,
            Span<float> output1,
            Span<float> output2,
            Span<float> output3,
            int seed = 0)
        {
            float[] registers = Evaluate3D(
                new NoiseScalar[] { channel1, channel2, channel3 },
                xCoords,
                yCoords,
                zCoords,
                seed);
            int batchSize = xCoords.Length;
            CopyEvaluationOutput(registers, batchSize, 0, output1);
            CopyEvaluationOutput(registers, batchSize, 1, output2);
            CopyEvaluationOutput(registers, batchSize, 2, output3);
        }

        /// <summary>
        /// Evaluates four scalar graphs over three-dimensional coordinates.
        /// </summary>
        public static void Evaluate3D(
            NoiseScalar channel1,
            NoiseScalar channel2,
            NoiseScalar channel3,
            NoiseScalar channel4,
            ReadOnlySpan<float> xCoords,
            ReadOnlySpan<float> yCoords,
            ReadOnlySpan<float> zCoords,
            Span<float> output1,
            Span<float> output2,
            Span<float> output3,
            Span<float> output4,
            int seed = 0)
        {
            float[] registers = Evaluate3D(
                new NoiseScalar[] { channel1, channel2, channel3, channel4 },
                xCoords,
                yCoords,
                zCoords,
                seed);
            int batchSize = xCoords.Length;
            CopyEvaluationOutput(registers, batchSize, 0, output1);
            CopyEvaluationOutput(registers, batchSize, 1, output2);
            CopyEvaluationOutput(registers, batchSize, 2, output3);
            CopyEvaluationOutput(registers, batchSize, 3, output4);
        }

        static float[] Evaluate2D(
            NoiseScalar[] channels,
            ReadOnlySpan<float> xCoords,
            ReadOnlySpan<float> yCoords,
            int seed)
        {
            ValidateEvaluationBufferLength(yCoords.Length, xCoords.Length, nameof(yCoords));

            NoiseGraphByteCode compiled = NoiseGraphByteCodeCompiler.Compile(channels);
            ByteCodeInfo info =
                System.Runtime.InteropServices.MemoryMarshal.Read<ByteCodeInfo>(compiled.ByteCode);
            if (info.InputCount > 2)
            {
                throw new ArgumentException(
                    $"Cannot evaluate a graph with {info.InputCount} coordinate inputs as a 2D graph.",
                    nameof(channels));
            }

            int batchSize = xCoords.Length;
            float[] registers = new float[checked(info.RegisterCount * batchSize)];
            if (info.InputCount >= 1)
                xCoords.CopyTo(registers.AsSpan(0, batchSize));
            if (info.InputCount >= 2)
                yCoords.CopyTo(registers.AsSpan(batchSize, batchSize));
            NoiseGraphByteCodeEval.EvaluateByteCode(compiled.ByteCode, seed, registers, batchSize);
            return registers;
        }

        static float[] Evaluate3D(
            NoiseScalar[] channels,
            ReadOnlySpan<float> xCoords,
            ReadOnlySpan<float> yCoords,
            ReadOnlySpan<float> zCoords,
            int seed)
        {
            ValidateEvaluationBufferLength(yCoords.Length, xCoords.Length, nameof(yCoords));
            ValidateEvaluationBufferLength(zCoords.Length, xCoords.Length, nameof(zCoords));

            NoiseGraphByteCode compiled = NoiseGraphByteCodeCompiler.Compile(channels);
            ByteCodeInfo info =
                System.Runtime.InteropServices.MemoryMarshal.Read<ByteCodeInfo>(compiled.ByteCode);

            int batchSize = xCoords.Length;
            float[] registers = new float[checked(info.RegisterCount * batchSize)];
            if (info.InputCount >= 1)
                xCoords.CopyTo(registers.AsSpan(0, batchSize));
            if (info.InputCount >= 2)
                yCoords.CopyTo(registers.AsSpan(batchSize, batchSize));
            if (info.InputCount >= 3)
                zCoords.CopyTo(registers.AsSpan(batchSize * 2, batchSize));
            NoiseGraphByteCodeEval.EvaluateByteCode(compiled.ByteCode, seed, registers, batchSize);
            return registers;
        }

        static void CopyEvaluationOutput(
            float[] registers,
            int batchSize,
            int outputIndex,
            Span<float> output)
        {
            ValidateEvaluationBufferLength(output.Length, batchSize, nameof(output));
            registers.AsSpan(outputIndex * batchSize, batchSize).CopyTo(output);
        }

        static void ValidateEvaluationBufferLength(int actual, int expected, string paramName)
        {
            if (actual != expected)
            {
                throw new ArgumentException(
                    $"Expected buffer length {actual} to equal coordinate buffer length {expected}.",
                    paramName);
            }
        }


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



        static void EvaluateInstruction(
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
                    {
                        Span<float> a = GetRegister(registerSpace, inputs[0], batchSize);
                        Span<float> b = GetRegister(registerSpace, inputs[1], batchSize);
                        for (int i = 0; i < batchSize; i++)
                            output0[i] = a[i] + b[i];
                        break;
                    }
                case NoiseNodeType.Min__a_b__min:
                    {
                        Span<float> a = GetRegister(registerSpace, inputs[0], batchSize);
                        Span<float> b = GetRegister(registerSpace, inputs[1], batchSize);
                        for (int i = 0; i < batchSize; i++)
                            output0[i] = MathF.Min(a[i], b[i]);
                        break;
                    }
                case NoiseNodeType.Max__a_b__max:
                    {
                        Span<float> a = GetRegister(registerSpace, inputs[0], batchSize);
                        Span<float> b = GetRegister(registerSpace, inputs[1], batchSize);
                        for (int i = 0; i < batchSize; i++)
                            output0[i] = MathF.Max(a[i], b[i]);
                        break;
                    }
                case NoiseNodeType.Pow__value_power__result:
                    {
                        Span<float> value = GetRegister(registerSpace, inputs[0], batchSize);
                        Span<float> power = GetRegister(registerSpace, inputs[1], batchSize);
                        for (int i = 0; i < batchSize; i++)
                            output0[i] = MathF.Pow(value[i], power[i]);
                        break;
                    }
                case NoiseNodeType.SmoothStep01__value__result:
                    {
                        Span<float> value = GetRegister(registerSpace, inputs[0], batchSize);
                        for (int i = 0; i < batchSize; i++)
                        {
                            float clamped = Math.Clamp(value[i], 0f, 1f);
                            output0[i] = clamped * clamped * (3f - 2f * clamped);
                        }
                        break;
                    }
                case NoiseNodeType.Lerp__a_b_t__result:
                    {
                        Span<float> a = GetRegister(registerSpace, inputs[0], batchSize);
                        Span<float> b = GetRegister(registerSpace, inputs[1], batchSize);
                        Span<float> t = GetRegister(registerSpace, inputs[2], batchSize);
                        for (int i = 0; i < batchSize; i++)
                            output0[i] = a[i] + (b[i] - a[i]) * t[i];
                        break;
                    }
                case NoiseNodeType.Floor__value__result:
                    {
                        Span<float> value = GetRegister(registerSpace, inputs[0], batchSize);
                        for (int i = 0; i < batchSize; i++)
                            output0[i] = MathF.Floor(value[i]);
                        break;
                    }
                case NoiseNodeType.Negate__value__negated:
                    {
                        Span<float> value = GetRegister(registerSpace, inputs[0], batchSize);
                        for (int i = 0; i < batchSize; i++)
                            output0[i] = -value[i];
                        break;
                    }
                case NoiseNodeType.Multiply__a_b__product:
                    {
                        Span<float> a = GetRegister(registerSpace, inputs[0], batchSize);
                        Span<float> b = GetRegister(registerSpace, inputs[1], batchSize);
                        for (int i = 0; i < batchSize; i++)
                            output0[i] = a[i] * b[i];
                        break;
                    }
                case NoiseNodeType.Inverse__value__inverse:
                    {
                        Span<float> value = GetRegister(registerSpace, inputs[0], batchSize);
                        for (int i = 0; i < batchSize; i++)
                            output0[i] = 1f / value[i];
                        break;
                    }
                case NoiseNodeType.Perlin2D_noise__x_y__noise:
                    Noise.GradientNoise2D(
                        GetRegister(registerSpace, inputs[0], batchSize),
                        GetRegister(registerSpace, inputs[1], batchSize),
                        output0,
                        CreateNoiseSettings(noiseInfo, evaluationSeed));
                    break;
                case NoiseNodeType.Perlin3D_noise__x_y_z__noise:
                    Noise.GradientNoise3D(
                        GetRegister(registerSpace, inputs[0], batchSize),
                        GetRegister(registerSpace, inputs[1], batchSize),
                        GetRegister(registerSpace, inputs[2], batchSize),
                        output0,
                        CreateNoiseSettings(noiseInfo, evaluationSeed));
                    break;
                case NoiseNodeType.Cellular2_noise__x_y__center_edge:
                    Noise.CellularNoise2D(
                        GetRegister(registerSpace, inputs[0], batchSize),
                        GetRegister(registerSpace, inputs[1], batchSize),
                        output0,
                        GetRegister(registerSpace, outputs[1], batchSize),
                        CreateNoiseSettings(noiseInfo, evaluationSeed));
                    break;
                case NoiseNodeType.Cellular3_noise__x_y_z__center_edge:
                    Noise.CellularNoise3D(
                        GetRegister(registerSpace, inputs[0], batchSize),
                        GetRegister(registerSpace, inputs[1], batchSize),
                        GetRegister(registerSpace, inputs[2], batchSize),
                        output0,
                        GetRegister(registerSpace, outputs[1], batchSize),
                        CreateNoiseSettings(noiseInfo, evaluationSeed));
                    break;
                default:
                    throw new ArgumentException($"Unsupported executable NoiseNodeType {type}.");
            }
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
