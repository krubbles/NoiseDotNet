namespace NoiseDotNet
{
    /// <summary>
    /// Evaluates noise graphs through compiled bytecode.
    /// </summary>
    public static partial class NoiseGraph
    {
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
            Evaluate2D(new NoiseScalar[] { channel1 }, xCoords, yCoords, seed, output1);
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
            Evaluate2D(new NoiseScalar[] { channel1, channel2 }, xCoords, yCoords, seed, output1, output2);
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
            Evaluate2D(new NoiseScalar[] { channel1, channel2, channel3 }, xCoords, yCoords, seed, output1, output2, output3);
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
            Evaluate2D(new NoiseScalar[] { channel1, channel2, channel3, channel4 }, xCoords, yCoords, seed, output1, output2, output3, output4);
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
            Evaluate3D(new NoiseScalar[] { channel1 }, xCoords, yCoords, zCoords, seed, output1);
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
            Evaluate3D(new NoiseScalar[] { channel1, channel2 }, xCoords, yCoords, zCoords, seed, output1, output2);
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
            Evaluate3D(new NoiseScalar[] { channel1, channel2, channel3 }, xCoords, yCoords, zCoords, seed, output1, output2, output3);
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
            Evaluate3D(new NoiseScalar[] { channel1, channel2, channel3, channel4 }, xCoords, yCoords, zCoords, seed, output1, output2, output3, output4);
        }

        const int EvaluationBlockSize = 1024;

        static void Evaluate2D(NoiseScalar[] channels, ReadOnlySpan<float> xCoords, ReadOnlySpan<float> yCoords, int seed, Span<float> output1, Span<float> output2 = default, Span<float> output3 = default, Span<float> output4 = default)
        {
            ValidateBufferLengths(xCoords.Length, yCoords.Length, output1, output2, output3, output4);
            NoiseGraphByteCode compiled = NoiseGraphByteCodeCompiler.Compile(channels);
            ByteCodeInfo info = System.Runtime.InteropServices.MemoryMarshal.Read<ByteCodeInfo>(compiled.ByteCode);
            if (info.InputCount > 2)
                throw new ArgumentException($"Cannot evaluate a graph with {info.InputCount} coordinate inputs as a 2D graph.", nameof(channels));

            EvaluateInBlocks(compiled, seed, xCoords, yCoords, default, info.InputCount, output1, output2, output3, output4);
        }

        static void Evaluate3D(NoiseScalar[] channels, ReadOnlySpan<float> xCoords, ReadOnlySpan<float> yCoords, ReadOnlySpan<float> zCoords, int seed, Span<float> output1, Span<float> output2 = default, Span<float> output3 = default, Span<float> output4 = default)
        {
            ValidateBufferLengths(xCoords.Length, yCoords.Length, zCoords.Length, output1, output2, output3, output4);
            NoiseGraphByteCode compiled = NoiseGraphByteCodeCompiler.Compile(channels);
            ByteCodeInfo info = System.Runtime.InteropServices.MemoryMarshal.Read<ByteCodeInfo>(compiled.ByteCode);
            EvaluateInBlocks(compiled, seed, xCoords, yCoords, zCoords, info.InputCount, output1, output2, output3, output4);
        }

        static void EvaluateInBlocks(NoiseGraphByteCode compiled, int seed, ReadOnlySpan<float> xCoords, ReadOnlySpan<float> yCoords, ReadOnlySpan<float> zCoords, int inputCount, Span<float> output1, Span<float> output2, Span<float> output3, Span<float> output4)
        {
            ByteCodeInfo info = System.Runtime.InteropServices.MemoryMarshal.Read<ByteCodeInfo>(compiled.ByteCode);
            for (int offset = 0; offset < xCoords.Length; offset += EvaluationBlockSize)
            {
                int count = Math.Min(EvaluationBlockSize, xCoords.Length - offset);
                float[] registers = new float[checked(info.RegisterCount * count)];
                if (inputCount >= 1)
                    xCoords.Slice(offset, count).CopyTo(registers.AsSpan(0, count));
                if (inputCount >= 2)
                    yCoords.Slice(offset, count).CopyTo(registers.AsSpan(count, count));
                if (inputCount >= 3)
                    zCoords.Slice(offset, count).CopyTo(registers.AsSpan(count * 2, count));

                NoiseGraphByteCodeEval.EvaluateByteCode(compiled.ByteCode, seed, registers, count);
                registers.AsSpan(0, count).CopyTo(output1.Slice(offset, count));
                if (!output2.IsEmpty)
                    registers.AsSpan(count, count).CopyTo(output2.Slice(offset, count));
                if (!output3.IsEmpty)
                    registers.AsSpan(count * 2, count).CopyTo(output3.Slice(offset, count));
                if (!output4.IsEmpty)
                    registers.AsSpan(count * 3, count).CopyTo(output4.Slice(offset, count));
            }
        }

        static void ValidateBufferLengths(int expectedLength, int coordinateLength, Span<float> output1, Span<float> output2, Span<float> output3, Span<float> output4)
        {
            if (coordinateLength != expectedLength)
                throw new ArgumentException($"Expected buffer length {coordinateLength} to equal coordinate buffer length {expectedLength}.");
            ValidateOutputLength(expectedLength, output1);
            if (!output2.IsEmpty) ValidateOutputLength(expectedLength, output2);
            if (!output3.IsEmpty) ValidateOutputLength(expectedLength, output3);
            if (!output4.IsEmpty) ValidateOutputLength(expectedLength, output4);
        }

        static void ValidateBufferLengths(int expectedLength, int yLength, int zLength, Span<float> output1, Span<float> output2, Span<float> output3, Span<float> output4)
        {
            ValidateBufferLengths(expectedLength, yLength, output1, output2, output3, output4);
            if (zLength != expectedLength)
                throw new ArgumentException($"Expected buffer length {zLength} to equal coordinate buffer length {expectedLength}.");
        }

        static void ValidateOutputLength(int expectedLength, Span<float> output)
        {
            if (output.Length != expectedLength)
                throw new ArgumentException($"Expected buffer length {output.Length} to equal coordinate buffer length {expectedLength}.");
        }

    }
}
