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

        static void AssertAbsGraph(NoiseScalar actual, NoiseScalar value)
        {
            NoiseScalar negated = actual.Node.Inputs[1];
            AssertNode(actual, NoiseNodeType.Max__a_b__max, value, negated);
            AssertNode(negated, NoiseNodeType.Negate__value__negated, value);
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

        static void AssertSaturateGraph(NoiseScalar actual, NoiseScalar value)
        {
            AssertClampGraph(actual, value, NoiseGraph.Zero, NoiseGraph.One);
        }

        static void AssertFractGraph(NoiseScalar actual, NoiseScalar value)
        {
            NoiseScalar negatedFloor = actual.Node.Inputs[1];
            NoiseScalar floor = negatedFloor.Node.Inputs[0];
            AssertNode(actual, NoiseNodeType.Add__a_b__sum, value, negatedFloor);
            AssertNode(negatedFloor, NoiseNodeType.Negate__value__negated, floor);
            AssertNode(floor, NoiseNodeType.Floor__value__result, value);
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

        static void AssertExpGraph(NoiseScalar actual, NoiseScalar value)
        {
            NoiseScalar e = actual.Node.Inputs[0];
            AssertNode(actual, NoiseNodeType.Pow__value_power__result, e, value);
            Assert.That(e.Node.Type, Is.EqualTo(NoiseNodeType.Constant1__NoIn__x));
            Assert.That(e.ChannelIndex, Is.Zero);
            Assert.That(e.Node.ConstantValues.ToArray(), Is.EqualTo(new[] { MathF.E }));
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

        static void AssertCompilesAccurately(NoiseScalar graph, Func<float, float> referenceImpl)
        {
            NoiseGraphByteCode compiled = NoiseGraphByteCodeCompiler.Compile(graph);
            ByteCodeInfo info = MemoryMarshal.Read<ByteCodeInfo>(compiled.ByteCode);
            int batchSize = XCoordinates.Length;
            float[] registers = new float[checked(info.RegisterCount * batchSize)];
            XCoordinates.CopyTo(registers, 0);

            NoiseGraphByteCodeEval.EvaluateByteCode(compiled.ByteCode, seed: 0, registers, batchSize);

            for (int i = 0; i < batchSize; i++)
            {
                Assert.That(
                    registers[i],
                    Is.EqualTo(referenceImpl(XCoordinates[i])).Within(Epsilon),
                    $"Compiled sample {i} was inaccurate.");
            }
        }
    }
}
