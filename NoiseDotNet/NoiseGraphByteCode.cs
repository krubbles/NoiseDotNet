using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace NoiseDotNet
{
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
    public readonly struct NoiseGraphByteCode
    {
        /// <summary>
        /// The raw bytecode. Its layout is:
        /// [ByteCodeInfo][constants][[opcode][optional NoiseOpInfo][input registers][output registers]]...
        /// Inputs occupy the first registers, constants the following registers, and final outputs the first registers.
        /// </summary>
        public readonly byte[] ByteCode;

        internal NoiseGraphByteCode(byte[] byteCode)
        {
            ByteCode = byteCode;
        }

        /// <summary>
        /// Returns a human-readable disassembly of this compiled graph.
        /// </summary>
        public override string ToString() =>
            ByteCode is null ? "Uninitialized NoiseGraphByteCode" : ToString(ByteCode);

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

                if (opCode == byte.MaxValue)
                {
                    int source = ReadRegisterIndex(bytecode, ref offset, info.RegisterCount);
                    int destination = ReadRegisterIndex(bytecode, ref offset, info.RegisterCount);
                    result.Append("Copy r").Append(source).Append(" -> r").AppendLine(destination.ToString());
                    continue;
                }

                NoiseNodeType type = (NoiseNodeType)opCode;
                if (!NoiseGraphByteCodeEval.IsExecutable(type))
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
    }
}
