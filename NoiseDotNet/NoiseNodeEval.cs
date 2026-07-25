using System.Runtime.InteropServices;

namespace NoiseDotNet
{
    /// <summary>
    /// Compilation and evaluation utilities for the NoiseNode byte-code.
    /// </summary>
    public static class NoiseNodeByteCode
    {
        // AI: write an evaluator for the noise function. also document this function
        public static void Evaluate(ReadOnlySpan<byte> bytecode, int seed, Span<float> registerSpace, int batchSize)
        {

        }

        public static CompiledNoiseNode Compile(NoiseNode node)
        {
            // AI: write a compiler for the noisenode. Try to use accumulate on noise functions when possible,
            // Order stuff such in a way that reduces register count, and avoid unneeded opps.
            // If a noise node is pointed to multiple times in a DAG, its output should only be evaluated once and reused
            // Assign each unique noise node instance a unique deterministic seed (compiling same graph = same seed).
            // It will be commbined with the evaluation seed during evaluation to get the actual used seed for the noise function.
        }

    }

    public static class NoiseNodeCompiler
    {
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
    /// Extra info for a noise function op instruction
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NoiseOpInfo
    {
        /// <summary>
        /// If true, all outputs of noise function op are accumulated instead of written.
        /// </summary>
        public bool Accumulate;
        public float XFrequency;
        public float YFrequency;
        public float ZFrequency;
        public int Seed;
    }


    /// <summary>
    /// A NoiseNode that has been compiled into a evaluatable bytecode.
    /// </summary>
    public struct CompiledNoiseNode
    {
        /// <summary>
        /// The raw bytecode the NoiseNode compiles to.
        /// </summary>
        public readonly byte[] ByteCode;

        // AI: Bytecode format: [ByteCodeInfo][constants][repeat|[opcode][if:isNoise|[NoiseOpInfo]][inputindices][outputindices]]
        // At the beginning, inputs are loaded into the first N registers (where N is InputCount)
        // Constants are loaded into the next M registers (where M is ConstantCount)
        // Then each instruction is evaluated
        // The expected output regsiters are the first OutputCount registers. This means the inputs get overwritten.
        // Bytecode is evaluated in batch over a Span of floats with a batchCount where the span length is RegisterCount * batchCount
        // Each instruction reads from the input indices, performs the operation, and writes to the output indices.
        // Noise functions also have frequency parameters and an optional accumulate flag.
    }
}
