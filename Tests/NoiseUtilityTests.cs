using System.Runtime.InteropServices;
using NoiseDotNet;

namespace Tests
{
    // Each test covers a NoiseGraph public utility function that doesn't reduce to a single operator type.
    // It verifies every overload creates the expected graph structure, then compiles a representative graph
    // and compares its output with an independent numerical reference implementation.
    public class NoiseUtilityTests
    {
        const float Epsilon = 0.00001f;

        static readonly float[] XCoordinates =
        [
            -3.25f, -1.75f, -0.4f, 0.15f, 0.8f, 1.35f, 3.65f,
        ];

        static readonly float[] YCoordinates =
        [
            2.8f, -2.2f, 1.1f, -0.35f, 3.4f, -1.6f, 0.55f,
        ];

        static readonly float[] ZCoordinates =
        [
            1.4f, -0.6f, 2.3f, -1.75f, 0.2f, 3.1f, -2.4f,
        ];

        [Test]
        public void AbsTests()
        {
            NoiseScalar scalar = NoiseGraph.X;
            NoiseVector2 vector2 = NoiseGraph.XY;
            NoiseVector3 vector3 = NoiseGraph.XYZ;

            AssertAbsGraph(NoiseGraph.Abs(scalar), scalar);
            AssertUnaryVectorGraph(NoiseGraph.Abs(vector2), vector2, AssertAbsGraph);
            AssertUnaryVectorGraph(NoiseGraph.Abs(vector3), vector3, AssertAbsGraph);

            AssertCompilesAccurately(NoiseGraph.Abs(scalar), MathF.Abs);
        }

        static void AssertAbsGraph(NoiseScalar actual, NoiseScalar value)
        {
            NoiseScalar negated = actual.Node.Inputs[1];
            AssertNode(actual, NoiseNodeType.Max__a_b__max, value, negated);
            AssertNode(negated, NoiseNodeType.Negate__value__negated, value);
        }

        [Test]
        public void ClampTests()
        {
            NoiseScalar scalarValue = NoiseGraph.X;
            NoiseScalar scalarMin = NoiseGraph.Constant(-1f);
            NoiseScalar scalarMax = NoiseGraph.Constant(1f);
            NoiseVector2 vector2Value = NoiseGraph.XY;
            NoiseVector2 vector2Min = NoiseGraph.Constant(-1f, -2f);
            NoiseVector2 vector2Max = NoiseGraph.Constant(1f, 2f);
            NoiseVector3 vector3Value = NoiseGraph.XYZ;
            NoiseVector3 vector3Min = NoiseGraph.Constant(-1f, -2f, -3f);
            NoiseVector3 vector3Max = NoiseGraph.Constant(1f, 2f, 3f);

            AssertClampGraph(NoiseGraph.Clamp(scalarValue, scalarMin, scalarMax), scalarValue, scalarMin, scalarMax);
            AssertTernaryVectorGraph(
                NoiseGraph.Clamp(vector2Value, vector2Min, vector2Max),
                vector2Value,
                vector2Min,
                vector2Max,
                AssertClampGraph);
            AssertTernaryVectorGraph(
                NoiseGraph.Clamp(vector3Value, vector3Min, vector3Max),
                vector3Value,
                vector3Min,
                vector3Max,
                AssertClampGraph);
            AssertVectorScalarClamp(
                NoiseGraph.Clamp(vector2Value, scalarMin, scalarMax),
                vector2Value,
                scalarMin,
                scalarMax);
            AssertVectorScalarClamp(
                NoiseGraph.Clamp(vector3Value, scalarMin, scalarMax),
                vector3Value,
                scalarMin,
                scalarMax);

            AssertCompilesAccurately(
                NoiseGraph.Clamp(scalarValue, scalarMin, scalarMax),
                value => Math.Clamp(value, -1f, 1f));
        }

        static void AssertTernaryVectorGraph(
            NoiseVector2 actual,
            NoiseVector2 value,
            NoiseVector2 min,
            NoiseVector2 max,
            Action<NoiseScalar, NoiseScalar, NoiseScalar, NoiseScalar> assertGraph)
        {
            assertGraph(actual.X, value.X, min.X, max.X);
            assertGraph(actual.Y, value.Y, min.Y, max.Y);
        }

        static void AssertTernaryVectorGraph(
            NoiseVector3 actual,
            NoiseVector3 value,
            NoiseVector3 min,
            NoiseVector3 max,
            Action<NoiseScalar, NoiseScalar, NoiseScalar, NoiseScalar> assertGraph)
        {
            assertGraph(actual.X, value.X, min.X, max.X);
            assertGraph(actual.Y, value.Y, min.Y, max.Y);
            assertGraph(actual.Z, value.Z, min.Z, max.Z);
        }

        static void AssertVectorScalarClamp(
            NoiseVector2 actual,
            NoiseVector2 value,
            NoiseScalar min,
            NoiseScalar max)
        {
            AssertClampGraph(actual.X, value.X, min, max);
            AssertClampGraph(actual.Y, value.Y, min, max);
        }

        static void AssertVectorScalarClamp(
            NoiseVector3 actual,
            NoiseVector3 value,
            NoiseScalar min,
            NoiseScalar max)
        {
            AssertClampGraph(actual.X, value.X, min, max);
            AssertClampGraph(actual.Y, value.Y, min, max);
            AssertClampGraph(actual.Z, value.Z, min, max);
        }

        [Test]
        public void SaturateTests()
        {
            NoiseScalar scalar = NoiseGraph.X;
            NoiseVector2 vector2 = NoiseGraph.XY;
            NoiseVector3 vector3 = NoiseGraph.XYZ;

            AssertSaturateGraph(NoiseGraph.Saturate(scalar), scalar);
            AssertUnaryVectorGraph(NoiseGraph.Saturate(vector2), vector2, AssertSaturateGraph);
            AssertUnaryVectorGraph(NoiseGraph.Saturate(vector3), vector3, AssertSaturateGraph);

            AssertCompilesAccurately(
                NoiseGraph.Saturate(scalar),
                value => Math.Clamp(value, 0f, 1f));
        }

        static void AssertSaturateGraph(NoiseScalar actual, NoiseScalar value)
        {
            AssertClampGraph(actual, value, NoiseGraph.Zero, NoiseGraph.One);
        }

        [Test]
        public void FractTests()
        {
            NoiseScalar scalar = NoiseGraph.X;
            NoiseVector2 vector2 = NoiseGraph.XY;
            NoiseVector3 vector3 = NoiseGraph.XYZ;

            AssertFractGraph(NoiseGraph.Fract(scalar), scalar);
            AssertUnaryVectorGraph(NoiseGraph.Fract(vector2), vector2, AssertFractGraph);
            AssertUnaryVectorGraph(NoiseGraph.Fract(vector3), vector3, AssertFractGraph);

            AssertCompilesAccurately(
                NoiseGraph.Fract(scalar),
                value => value - MathF.Floor(value));
        }

        static void AssertFractGraph(NoiseScalar actual, NoiseScalar value)
        {
            NoiseScalar negatedFloor = actual.Node.Inputs[1];
            NoiseScalar floor = negatedFloor.Node.Inputs[0];
            AssertNode(actual, NoiseNodeType.Add__a_b__sum, value, negatedFloor);
            AssertNode(negatedFloor, NoiseNodeType.Negate__value__negated, floor);
            AssertNode(floor, NoiseNodeType.Floor__value__result, value);
        }

        [Test]
        public void ModTests()
        {
            NoiseScalar scalarValue = NoiseGraph.X;
            NoiseScalar scalarModulus = NoiseGraph.Constant(1.25f);
            NoiseVector2 vector2Value = NoiseGraph.XY;
            NoiseVector2 vector2Modulus = NoiseGraph.Constant(1.25f, 2.5f);
            NoiseVector3 vector3Value = NoiseGraph.XYZ;
            NoiseVector3 vector3Modulus = NoiseGraph.Constant(1.25f, 2.5f, 3.75f);

            AssertModGraph(NoiseGraph.Mod(scalarValue, scalarModulus), scalarValue, scalarModulus);
            AssertBinaryVectorGraph(
                NoiseGraph.Mod(vector2Value, vector2Modulus),
                vector2Value,
                vector2Modulus,
                AssertModGraph);
            AssertBinaryVectorGraph(
                NoiseGraph.Mod(vector3Value, vector3Modulus),
                vector3Value,
                vector3Modulus,
                AssertModGraph);
            AssertVectorScalarMod(NoiseGraph.Mod(vector2Value, scalarModulus), vector2Value, scalarModulus);
            AssertVectorScalarMod(NoiseGraph.Mod(vector3Value, scalarModulus), vector3Value, scalarModulus);

            AssertCompilesAccurately(
                NoiseGraph.Mod(scalarValue, scalarModulus),
                value => value - 1.25f * MathF.Floor(value / 1.25f));
        }

        static void AssertModGraph(NoiseScalar actual, NoiseScalar value, NoiseScalar modulus)
        {
            NoiseScalar negatedProduct = actual.Node.Inputs[1];
            NoiseScalar product = negatedProduct.Node.Inputs[0];
            NoiseScalar floor = product.Node.Inputs[1];
            NoiseScalar quotient = floor.Node.Inputs[0];
            NoiseScalar inverse = quotient.Node.Inputs[1];

            AssertNode(actual, NoiseNodeType.Add__a_b__sum, value, negatedProduct);
            AssertNode(negatedProduct, NoiseNodeType.Negate__value__negated, product);
            AssertNode(product, NoiseNodeType.Multiply__a_b__product, modulus, floor);
            AssertNode(floor, NoiseNodeType.Floor__value__result, quotient);
            AssertNode(quotient, NoiseNodeType.Multiply__a_b__product, value, inverse);
            AssertNode(inverse, NoiseNodeType.Inverse__value__inverse, modulus);
        }

        static void AssertBinaryVectorGraph(
            NoiseVector2 actual,
            NoiseVector2 a,
            NoiseVector2 b,
            Action<NoiseScalar, NoiseScalar, NoiseScalar> assertGraph)
        {
            assertGraph(actual.X, a.X, b.X);
            assertGraph(actual.Y, a.Y, b.Y);
        }

        static void AssertBinaryVectorGraph(
            NoiseVector3 actual,
            NoiseVector3 a,
            NoiseVector3 b,
            Action<NoiseScalar, NoiseScalar, NoiseScalar> assertGraph)
        {
            assertGraph(actual.X, a.X, b.X);
            assertGraph(actual.Y, a.Y, b.Y);
            assertGraph(actual.Z, a.Z, b.Z);
        }

        static void AssertVectorScalarMod(
            NoiseVector2 actual,
            NoiseVector2 value,
            NoiseScalar modulus)
        {
            AssertModGraph(actual.X, value.X, modulus);
            AssertModGraph(actual.Y, value.Y, modulus);
        }

        static void AssertVectorScalarMod(
            NoiseVector3 actual,
            NoiseVector3 value,
            NoiseScalar modulus)
        {
            AssertModGraph(actual.X, value.X, modulus);
            AssertModGraph(actual.Y, value.Y, modulus);
            AssertModGraph(actual.Z, value.Z, modulus);
        }

        [Test]
        public void ExpTests()
        {
            NoiseScalar scalar = NoiseGraph.X;
            NoiseVector2 vector2 = NoiseGraph.XY;
            NoiseVector3 vector3 = NoiseGraph.XYZ;

            AssertExpGraph(NoiseGraph.Exp(scalar), scalar);
            AssertUnaryVectorGraph(NoiseGraph.Exp(vector2), vector2, AssertExpGraph);
            AssertUnaryVectorGraph(NoiseGraph.Exp(vector3), vector3, AssertExpGraph);

            AssertCompilesAccurately(NoiseGraph.Exp(scalar), MathF.Exp);
        }

        static void AssertExpGraph(NoiseScalar actual, NoiseScalar value)
        {
            NoiseScalar e = actual.Node.Inputs[0];
            AssertNode(actual, NoiseNodeType.Pow__value_power__result, e, value);
            Assert.That(e.Node.Type, Is.EqualTo(NoiseNodeType.Constant1__NoIn__x));
            Assert.That(e.ChannelIndex, Is.Zero);
            Assert.That(e.Node.ConstantValues.ToArray(), Is.EqualTo(new[] { MathF.E }));
        }

        [Test]
        public void TransformTests()
        {
            NoiseVector2 coordinates2D = NoiseGraph.XY;
            NoiseScalar perlin2D = CreatePerlin2D(coordinates2D);
            NoiseVector2 cellular2D = CreateCellular2D(coordinates2D);
            NoiseVector3 outputs2D = new(perlin2D, cellular2D.X, cellular2D.Y);
            NoiseVector2 i2D = NoiseGraph.Constant(2f, 3f);
            NoiseVector2 j2D = NoiseGraph.Constant(5f, 7f);
            NoiseScalar[][] basis2D =
            [
                [i2D.X, i2D.Y],
                [j2D.X, j2D.Y],
            ];

            NoiseScalar transformedScalar2D = NoiseGraph.Transform(perlin2D, i2D, j2D);
            NoiseVector2 transformedVector2D = NoiseGraph.Transform(cellular2D, i2D, j2D);
            NoiseVector3 transformedVector3D = NoiseGraph.Transform(outputs2D, i2D, j2D);
            AssertTransformedNoise(transformedScalar2D, perlin2D, basis2D);
            AssertTransformedNoise(transformedVector2D.X, cellular2D.X, basis2D);
            AssertTransformedNoise(transformedVector2D.Y, cellular2D.Y, basis2D);
            Assert.That(transformedVector2D.X.Node, Is.SameAs(transformedVector2D.Y.Node));
            AssertTransformedNoise(transformedVector3D.X, outputs2D.X, basis2D);
            AssertTransformedNoise(transformedVector3D.Y, outputs2D.Y, basis2D);
            AssertTransformedNoise(transformedVector3D.Z, outputs2D.Z, basis2D);
            Assert.That(transformedVector3D.Y.Node, Is.SameAs(transformedVector3D.Z.Node));

            NoiseScalar reference2D = CreatePerlin2D(new(
                coordinates2D.X * i2D.X + coordinates2D.Y * j2D.X,
                coordinates2D.X * i2D.Y + coordinates2D.Y * j2D.Y));
            AssertCompilesLikeReference(transformedScalar2D, reference2D);

            NoiseVector3 coordinates3D = NoiseGraph.XYZ;
            NoiseScalar perlin3D = CreatePerlin3D(coordinates3D);
            NoiseVector2 cellular3D = CreateCellular3D(coordinates3D);
            NoiseVector3 outputs3D = new(perlin3D, cellular3D.X, cellular3D.Y);
            NoiseVector3 i3D = NoiseGraph.Constant(2f, 3f, 5f);
            NoiseVector3 j3D = NoiseGraph.Constant(7f, 11f, 13f);
            NoiseVector3 k3D = NoiseGraph.Constant(17f, 19f, 23f);
            NoiseScalar[][] basis3D =
            [
                [i3D.X, i3D.Y, i3D.Z],
                [j3D.X, j3D.Y, j3D.Z],
                [k3D.X, k3D.Y, k3D.Z],
            ];

            NoiseScalar transformedScalar3D = NoiseGraph.Transform(perlin3D, i3D, j3D, k3D);
            NoiseVector2 transformedVector2D3 = NoiseGraph.Transform(cellular3D, i3D, j3D, k3D);
            NoiseVector3 transformedVector3D3 = NoiseGraph.Transform(outputs3D, i3D, j3D, k3D);
            AssertTransformedNoise(transformedScalar3D, perlin3D, basis3D);
            AssertTransformedNoise(transformedVector2D3.X, cellular3D.X, basis3D);
            AssertTransformedNoise(transformedVector2D3.Y, cellular3D.Y, basis3D);
            Assert.That(transformedVector2D3.X.Node, Is.SameAs(transformedVector2D3.Y.Node));
            AssertTransformedNoise(transformedVector3D3.X, outputs3D.X, basis3D);
            AssertTransformedNoise(transformedVector3D3.Y, outputs3D.Y, basis3D);
            AssertTransformedNoise(transformedVector3D3.Z, outputs3D.Z, basis3D);
            Assert.That(transformedVector3D3.Y.Node, Is.SameAs(transformedVector3D3.Z.Node));

            NoiseScalar reference3D = CreatePerlin3D(new(
                coordinates3D.X * i3D.X + coordinates3D.Y * j3D.X + coordinates3D.Z * k3D.X,
                coordinates3D.X * i3D.Y + coordinates3D.Y * j3D.Y + coordinates3D.Z * k3D.Y,
                coordinates3D.X * i3D.Z + coordinates3D.Y * j3D.Z + coordinates3D.Z * k3D.Z));
            AssertCompilesLikeReference(transformedScalar3D, reference3D);
        }

        static void AssertTransformedNoise(
            NoiseScalar actual,
            NoiseScalar original,
            NoiseScalar[][] basis)
        {
            Assert.That(actual.Node, Is.Not.SameAs(original.Node));
            Assert.That(actual.Node.Type, Is.EqualTo(original.Node.Type));
            Assert.That(actual.ChannelIndex, Is.EqualTo(original.ChannelIndex));
            Assert.That(actual.Node.InputChannelCount, Is.EqualTo(basis.Length));

            NoiseScalar[] originalCoordinates = original.Node.Inputs.ToArray();
            for (int outputAxis = 0; outputAxis < basis.Length; outputAxis++)
            {
                NoiseScalar[] coefficients = basis
                    .Select(inputBasis => inputBasis[outputAxis])
                    .ToArray();
                AssertLinearCombination(
                    actual.Node.Inputs[outputAxis],
                    originalCoordinates,
                    coefficients,
                    coefficients.Length);
            }
        }

        static void AssertLinearCombination(
            NoiseScalar actual,
            NoiseScalar[] values,
            NoiseScalar[] coefficients,
            int termCount)
        {
            if (termCount == 1)
            {
                AssertNode(
                    actual,
                    NoiseNodeType.Multiply__a_b__product,
                    values[0],
                    coefficients[0]);
                return;
            }

            NoiseScalar precedingTerms = actual.Node.Inputs[0];
            NoiseScalar finalTerm = actual.Node.Inputs[1];
            AssertNode(
                actual,
                NoiseNodeType.Add__a_b__sum,
                precedingTerms,
                finalTerm);
            AssertLinearCombination(precedingTerms, values, coefficients, termCount - 1);
            AssertNode(
                finalTerm,
                NoiseNodeType.Multiply__a_b__product,
                values[termCount - 1],
                coefficients[termCount - 1]);
        }

        static void AssertCompilesLikeReference(NoiseScalar graph, NoiseScalar reference)
        {
            float[] actual = Evaluate(NoiseGraphByteCodeCompiler.Compile(graph));
            float[] expected = Evaluate(NoiseGraphByteCodeCompiler.Compile(reference));

            for (int i = 0; i < actual.Length; i++)
            {
                Assert.That(
                    actual[i],
                    Is.EqualTo(expected[i]).Within(Epsilon),
                    $"Compiled sample {i} differed from the reference graph.");
            }
        }

        static void AssertClampGraph(
            NoiseScalar actual,
            NoiseScalar value,
            NoiseScalar min,
            NoiseScalar max)
        {
            NoiseScalar maximum = actual.Node.Inputs[0];
            AssertNode(actual, NoiseNodeType.Min__a_b__min, maximum, max);
            AssertNode(maximum, NoiseNodeType.Max__a_b__max, value, min);
        }

        static void AssertNode(
            NoiseScalar actual,
            NoiseNodeType type,
            params NoiseScalar[] inputs)
        {
            Assert.That(actual.Node.Type, Is.EqualTo(type));
            Assert.That(actual.ChannelIndex, Is.Zero);
            Assert.That(actual.Node.Inputs.ToArray(), Is.EqualTo(inputs));
        }

        static void AssertUnaryVectorGraph(
            NoiseVector2 actual,
            NoiseVector2 value,
            Action<NoiseScalar, NoiseScalar> assertGraph)
        {
            assertGraph(actual.X, value.X);
            assertGraph(actual.Y, value.Y);
        }

        static void AssertUnaryVectorGraph(
            NoiseVector3 actual,
            NoiseVector3 value,
            Action<NoiseScalar, NoiseScalar> assertGraph)
        {
            assertGraph(actual.X, value.X);
            assertGraph(actual.Y, value.Y);
            assertGraph(actual.Z, value.Z);
        }

        static void AssertCompilesAccurately(NoiseScalar graph, Func<float, float> referenceImpl)
        {
            float[] actual = Evaluate(NoiseGraphByteCodeCompiler.Compile(graph));

            for (int i = 0; i < actual.Length; i++)
            {
                Assert.That(
                    actual[i],
                    Is.EqualTo(referenceImpl(XCoordinates[i])).Within(Epsilon),
                    $"Compiled sample {i} was inaccurate.");
            }
        }

        static float[] Evaluate(NoiseGraphByteCode compiled)
        {
            ByteCodeInfo info = MemoryMarshal.Read<ByteCodeInfo>(compiled.ByteCode);
            int batchSize = XCoordinates.Length;
            float[] registers = new float[checked(info.RegisterCount * batchSize)];
            if (info.InputCount >= 1)
                XCoordinates.CopyTo(registers, 0);
            if (info.InputCount >= 2)
                YCoordinates.CopyTo(registers, batchSize);
            if (info.InputCount >= 3)
                ZCoordinates.CopyTo(registers, batchSize * 2);

            NoiseGraphByteCodeEval.EvaluateByteCode(compiled.ByteCode, seed: 0, registers, batchSize);
            return registers[..batchSize];
        }

        static NoiseScalar CreatePerlin2D(NoiseVector2 coordinates) =>
            new NoiseNode(
                NoiseNodeType.Perlin2D_noise__x_y__noise,
                coordinates.X,
                coordinates.Y).AsScalar;

        static NoiseVector2 CreateCellular2D(NoiseVector2 coordinates) =>
            new NoiseNode(
                NoiseNodeType.Cellular2_noise__x_y__center_edge,
                coordinates.X,
                coordinates.Y).AsVector2;

        static NoiseScalar CreatePerlin3D(NoiseVector3 coordinates) =>
            new NoiseNode(
                NoiseNodeType.Perlin3D_noise__x_y_z__noise,
                coordinates.X,
                coordinates.Y,
                coordinates.Z).AsScalar;

        static NoiseVector2 CreateCellular3D(NoiseVector3 coordinates) =>
            new NoiseNode(
                NoiseNodeType.Cellular3_noise__x_y_z__center_edge,
                coordinates.X,
                coordinates.Y,
                coordinates.Z).AsVector2;
    }
}
