#if UNITY_2017_1_OR_NEWER

using System;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace NoiseDotNet
{
    /// <summary>
    /// Burst job for evaluating compiled <see cref="NoiseGraphByteCode"/> values.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    public unsafe struct BurstNoiseByteCodeJob : IJob
    {
        [NoAlias]
        [NativeDisableUnsafePtrRestriction]
        public byte* byteCode;

        [NoAlias]
        [NativeDisableUnsafePtrRestriction]
        public float* registerSpace;

        public int byteCodeLength;
        public int registerSpaceLength;
        public int batchSize;
        public int seed;

        public void Execute()
        {
            NoiseGraphByteCodeEval.EvaluateByteCode(
                new ReadOnlySpan<byte>(byteCode, byteCodeLength),
                seed,
                new Span<float>(registerSpace, registerSpaceLength),
                batchSize);
        }

        /// <summary>
        /// Runs a bytecode evaluation synchronously through Burst.
        /// </summary>
        public static void RunByteCodeJob(
            ReadOnlySpan<byte> byteCode,
            int seed,
            Span<float> registerSpace,
            int batchSize)
        {
            fixed (byte* byteCodePtr = byteCode)
            fixed (float* registerSpacePtr = registerSpace)
            {
                BurstNoiseByteCodeJob job = new()
                {
                    byteCode = byteCodePtr,
                    byteCodeLength = byteCode.Length,
                    registerSpace = registerSpacePtr,
                    registerSpaceLength = registerSpace.Length,
                    batchSize = batchSize,
                    seed = seed,
                };
                job.Run();
            }
        }

        /// <summary>
        /// Creates a bytecode evaluation job over the supplied buffers.
        /// </summary>
        public static BurstNoiseByteCodeJob CreateByteCodeJob(
            ReadOnlySpan<byte> byteCode,
            int seed,
            Span<float> registerSpace,
            int batchSize)
        {
            fixed (byte* byteCodePtr = byteCode)
            fixed (float* registerSpacePtr = registerSpace)
            {
                BurstNoiseByteCodeJob job = new()
                {
                    byteCode = byteCodePtr,
                    byteCodeLength = byteCode.Length,
                    registerSpace = registerSpacePtr,
                    registerSpaceLength = registerSpace.Length,
                    batchSize = batchSize,
                    seed = seed,
                };
                return job;
            }
        }
    }
}

#endif
