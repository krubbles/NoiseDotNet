using System.Runtime.InteropServices;
using NoiseDotNet;

namespace Tests
{
    public class NoiseNodeTests
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

        [Test]
        public void CompiledNoiseVariesAcrossSamplePoints()
        {
            // A compiled noise instruction must read the coordinate registers rather than
            // accidentally producing one constant value for the whole batch.
            NoiseVector2 coordinates = CreateCoordinates2D();
            CompiledNoiseNode compiled = NoiseNodeByteCode.Compile(CreatePerlin2D(coordinates));

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
            NoiseVector2 coordinates = CreateCoordinates2D();
            NoiseScalar noise = CreatePerlin2D(coordinates);
            CompiledNoiseNode firstCompilation = NoiseNodeByteCode.Compile(noise);
            CompiledNoiseNode secondCompilation = NoiseNodeByteCode.Compile(noise);

            float[] first = Evaluate(firstCompilation, seed: 41);
            float[] second = Evaluate(secondCompilation, seed: 97);

            AssertDifferent(first, second, "Different evaluation seeds produced identical samples.");
        }

        [Test]
        public void AddingSharedNoiseNodeIsEquivalentToDoublingIt()
        {
            // Both operands reference the same NoiseNode instance, so the compiler must assign
            // one internal seed and evaluate exactly the same function for both operands.
            NoiseVector2 coordinates = CreateCoordinates2D();
            NoiseScalar noise = CreatePerlin2D(coordinates);
            CompiledNoiseNode doubledGraph = NoiseNodeByteCode.Compile(noise + noise);
            CompiledNoiseNode baseGraph = NoiseNodeByteCode.Compile(noise);

            float[] doubled = Evaluate(doubledGraph, seed: 23);
            float[] original = Evaluate(baseGraph, seed: 23);

            for (int i = 0; i < doubled.Length; i++)
                AssertEqualEnough(original[i] * 2f, doubled[i], $"Sample {i} was not doubled.");
        }

        [Test]
        public void AddingDistinctNoiseNodesUsesDistinctInternalSeeds()
        {
            // Structurally identical but separately allocated noise nodes must receive different
            // internal seeds. Re-evaluating each node alone with its combined-graph seed lets us
            // verify both the sum and that it is not merely either function doubled.
            NoiseVector2 coordinates = CreateCoordinates2D();
            NoiseScalar leftNoise = CreatePerlin2D(coordinates);
            NoiseScalar rightNoise = CreatePerlin2D(coordinates);
            NoiseScalar sum = leftNoise + rightNoise;
            Dictionary<NoiseNode, int> combinedSeeds = NoiseNodeByteCode.GetNoiseSeeds(sum);

            CompiledNoiseNode combinedGraph = NoiseNodeByteCode.Compile(sum);
            CompiledNoiseNode leftGraph = NoiseNodeByteCode.Compile(leftNoise);
            CompiledNoiseNode rightGraph = NoiseNodeByteCode.Compile(rightNoise);
            int evaluationSeed = 61;

            float[] combined = Evaluate(combinedGraph, evaluationSeed);
            float[] left = Evaluate(leftGraph, evaluationSeed ^ combinedSeeds[leftNoise.Node]);
            float[] right = Evaluate(rightGraph, evaluationSeed ^ combinedSeeds[rightNoise.Node]);

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
            NoiseVector2 coordinates = CreateCoordinates2D();
            NoiseScalar octave0 = CreatePerlin2D(Scale(coordinates, 0.5f, 0.75f));
            NoiseScalar octave1 = CreatePerlin2D(Scale(coordinates, 1f, 1.5f));
            NoiseScalar octave2 = CreatePerlin2D(Scale(coordinates, 2f, 3f));
            NoiseScalar fbm = octave0 + octave1 * NoiseNode.Constant(0.5f) +
                              octave2 * NoiseNode.Constant(0.25f);
            Dictionary<NoiseNode, int> seeds = NoiseNodeByteCode.GetNoiseSeeds(fbm);
            CompiledNoiseNode compiled = NoiseNodeByteCode.Compile(fbm);
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
                -0.35843804f,
                0.013631545f,
                -0.019447811f,
                0.2464526f,
                -0.20418963f,
                0.22407344f,
                -0.005684115f,
                0.21527067f,
            ];
            for (int i = 0; i < actual.Length; i++)
                AssertEqualEnough(expected[i], actual[i], $"FBM golden sample {i} changed.");
        }

        static NoiseVector2 CreateCoordinates2D() =>
            new NoiseNode(NoiseNodeType.Coords2__NoIn__x_y, Array.Empty<NoiseScalar>()).AsVector2;

        static NoiseScalar CreatePerlin2D(NoiseVector2 coordinates) =>
            new NoiseNode(NoiseNodeType.Perlin2D_noise__x_y__noise, coordinates.X, coordinates.Y).AsScalar;

        static NoiseVector2 Scale(NoiseVector2 coordinates, float xFrequency, float yFrequency) =>
            new(
                coordinates.X * NoiseNode.Constant(xFrequency),
                coordinates.Y * NoiseNode.Constant(yFrequency));

        static float[] Evaluate(CompiledNoiseNode compiled, int seed)
        {
            ByteCodeInfo info = MemoryMarshal.Read<ByteCodeInfo>(compiled.ByteCode);
            int batchSize = XCoordinates.Length;
            float[] registerSpace = new float[checked(info.RegisterCount * batchSize)];
            XCoordinates.CopyTo(registerSpace, 0);
            YCoordinates.CopyTo(registerSpace, batchSize);

            NoiseNodeByteCode.Evaluate(compiled.ByteCode, seed, registerSpace, batchSize);
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
                new(xFreq: 0.5f, yFreq: 0.75f, seed: evaluationSeed ^ octave0Seed));
            Noise.GradientNoise2D(
                XCoordinates,
                YCoordinates,
                octave1,
                new(xFreq: 1f, yFreq: 1.5f, seed: evaluationSeed ^ octave1Seed));
            Noise.GradientNoise2D(
                XCoordinates,
                YCoordinates,
                octave2,
                new(xFreq: 2f, yFreq: 3f, seed: evaluationSeed ^ octave2Seed));

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
