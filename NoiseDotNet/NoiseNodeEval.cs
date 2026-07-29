using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace NoiseDotNet
{
    /// <summary>
    /// Compilation and evaluation utilities for NoiseNode bytecode.
    /// </summary>
    public static class NoiseNodeByteCode
    {
        const byte CopyOpCode = byte.MaxValue;

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
        public static void Evaluate(ReadOnlySpan<byte> bytecode, int seed, Span<float> registerSpace, int batchSize)
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

        /// <summary>
        /// Compiles the graphs needed by the ordered output channels into reusable bytecode.
        /// </summary>
        /// <param name="outputs">Output channels, in the order they should appear in the output registers.</param>
        public static CompiledNoiseNode Compile(params NoiseScalar[] outputs)
        {
            ValidateOutputs(outputs);
            return new NoiseNodeCompiler(outputs).Compile();
        }

        /// <summary>
        /// Returns the per-node seeds that would be assigned when compiling the graphs needed by
        /// the ordered output channels.
        /// </summary>
        /// <param name="outputs">Output channels whose graphs should be inspected.</param>
        public static Dictionary<NoiseNode, int> GetNoiseSeeds(params NoiseScalar[] outputs)
        {
            ValidateOutputs(outputs);
            return new NoiseNodeCompiler(outputs).GetNoiseSeeds();
        }

        static void ValidateOutputs(NoiseScalar[] outputs)
        {
            ArgumentNullException.ThrowIfNull(outputs);
            if (outputs.Length == 0)
                throw new ArgumentException("At least one output channel must be provided.", nameof(outputs));
        }

        /// <summary>
        /// Returns a human-readable disassembly of compiled NoiseNode bytecode.
        /// </summary>
        public static string ToString(ReadOnlySpan<byte> bytecode)
        {
            int offset = 0;
            ByteCodeInfo info = Read<ByteCodeInfo>(bytecode, ref offset);
            ValidateByteCodeInfo(info);

            StringBuilder result = new();
            result.Append("ByteCodeInfo { Inputs = ")
                .Append(info.InputCount)
                .Append(", Outputs = ")
                .Append(info.OutputCount)
                .Append(", Registers = ")
                .Append(info.RegisterCount)
                .Append(", Constants = ")
                .Append(info.ConstantCount)
                .AppendLine(" }");

            if (info.ConstantCount > 0)
            {
                result.AppendLine("Constants:");
                for (int constantIndex = 0; constantIndex < info.ConstantCount; constantIndex++)
                {
                    float value = Read<float>(bytecode, ref offset);
                    result.Append("  r")
                        .Append(info.InputCount + constantIndex)
                        .Append(" = ")
                        .AppendLine(value.ToString("R", CultureInfo.InvariantCulture));
                }
            }

            result.AppendLine("Instructions:");
            int instructionIndex = 0;
            while (offset < bytecode.Length)
            {
                byte opCode = Read<byte>(bytecode, ref offset);
                result.Append("  ").Append(instructionIndex++).Append(": ");

                if (opCode == CopyOpCode)
                {
                    int source = ReadRegisterIndex(bytecode, ref offset, info.RegisterCount);
                    int destination = ReadRegisterIndex(bytecode, ref offset, info.RegisterCount);
                    result.Append("Copy r").Append(source).Append(" -> r").AppendLine(destination.ToString());
                    continue;
                }

                NoiseNodeType type = (NoiseNodeType)opCode;
                if (!IsExecutable(type))
                    throw new ArgumentException($"Bytecode contains unsupported opcode {opCode}.", nameof(bytecode));

                result.Append(type);
                if (type.IsNoise())
                {
                    NoiseOpInfo noiseInfo = Read<NoiseOpInfo>(bytecode, ref offset);
                    result.Append(" [frequency = (")
                        .Append(noiseInfo.XFrequency.ToString("R", CultureInfo.InvariantCulture))
                        .Append(", ")
                        .Append(noiseInfo.YFrequency.ToString("R", CultureInfo.InvariantCulture))
                        .Append(", ")
                        .Append(noiseInfo.ZFrequency.ToString("R", CultureInfo.InvariantCulture))
                        .Append("), seed = ")
                        .Append(noiseInfo.Seed);
                    if (noiseInfo.Accumulate)
                        result.Append(", accumulate");
                    result.Append(']');
                }

                int inputCount = type.GetInputCount();
                int outputCount = type.GetOutputCount();
                result.Append(" (");
                for (int i = 0; i < inputCount; i++)
                {
                    if (i > 0)
                        result.Append(", ");
                    result.Append('r').Append(ReadRegisterIndex(bytecode, ref offset, info.RegisterCount));
                }
                result.Append(") -> (");
                for (int i = 0; i < outputCount; i++)
                {
                    if (i > 0)
                        result.Append(", ");
                    result.Append('r').Append(ReadRegisterIndex(bytecode, ref offset, info.RegisterCount));
                }
                result.AppendLine(")");
            }

            return result.ToString().TrimEnd();
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
            seed: evaluationSeed ^ info.Seed,
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

        internal static void Append<T>(List<byte> bytecode, T value) where T : unmanaged
        {
            ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(
                MemoryMarshal.CreateReadOnlySpan(ref value, 1));
            for (int i = 0; i < bytes.Length; i++)
                bytecode.Add(bytes[i]);
        }

        internal static void AppendCopy(List<byte> bytecode, int source, int destination)
        {
            Append(bytecode, CopyOpCode);
            Append(bytecode, source);
            Append(bytecode, destination);
        }
    }

    /// <summary>
    /// Information about a compiled NoiseNode. Stored at the beginning of the compiled bytecode.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ByteCodeInfo
    {
        public int InputCount;
        public int OutputCount;
        public int RegisterCount;
        public int ConstantCount;
    }

    /// <summary>
    /// Extra information for a noise-function instruction.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NoiseOpInfo
    {
        /// <summary>
        /// If true, all outputs of the noise function are accumulated instead of overwritten.
        /// </summary>
        public bool Accumulate;
        public float XFrequency;
        public float YFrequency;
        public float ZFrequency;
        public int Seed;
    }

    /// <summary>
    /// A NoiseNode graph compiled into evaluatable bytecode.
    /// </summary>
    public readonly struct CompiledNoiseNode
    {
        /// <summary>
        /// The raw bytecode. Its layout is:
        /// [ByteCodeInfo][constants][[opcode][optional NoiseOpInfo][input registers][output registers]]...
        /// Inputs occupy the first registers, constants the following registers, and final outputs the first registers.
        /// </summary>
        public readonly byte[] ByteCode;

        internal CompiledNoiseNode(byte[] byteCode)
        {
            ByteCode = byteCode;
        }

        /// <summary>
        /// Returns a human-readable disassembly of this compiled graph.
        /// </summary>
        public override string ToString() =>
            ByteCode is null ? "Uninitialized CompiledNoiseNode" : NoiseNodeByteCode.ToString(ByteCode);
    }
}
