using System.Runtime.InteropServices;
using NoiseDotNet;

namespace Tests
{
    public class NoiseGraphTests
    {
        const float Epsilon = 0.00001f;

        static readonly float[] XCoordinates =
        [
            -3.25f, -1.75f, -0.4f, 0.15f, 0.8f, 1.35f, 2.1f, 3.65f,
        ];

        static readonly float[] YCoordinates =
        [
            2.8f, -2.2f, 1.1f, -0.35f, 3.4f, -1.6f, 0.55f, 2.25f,
        ];

        static readonly float[] ZCoordinates =
        [
            1.4f, -0.6f, 2.3f, -1.75f, 0.2f, 3.1f, -2.4f, 0.9f,
        ];

        [Test]
        public void CompiledNoiseVariesAcrossSamplePoints()
        {
            // A compiled noise instruction must read the coordinate registers rather than
            // accidentally producing one constant value for the whole batch.
            NoiseVector2 coordinates = NoiseGraph.XY;
            NoiseGraphByteCode compiled = NoiseGraphByteCodeCompiler.Compile(CreatePerlin2D(coordinates));

            float[] actual = Evaluate(compiled, seed: 17);

            Assert.That(
                actual.Skip(1).Any(value => !EqualEnough(value, actual[0])),
                Is.True,
                "Compiled noise produced the same value at every sample point.");
        }

        [Test]
        public void CompiledNoiseChangesWithEvaluationSeed()
        {
            // Compiling the same graph twice should still allow the caller-provided
            // evaluation seed to select different deterministic noise functions.
            NoiseVector2 coordinates = NoiseGraph.XY;
            NoiseScalar noise = CreatePerlin2D(coordinates);
            NoiseGraphByteCode firstCompilation = NoiseGraphByteCodeCompiler.Compile(noise);
            NoiseGraphByteCode secondCompilation = NoiseGraphByteCodeCompiler.Compile(noise);

            float[] first = Evaluate(firstCompilation, seed: 41);
            float[] second = Evaluate(secondCompilation, seed: 97);

            AssertDifferent(first, second, "Different evaluation seeds produced identical samples.");
        }

        [Test]
        public void AddingSharedNoiseGraphIsEquivalentToDoublingIt()
        {
            // Both operands reference the same NoiseGraph instance, so the compiler must assign
            // one internal seed and evaluate exactly the same function for both operands.
            NoiseVector2 coordinates = NoiseGraph.XY;
            NoiseScalar noise = CreatePerlin2D(coordinates);
            NoiseGraphByteCode doubledGraph = NoiseGraphByteCodeCompiler.Compile(noise + noise);
            NoiseGraphByteCode baseGraph = NoiseGraphByteCodeCompiler.Compile(noise);

            float[] doubled = Evaluate(doubledGraph, seed: 23);
            float[] original = Evaluate(baseGraph, seed: 23);

            for (int i = 0; i < doubled.Length; i++)
                AssertEqualEnough(original[i] * 2f, doubled[i], $"Sample {i} was not doubled.");
        }

        [Test]
        public void AddingDistinctNoiseGraphsUsesDistinctInternalSeeds()
        {
            // Structurally identical but separately allocated noise nodes must receive different
            // internal seeds. Re-evaluating each node alone with its combined-graph seed lets us
            // verify both the sum and that it is not merely either function doubled.
            NoiseVector2 coordinates = NoiseGraph.XY;
            NoiseScalar leftNoise = CreatePerlin2D(coordinates);
            NoiseScalar rightNoise = CreatePerlin2D(coordinates);
            NoiseScalar sum = leftNoise + rightNoise;
            Dictionary<NoiseNode, int> combinedSeeds = NoiseGraphByteCodeCompiler.GetNoiseSeeds(sum);

            NoiseGraphByteCode combinedGraph = NoiseGraphByteCodeCompiler.Compile(sum);
            NoiseGraphByteCode leftGraph = NoiseGraphByteCodeCompiler.Compile(leftNoise);
            NoiseGraphByteCode rightGraph = NoiseGraphByteCodeCompiler.Compile(rightNoise);
            int evaluationSeed = 61;

            float[] combined = Evaluate(combinedGraph, evaluationSeed);
            float[] left = Evaluate(leftGraph, evaluationSeed + combinedSeeds[leftNoise.Node]);
            float[] right = Evaluate(rightGraph, evaluationSeed + combinedSeeds[rightNoise.Node]);

            Assert.That(combinedSeeds[leftNoise.Node], Is.Not.EqualTo(combinedSeeds[rightNoise.Node]));
            for (int i = 0; i < combined.Length; i++)
                AssertEqualEnough(left[i] + right[i], combined[i], $"Sample {i} did not contain both noise functions.");
            AssertDifferent(combined, left.Select(value => value * 2f).ToArray(), "The sum was the left function doubled.");
            AssertDifferent(combined, right.Select(value => value * 2f).ToArray(), "The sum was the right function doubled.");
        }

        [Test]
        public void FbmFoldsFrequenciesAndAccumulationAndMatchesExpectedOutput()
        {
            // Build three FBM octaves with increasing coordinate frequencies and decreasing
            // amplitudes. This exercises folded frequency metadata and the accumulate form used
            // when a noise instruction is added directly into an existing partial result.
            NoiseScalar octave0 = CreatePerlin2D(NoiseGraph.Coordinates(NoiseGraph.Constant(0.5f, 0.75f)));
            NoiseScalar octave1 = CreatePerlin2D(NoiseGraph.Coordinates(NoiseGraph.Constant(1f, 1.5f)));
            NoiseScalar octave2 = CreatePerlin2D(NoiseGraph.Coordinates(NoiseGraph.Constant(2f, 3f)));
            NoiseScalar fbm = octave0 + octave1 * NoiseGraph.Constant(0.5f) +
                              octave2 * NoiseGraph.Constant(0.25f);
            Dictionary<NoiseNode, int> seeds = NoiseGraphByteCodeCompiler.GetNoiseSeeds(fbm);
            NoiseGraphByteCode compiled = NoiseGraphByteCodeCompiler.Compile(fbm);
            const int evaluationSeed = 73;

            string disassembly = compiled.ToString();
            Assert.That(disassembly, Does.Contain("frequency = (0.5, 0.75, 1)"));
            Assert.That(disassembly, Does.Contain("frequency = (1, 1.5, 1)"));
            Assert.That(disassembly, Does.Contain("frequency = (2, 3, 1)"));
            Assert.That(disassembly, Does.Contain(", accumulate]"));

            float[] actual = Evaluate(compiled, evaluationSeed);
            float[] direct = EvaluateFbmDirectly(
                evaluationSeed,
                seeds[octave0.Node],
                seeds[octave1.Node],
                seeds[octave2.Node]);

            for (int i = 0; i < actual.Length; i++)
                AssertEqualEnough(direct[i], actual[i], $"Compiled FBM differed from direct evaluation at sample {i}.");

            // These fixed values make the test catch simultaneous regressions in both the
            // compiler and the direct reference path.
            float[] expected =
            [
                -0.3199076f,
                -0.002501659f,
                -0.17252332f,
                0.24282697f,
                -0.12385333f,
                0.0887285f,
                0.10709832f,
                0.19059989f,
            ];
            for (int i = 0; i < actual.Length; i++)
                AssertEqualEnough(expected[i], actual[i], $"FBM golden sample {i} changed.");
        }

        [Test]
        public void Evaluate2DWritesFourScalarOutputs()
        {
            NoiseScalar noise = CreatePerlin2D(NoiseGraph.XY);
            NoiseScalar negated = -noise;
            NoiseScalar doubled = noise * NoiseGraph.Constant(2f);
            NoiseScalar offset = noise + NoiseGraph.Constant(1f);
            float[] noiseOutput = new float[XCoordinates.Length];
            float[] negatedOutput = new float[XCoordinates.Length];
            float[] doubledOutput = new float[XCoordinates.Length];
            float[] offsetOutput = new float[XCoordinates.Length];

            NoiseGraph.Evaluate2D(
                noise,
                negated,
                doubled,
                offset,
                XCoordinates,
                YCoordinates,
                noiseOutput,
                negatedOutput,
                doubledOutput,
                offsetOutput,
                seed: 97);

            float[] expectedNoise = Evaluate(NoiseGraphByteCodeCompiler.Compile(noise), seed: 97);
            for (int sampleIndex = 0; sampleIndex < expectedNoise.Length; sampleIndex++)
            {
                float expected = expectedNoise[sampleIndex];
                AssertEqualEnough(expected, noiseOutput[sampleIndex], $"2D noise output {sampleIndex} was incorrect.");
                AssertEqualEnough(-expected, negatedOutput[sampleIndex], $"2D negated output {sampleIndex} was incorrect.");
                AssertEqualEnough(expected * 2f, doubledOutput[sampleIndex], $"2D doubled output {sampleIndex} was incorrect.");
                AssertEqualEnough(expected + 1f, offsetOutput[sampleIndex], $"2D offset output {sampleIndex} was incorrect.");
            }
        }

        [Test]
        public void Evaluate3DWritesFourScalarOutputs()
        {
            NoiseScalar noise = CreatePerlin3D(NoiseGraph.XYZ);
            NoiseScalar negated = -noise;
            NoiseScalar doubled = noise * NoiseGraph.Constant(2f);
            NoiseScalar offset = noise + NoiseGraph.Constant(1f);
            float[] noiseOutput = new float[XCoordinates.Length];
            float[] negatedOutput = new float[XCoordinates.Length];
            float[] doubledOutput = new float[XCoordinates.Length];
            float[] offsetOutput = new float[XCoordinates.Length];

            NoiseGraph.Evaluate3D(
                noise,
                negated,
                doubled,
                offset,
                XCoordinates,
                YCoordinates,
                ZCoordinates,
                noiseOutput,
                negatedOutput,
                doubledOutput,
                offsetOutput,
                seed: 101);

            float[] expectedNoise = Evaluate(NoiseGraphByteCodeCompiler.Compile(noise), seed: 101);
            for (int sampleIndex = 0; sampleIndex < expectedNoise.Length; sampleIndex++)
            {
                float expected = expectedNoise[sampleIndex];
                AssertEqualEnough(expected, noiseOutput[sampleIndex], $"3D noise output {sampleIndex} was incorrect.");
                AssertEqualEnough(-expected, negatedOutput[sampleIndex], $"3D negated output {sampleIndex} was incorrect.");
                AssertEqualEnough(expected * 2f, doubledOutput[sampleIndex], $"3D doubled output {sampleIndex} was incorrect.");
                AssertEqualEnough(expected + 1f, offsetOutput[sampleIndex], $"3D offset output {sampleIndex} was incorrect.");
            }
        }

        [Test]
        public void Evaluate2DProcessesLargeBuffersInCacheSizedBlocks()
        {
            const int sampleCount = 2051;
            float[] xCoords = Enumerable.Range(0, sampleCount).Select(index => index - 1025.5f).ToArray();
            float[] yCoords = new float[sampleCount];
            float[] output = new float[sampleCount];

            NoiseGraph.Evaluate2D(NoiseGraph.X * NoiseGraph.Constant(2f), xCoords, yCoords, output);

            for (int index = 0; index < sampleCount; index++)
                AssertEqualEnough(xCoords[index] * 2f, output[index], $"Block sample {index} was incorrect.");
        }

        static NoiseScalar CreatePerlin2D(NoiseVector2 coordinates) =>
            new NoiseNode(NoiseNodeType.Perlin2D_noise__x_y__noise, coordinates.X, coordinates.Y).AsScalar;

        static NoiseScalar CreatePerlin3D(NoiseVector3 coordinates) =>
            new NoiseNode(
                NoiseNodeType.Perlin3D_noise__x_y_z__noise,
                coordinates.X,
                coordinates.Y,
                coordinates.Z).AsScalar;

        static float[] Evaluate(NoiseGraphByteCode compiled, int seed)
        {
            ByteCodeInfo info = MemoryMarshal.Read<ByteCodeInfo>(compiled.ByteCode);
            int batchSize = XCoordinates.Length;
            float[] registerSpace = new float[checked(info.RegisterCount * batchSize)];
            XCoordinates.CopyTo(registerSpace, 0);
            YCoordinates.CopyTo(registerSpace, batchSize);
            if (info.InputCount == 3)
                ZCoordinates.CopyTo(registerSpace, batchSize * 2);

            NoiseGraphByteCodeEval.EvaluateByteCode(compiled.ByteCode, seed, registerSpace, batchSize);
            return registerSpace[..batchSize];
        }

        static float[] EvaluateFbmDirectly(
            int evaluationSeed,
            int octave0Seed,
            int octave1Seed,
            int octave2Seed)
        {
            float[] octave0 = new float[XCoordinates.Length];
            float[] octave1 = new float[XCoordinates.Length];
            float[] octave2 = new float[XCoordinates.Length];
            Noise.GradientNoise2D(
                XCoordinates,
                YCoordinates,
                octave0,
                new(xFreq: 0.5f, yFreq: 0.75f, seed: evaluationSeed + octave0Seed));
            Noise.GradientNoise2D(
                XCoordinates,
                YCoordinates,
                octave1,
                new(xFreq: 1f, yFreq: 1.5f, seed: evaluationSeed + octave1Seed));
            Noise.GradientNoise2D(
                XCoordinates,
                YCoordinates,
                octave2,
                new(xFreq: 2f, yFreq: 3f, seed: evaluationSeed + octave2Seed));

            float[] result = new float[XCoordinates.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = octave0[i] + octave1[i] * 0.5f + octave2[i] * 0.25f;
            return result;
        }

        static bool EqualEnough(float expected, float actual) =>
            MathF.Abs(expected - actual) <= Epsilon;

        static void AssertEqualEnough(float expected, float actual, string message) =>
            Assert.That(actual, Is.EqualTo(expected).Within(Epsilon), message);

        static void AssertDifferent(float[] first, float[] second, string message) =>
            Assert.That(
                first.Zip(second).Any(pair => !EqualEnough(pair.First, pair.Second)),
                Is.True,
                message);
    }
}
