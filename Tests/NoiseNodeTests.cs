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

        static readonly float[] ZCoordinates =
        [
            1.4f, -0.6f, 2.3f, -1.75f, 0.2f, 3.1f, -2.4f, 0.9f,
        ];

        [Test]
        public void CompiledNoiseVariesAcrossSamplePoints()
        {
            // A compiled noise instruction must read the coordinate registers rather than
            // accidentally producing one constant value for the whole batch.
            NoiseVector2 coordinates = CreateCoordinates2D();
            CompiledNoiseNode compiled = NoiseNodeEval.Compile(CreatePerlin2D(coordinates));

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
            CompiledNoiseNode firstCompilation = NoiseNodeEval.Compile(noise);
            CompiledNoiseNode secondCompilation = NoiseNodeEval.Compile(noise);

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
            CompiledNoiseNode doubledGraph = NoiseNodeEval.Compile(noise + noise);
            CompiledNoiseNode baseGraph = NoiseNodeEval.Compile(noise);

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
            Dictionary<NoiseNode, int> combinedSeeds = NoiseNodeEval.GetNoiseSeeds(sum);

            CompiledNoiseNode combinedGraph = NoiseNodeEval.Compile(sum);
            CompiledNoiseNode leftGraph = NoiseNodeEval.Compile(leftNoise);
            CompiledNoiseNode rightGraph = NoiseNodeEval.Compile(rightNoise);
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
            Dictionary<NoiseNode, int> seeds = NoiseNodeEval.GetNoiseSeeds(fbm);
            CompiledNoiseNode compiled = NoiseNodeEval.Compile(fbm);
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

        [Test]
        public void TransformRemapsTwoDimensionalNoiseCoordinates()
        {
            NoiseVector2 coordinates = CreateCoordinates2D();
            NoiseScalar original = CreatePerlin2D(coordinates);
            NoiseVector2 i = NoiseNode.Constant(2f, 3f);
            NoiseVector2 j = NoiseNode.Constant(5f, 7f);

            NoiseScalar transformed = NoiseNode.Transform(original, i, j);
            NoiseScalar expected = CreatePerlin2D(new(
                coordinates.X * i.X + coordinates.Y * j.X,
                coordinates.X * i.Y + coordinates.Y * j.Y));

            float[] actualValues = Evaluate(NoiseNodeEval.Compile(transformed), seed: 83);
            float[] expectedValues = Evaluate(NoiseNodeEval.Compile(expected), seed: 83);
            for (int sampleIndex = 0; sampleIndex < actualValues.Length; sampleIndex++)
            {
                AssertEqualEnough(
                    expectedValues[sampleIndex],
                    actualValues[sampleIndex],
                    $"Transformed 2D sample {sampleIndex} was incorrect.");
            }
        }

        [Test]
        public void TransformRemapsThreeDimensionalNoiseCoordinates()
        {
            NoiseVector3 coordinates = CreateCoordinates3D();
            NoiseScalar original = CreatePerlin3D(coordinates);
            NoiseVector3 i = NoiseNode.Constant(2f, 3f, 5f);
            NoiseVector3 j = NoiseNode.Constant(7f, 11f, 13f);
            NoiseVector3 k = NoiseNode.Constant(17f, 19f, 23f);

            NoiseScalar transformed = NoiseNode.Transform(original, i, j, k);
            NoiseScalar expected = CreatePerlin3D(new(
                coordinates.X * i.X + coordinates.Y * j.X + coordinates.Z * k.X,
                coordinates.X * i.Y + coordinates.Y * j.Y + coordinates.Z * k.Y,
                coordinates.X * i.Z + coordinates.Y * j.Z + coordinates.Z * k.Z));

            float[] actualValues = Evaluate(NoiseNodeEval.Compile(transformed), seed: 89);
            float[] expectedValues = Evaluate(NoiseNodeEval.Compile(expected), seed: 89);
            for (int sampleIndex = 0; sampleIndex < actualValues.Length; sampleIndex++)
            {
                AssertEqualEnough(
                    expectedValues[sampleIndex],
                    actualValues[sampleIndex],
                    $"Transformed 3D sample {sampleIndex} was incorrect.");
            }
        }

        [Test]
        public void Evaluate2DWritesFourScalarOutputs()
        {
            NoiseScalar noise = CreatePerlin2D(CreateCoordinates2D());
            NoiseScalar negated = -noise;
            NoiseScalar doubled = noise * NoiseNode.Constant(2f);
            NoiseScalar offset = noise + NoiseNode.Constant(1f);
            float[] noiseOutput = new float[XCoordinates.Length];
            float[] negatedOutput = new float[XCoordinates.Length];
            float[] doubledOutput = new float[XCoordinates.Length];
            float[] offsetOutput = new float[XCoordinates.Length];

            NoiseNode.Evaluate2D(
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

            float[] expectedNoise = Evaluate(NoiseNodeEval.Compile(noise), seed: 97);
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
            NoiseScalar noise = CreatePerlin3D(CreateCoordinates3D());
            NoiseScalar negated = -noise;
            NoiseScalar doubled = noise * NoiseNode.Constant(2f);
            NoiseScalar offset = noise + NoiseNode.Constant(1f);
            float[] noiseOutput = new float[XCoordinates.Length];
            float[] negatedOutput = new float[XCoordinates.Length];
            float[] doubledOutput = new float[XCoordinates.Length];
            float[] offsetOutput = new float[XCoordinates.Length];

            NoiseNode.Evaluate3D(
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

            float[] expectedNoise = Evaluate(NoiseNodeEval.Compile(noise), seed: 101);
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
        public void MinAndMaxEvaluateComponentWise()
        {
            // Scalar operations select the expected operand for every sample, while vector
            // overloads construct one independent operation per component.
            NoiseVector2 coordinates = CreateCoordinates2D();
            NoiseScalar minimum = NoiseNode.Min(coordinates.X, NoiseNode.Constant(0.5f));
            NoiseScalar maximum = NoiseNode.Max(coordinates.Y, NoiseNode.Constant(-0.25f));

            float[] minimumValues = Evaluate(NoiseNodeEval.Compile(minimum), seed: 0);
            float[] maximumValues = Evaluate(NoiseNodeEval.Compile(maximum), seed: 0);
            for (int i = 0; i < XCoordinates.Length; i++)
            {
                AssertEqualEnough(MathF.Min(XCoordinates[i], 0.5f), minimumValues[i], $"Minimum sample {i} was incorrect.");
                AssertEqualEnough(MathF.Max(YCoordinates[i], -0.25f), maximumValues[i], $"Maximum sample {i} was incorrect.");
            }

            NoiseVector2 vectorMinimum = NoiseNode.Min(coordinates, NoiseNode.Constant(1f, 2f));
            NoiseVector2 vectorMaximum = NoiseNode.Max(coordinates, NoiseNode.Constant(1f, 2f));
            Assert.That(vectorMinimum.X.Node.Type, Is.EqualTo(NoiseNodeType.Min__a_b__min));
            Assert.That(vectorMinimum.Y.Node.Type, Is.EqualTo(NoiseNodeType.Min__a_b__min));
            Assert.That(vectorMaximum.X.Node.Type, Is.EqualTo(NoiseNodeType.Max__a_b__max));
            Assert.That(vectorMaximum.Y.Node.Type, Is.EqualTo(NoiseNodeType.Max__a_b__max));
        }

        [Test]
        public void PowEvaluatesScalarAndVectorPowers()
        {
            // Squaring the coordinate samples covers negative and positive bases, while the
            // vector overloads verify that component-wise and shared exponents build Pow nodes.
            NoiseVector2 coordinates = CreateCoordinates2D();
            NoiseScalar squared = NoiseNode.Pow(coordinates.X, NoiseNode.Constant(2f));

            float[] squaredValues = Evaluate(NoiseNodeEval.Compile(squared), seed: 0);
            for (int i = 0; i < XCoordinates.Length; i++)
                AssertEqualEnough(MathF.Pow(XCoordinates[i], 2f), squaredValues[i], $"Power sample {i} was incorrect.");

            NoiseVector2 componentPowers = NoiseNode.Pow(coordinates, NoiseNode.Constant(2f, 3f));
            NoiseVector2 sharedPower = NoiseNode.Pow(coordinates, NoiseNode.Constant(0.5f));
            Assert.That(componentPowers.X.Node.Type, Is.EqualTo(NoiseNodeType.Pow__value_power__result));
            Assert.That(componentPowers.Y.Node.Type, Is.EqualTo(NoiseNodeType.Pow__value_power__result));
            Assert.That(sharedPower.X.Node.Type, Is.EqualTo(NoiseNodeType.Pow__value_power__result));
            Assert.That(sharedPower.Y.Node.Type, Is.EqualTo(NoiseNodeType.Pow__value_power__result));
        }

        [Test]
        public void SmoothStep01ClampsAndSmoothsScalarAndVectorValues()
        {
            // The fixed coordinate set includes values below zero, inside the unit interval,
            // and above one, validating both endpoint clamping and the smoothstep polynomial.
            NoiseVector2 coordinates = CreateCoordinates2D();
            NoiseScalar smoothed = NoiseNode.SmoothStep(coordinates.X);

            float[] actual = Evaluate(NoiseNodeEval.Compile(smoothed), seed: 0);
            for (int i = 0; i < XCoordinates.Length; i++)
            {
                float clamped = Math.Clamp(XCoordinates[i], 0f, 1f);
                float expected = clamped * clamped * (3f - 2f * clamped);
                AssertEqualEnough(expected, actual[i], $"Smoothstep sample {i} was incorrect.");
            }

            NoiseVector2 vectorResult = NoiseNode.SmoothStep(coordinates);
            Assert.That(vectorResult.X.Node.Type, Is.EqualTo(NoiseNodeType.SmoothStep01__value__result));
            Assert.That(vectorResult.Y.Node.Type, Is.EqualTo(NoiseNodeType.SmoothStep01__value__result));
        }

        [Test]
        public void LerpInterpolatesAndExtrapolatesScalarAndVectorValues()
        {
            // Using the X coordinate as t covers values below zero, between zero and one,
            // and above one, verifying that the native operation intentionally remains unclamped.
            NoiseVector2 coordinates = CreateCoordinates2D();
            NoiseScalar interpolated = NoiseNode.Lerp(
                NoiseNode.Constant(-2f),
                NoiseNode.Constant(6f),
                coordinates.X);

            float[] actual = Evaluate(NoiseNodeEval.Compile(interpolated), seed: 0);
            for (int i = 0; i < XCoordinates.Length; i++)
            {
                float expected = -2f + (6f - -2f) * XCoordinates[i];
                AssertEqualEnough(expected, actual[i], $"Lerp sample {i} was incorrect.");
            }

            NoiseVector2 start = NoiseNode.Constant(-2f, 4f);
            NoiseVector2 end = NoiseNode.Constant(6f, 8f);
            NoiseVector2 sharedFactor = NoiseNode.Lerp(start, end, coordinates.X);
            NoiseVector2 componentFactors = NoiseNode.Lerp(start, end, coordinates);
            Assert.That(sharedFactor.X.Node.Type, Is.EqualTo(NoiseNodeType.Lerp__a_b_t__result));
            Assert.That(sharedFactor.Y.Node.Type, Is.EqualTo(NoiseNodeType.Lerp__a_b_t__result));
            Assert.That(componentFactors.X.Node.Type, Is.EqualTo(NoiseNodeType.Lerp__a_b_t__result));
            Assert.That(componentFactors.Y.Node.Type, Is.EqualTo(NoiseNodeType.Lerp__a_b_t__result));
        }

        [Test]
        public void FloorRoundsScalarAndVectorValuesTowardNegativeInfinity()
        {
            // The coordinate set contains negative and positive fractional values, distinguishing
            // floor from truncation and validating both sides of zero.
            NoiseVector2 coordinates = CreateCoordinates2D();
            NoiseScalar floored = NoiseNode.Floor(coordinates.X);

            float[] actual = Evaluate(NoiseNodeEval.Compile(floored), seed: 0);
            for (int i = 0; i < XCoordinates.Length; i++)
                AssertEqualEnough(MathF.Floor(XCoordinates[i]), actual[i], $"Floor sample {i} was incorrect.");

            NoiseVector2 vectorResult = NoiseNode.Floor(coordinates);
            Assert.That(vectorResult.X.Node.Type, Is.EqualTo(NoiseNodeType.Floor__value__result));
            Assert.That(vectorResult.Y.Node.Type, Is.EqualTo(NoiseNodeType.Floor__value__result));
        }

        [Test]
        public void ComposedMathUtilitiesEvaluateWithoutDedicatedOpcodes()
        {
            // Each utility is built exclusively from existing node operations. The samples span
            // negative and positive values to exercise clamp, fractional, and modulus behavior.
            NoiseVector2 coordinates = CreateCoordinates2D();
            NoiseScalar modulus = NoiseNode.Constant(1.25f);
            (NoiseScalar Node, Func<float, float> Expected, string Name)[] cases =
            [
                (NoiseNode.Abs(coordinates.X), MathF.Abs, "Abs"),
                (
                    NoiseNode.Clamp(coordinates.X, NoiseNode.Constant(-1f), NoiseNode.Constant(1f)),
                    value => Math.Clamp(value, -1f, 1f),
                    "Clamp"
                ),
                (NoiseNode.Saturate(coordinates.X), value => Math.Clamp(value, 0f, 1f), "Saturate"),
                (NoiseNode.Fract(coordinates.X), value => value - MathF.Floor(value), "Fract"),
                (
                    NoiseNode.Mod(coordinates.X, modulus),
                    value => value - 1.25f * MathF.Floor(value / 1.25f),
                    "Mod"
                ),
                (NoiseNode.Exp(coordinates.X), MathF.Exp, "Exp"),
            ];

            foreach ((NoiseScalar node, Func<float, float> expected, string name) in cases)
            {
                float[] actual = Evaluate(NoiseNodeEval.Compile(node), seed: 0);
                for (int i = 0; i < XCoordinates.Length; i++)
                    AssertEqualEnough(expected(XCoordinates[i]), actual[i], $"{name} sample {i} was incorrect.");
            }

            // The root types demonstrate that these helpers expand to existing instructions.
            Assert.That(NoiseNode.Abs(coordinates.X).Node.Type, Is.EqualTo(NoiseNodeType.Max__a_b__max));
            Assert.That(NoiseNode.Clamp(coordinates.X, modulus, modulus).Node.Type, Is.EqualTo(NoiseNodeType.Min__a_b__min));
            Assert.That(NoiseNode.Fract(coordinates.X).Node.Type, Is.EqualTo(NoiseNodeType.Add__a_b__sum));
            Assert.That(NoiseNode.Mod(coordinates.X, modulus).Node.Type, Is.EqualTo(NoiseNodeType.Add__a_b__sum));
            Assert.That(NoiseNode.Exp(coordinates.X).Node.Type, Is.EqualTo(NoiseNodeType.Pow__value_power__result));

            NoiseScalar saturatedScalar = NoiseNode.Saturate(coordinates.X);
            Assert.That(saturatedScalar.Node.Inputs[1].Node, Is.SameAs(NoiseNode.One.Node));
            Assert.That(saturatedScalar.Node.Inputs[0].Node.Inputs[1].Node, Is.SameAs(NoiseNode.Zero.Node));

            NoiseVector2 vector = NoiseNode.Saturate(coordinates);
            Assert.That(vector.X.Node.Type, Is.EqualTo(NoiseNodeType.Min__a_b__min));
            Assert.That(vector.Y.Node.Type, Is.EqualTo(NoiseNodeType.Min__a_b__min));
        }

        static NoiseVector2 CreateCoordinates2D() =>
            new NoiseNode(NoiseNodeType.Coords2__NoIn__x_y, Array.Empty<NoiseScalar>()).AsVector2;

        static NoiseVector3 CreateCoordinates3D() =>
            new NoiseNode(NoiseNodeType.Coords3__NoIn__x_y_z, Array.Empty<NoiseScalar>()).AsVector3;

        static NoiseScalar CreatePerlin2D(NoiseVector2 coordinates) =>
            new NoiseNode(NoiseNodeType.Perlin2D_noise__x_y__noise, coordinates.X, coordinates.Y).AsScalar;

        static NoiseScalar CreatePerlin3D(NoiseVector3 coordinates) =>
            new NoiseNode(
                NoiseNodeType.Perlin3D_noise__x_y_z__noise,
                coordinates.X,
                coordinates.Y,
                coordinates.Z).AsScalar;

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
            if (info.InputCount == 3)
                ZCoordinates.CopyTo(registerSpace, batchSize * 2);

            NoiseNodeEval.EvaluateByteCode(compiled.ByteCode, seed, registerSpace, batchSize);
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
