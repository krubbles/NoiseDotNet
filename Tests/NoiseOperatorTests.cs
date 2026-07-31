using System.Runtime.InteropServices;
using NoiseDotNet;

namespace Tests
{
    // Each test covers one NoiseNode operator type. It verifies every direct public API for
    // that operator creates correctly typed and wired nodes, then compiles a simple graph and
    // compares its output with an independent numerical reference implementation.
    public class NoiseOperatorTests
    {
        const float Epsilon = 0.00001f;

        static readonly float[] XCoordinates =
        [
            -3.25f, -1.75f, -0.4f, 0.15f, 0.8f, 1.35f, 3.65f,
        ];

        [Test]
        public void AddTests()
        {
            NoiseScalar scalarA = NoiseGraph.X;
            NoiseScalar scalarB = NoiseGraph.Constant(2.5f);
            NoiseVector2 vector2A = NoiseGraph.XY;
            NoiseVector2 vector2B = NoiseGraph.Constant(2.5f, -1.25f);
            NoiseVector3 vector3A = NoiseGraph.XYZ;
            NoiseVector3 vector3B = NoiseGraph.Constant(2.5f, -1.25f, 0.75f);

            AssertBinaryNode(NoiseGraph.Add(scalarA, scalarB), NoiseNodeType.Add__a_b__sum, scalarA, scalarB);
            AssertBinaryNode(scalarA + scalarB, NoiseNodeType.Add__a_b__sum, scalarA, scalarB);
            AssertBinaryNodes(NoiseGraph.Add(vector2A, vector2B), NoiseNodeType.Add__a_b__sum, vector2A, vector2B);
            AssertBinaryNodes(vector2A + vector2B, NoiseNodeType.Add__a_b__sum, vector2A, vector2B);
            AssertBinaryNodes(NoiseGraph.Add(vector3A, vector3B), NoiseNodeType.Add__a_b__sum, vector3A, vector3B);
            AssertBinaryNodes(vector3A + vector3B, NoiseNodeType.Add__a_b__sum, vector3A, vector3B);

            AssertAddIdentityOptimizations(scalarA, vector2A, vector3A);
            AssertCompilesAccurately(NoiseGraph.Add(scalarA, scalarB), x => x + 2.5f);
        }

        static void AssertAddIdentityOptimizations(
            NoiseScalar scalar,
            NoiseVector2 vector2,
            NoiseVector3 vector3)
        {
            Assert.That(NoiseGraph.Add(scalar, 0f), Is.EqualTo(scalar));
            Assert.That(NoiseGraph.Add(0f, scalar), Is.EqualTo(scalar));
            Assert.That(scalar + 0f, Is.EqualTo(scalar));
            Assert.That(0f + scalar, Is.EqualTo(scalar));

            NoiseVector2 offset = (0f, 2f);
            NoiseVector2 vector2ByComponents = NoiseGraph.Add(vector2, offset);
            Assert.That(vector2ByComponents.X, Is.EqualTo(vector2.X));
            AssertBinaryNode(
                vector2ByComponents.Y,
                NoiseNodeType.Add__a_b__sum,
                vector2.Y,
                offset.Y);

            NoiseVector3 unchangedVector3 = vector3 + (0f, 0f, 0f);
            Assert.That(unchangedVector3.X, Is.EqualTo(vector3.X));
            Assert.That(unchangedVector3.Y, Is.EqualTo(vector3.Y));
            Assert.That(unchangedVector3.Z, Is.EqualTo(vector3.Z));
        }

        [Test]
        public void NegateTests()
        {
            NoiseScalar scalar = NoiseGraph.X;
            NoiseVector2 vector2 = NoiseGraph.XY;
            NoiseVector3 vector3 = NoiseGraph.XYZ;

            AssertUnaryNode(NoiseGraph.Negate(scalar), NoiseNodeType.Negate__value__negated, scalar);
            AssertUnaryNode(-scalar, NoiseNodeType.Negate__value__negated, scalar);
            AssertUnaryNodes(NoiseGraph.Negate(vector2), NoiseNodeType.Negate__value__negated, vector2);
            AssertUnaryNodes(-vector2, NoiseNodeType.Negate__value__negated, vector2);
            AssertUnaryNodes(NoiseGraph.Negate(vector3), NoiseNodeType.Negate__value__negated, vector3);
            AssertUnaryNodes(-vector3, NoiseNodeType.Negate__value__negated, vector3);

            AssertCompilesAccurately(NoiseGraph.Negate(scalar), x => -x);
        }

        [Test]
        public void MultiplyTests()
        {
            NoiseScalar scalarA = NoiseGraph.X;
            NoiseScalar scalarB = NoiseGraph.Constant(-1.5f);
            NoiseVector2 vector2A = NoiseGraph.XY;
            NoiseVector2 vector2B = NoiseGraph.Constant(2.5f, -1.25f);
            NoiseVector3 vector3A = NoiseGraph.XYZ;
            NoiseVector3 vector3B = NoiseGraph.Constant(2.5f, -1.25f, 0.75f);

            AssertBinaryNode(NoiseGraph.Multiply(scalarA, scalarB), NoiseNodeType.Multiply__a_b__product, scalarA, scalarB);
            AssertBinaryNode(scalarA * scalarB, NoiseNodeType.Multiply__a_b__product, scalarA, scalarB);
            AssertBinaryNodes(NoiseGraph.Multiply(vector2A, vector2B), NoiseNodeType.Multiply__a_b__product, vector2A, vector2B);
            AssertBinaryNodes(vector2A * vector2B, NoiseNodeType.Multiply__a_b__product, vector2A, vector2B);
            AssertBinaryNodes(NoiseGraph.Multiply(vector3A, vector3B), NoiseNodeType.Multiply__a_b__product, vector3A, vector3B);
            AssertBinaryNodes(vector3A * vector3B, NoiseNodeType.Multiply__a_b__product, vector3A, vector3B);

            AssertVectorScalarMultiply(NoiseGraph.Multiply(vector2A, scalarB), vector2A, scalarB);
            AssertVectorScalarMultiply(vector2A * scalarB, vector2A, scalarB);
            AssertVectorScalarMultiply(scalarB * vector2A, vector2A, scalarB);
            AssertVectorScalarMultiply(NoiseGraph.Multiply(vector3A, scalarB), vector3A, scalarB);
            AssertVectorScalarMultiply(vector3A * scalarB, vector3A, scalarB);
            AssertVectorScalarMultiply(scalarB * vector3A, vector3A, scalarB);

            AssertMultiplyIdentityOptimizations(scalarA, vector2A, vector3A);
            AssertCompilesAccurately(NoiseGraph.Multiply(scalarA, scalarB), x => x * -1.5f);
        }

        static void AssertMultiplyIdentityOptimizations(
            NoiseScalar scalar,
            NoiseVector2 vector2,
            NoiseVector3 vector3)
        {
            Assert.That(NoiseGraph.Multiply(scalar, 0f).Node, Is.SameAs(NoiseGraph.Zero.Node));
            Assert.That(NoiseGraph.Multiply(0f, scalar).Node, Is.SameAs(NoiseGraph.Zero.Node));
            Assert.That((scalar * 0f).Node, Is.SameAs(NoiseGraph.Zero.Node));
            Assert.That((0f * scalar).Node, Is.SameAs(NoiseGraph.Zero.Node));

            Assert.That(NoiseGraph.Multiply(scalar, 1f), Is.EqualTo(scalar));
            Assert.That(NoiseGraph.Multiply(1f, scalar), Is.EqualTo(scalar));
            Assert.That(scalar * 1f, Is.EqualTo(scalar));
            Assert.That(1f * scalar, Is.EqualTo(scalar));

            NoiseVector2 vector2ByComponents = NoiseGraph.Multiply(vector2, (0f, 1f));
            Assert.That(vector2ByComponents.X.Node, Is.SameAs(NoiseGraph.Zero.Node));
            Assert.That(vector2ByComponents.Y, Is.EqualTo(vector2.Y));

            NoiseVector3 vector3ByComponents = NoiseGraph.Multiply(vector3, (1f, 0f, 1f));
            Assert.That(vector3ByComponents.X, Is.EqualTo(vector3.X));
            Assert.That(vector3ByComponents.Y.Node, Is.SameAs(NoiseGraph.Zero.Node));
            Assert.That(vector3ByComponents.Z, Is.EqualTo(vector3.Z));

            NoiseVector2 zeroVector2 = vector2 * 0f;
            Assert.That(zeroVector2.X.Node, Is.SameAs(NoiseGraph.Zero.Node));
            Assert.That(zeroVector2.Y.Node, Is.SameAs(NoiseGraph.Zero.Node));

            NoiseVector3 unchangedVector3 = 1f * vector3;
            Assert.That(unchangedVector3.X, Is.EqualTo(vector3.X));
            Assert.That(unchangedVector3.Y, Is.EqualTo(vector3.Y));
            Assert.That(unchangedVector3.Z, Is.EqualTo(vector3.Z));
        }

        static void AssertVectorScalarMultiply(
            NoiseVector2 actual,
            NoiseVector2 vector,
            NoiseScalar scalar)
        {
            AssertBinaryNode(actual.X, NoiseNodeType.Multiply__a_b__product, vector.X, scalar);
            AssertBinaryNode(actual.Y, NoiseNodeType.Multiply__a_b__product, vector.Y, scalar);
        }

        static void AssertVectorScalarMultiply(
            NoiseVector3 actual,
            NoiseVector3 vector,
            NoiseScalar scalar)
        {
            AssertBinaryNode(actual.X, NoiseNodeType.Multiply__a_b__product, vector.X, scalar);
            AssertBinaryNode(actual.Y, NoiseNodeType.Multiply__a_b__product, vector.Y, scalar);
            AssertBinaryNode(actual.Z, NoiseNodeType.Multiply__a_b__product, vector.Z, scalar);
        }

        [Test]
        public void InverseTests()
        {
            NoiseScalar scalar = NoiseGraph.X + NoiseGraph.Constant(4f);
            NoiseVector2 vector2 = NoiseGraph.XY;
            NoiseVector3 vector3 = NoiseGraph.XYZ;

            AssertUnaryNode(NoiseGraph.Inverse(scalar), NoiseNodeType.Inverse__value__inverse, scalar);
            AssertUnaryNodes(NoiseGraph.Inverse(vector2), NoiseNodeType.Inverse__value__inverse, vector2);
            AssertUnaryNodes(NoiseGraph.Inverse(vector3), NoiseNodeType.Inverse__value__inverse, vector3);

            AssertCompilesAccurately(NoiseGraph.Inverse(scalar), x => 1f / (x + 4f));
        }

        [Test]
        public void MinTests()
        {
            NoiseScalar scalarA = NoiseGraph.X;
            NoiseScalar scalarB = NoiseGraph.Constant(0.5f);
            NoiseVector2 vector2A = NoiseGraph.XY;
            NoiseVector2 vector2B = NoiseGraph.Constant(0.5f, -0.25f);
            NoiseVector3 vector3A = NoiseGraph.XYZ;
            NoiseVector3 vector3B = NoiseGraph.Constant(0.5f, -0.25f, 1.25f);

            AssertBinaryNode(NoiseGraph.Min(scalarA, scalarB), NoiseNodeType.Min__a_b__min, scalarA, scalarB);
            AssertBinaryNodes(NoiseGraph.Min(vector2A, vector2B), NoiseNodeType.Min__a_b__min, vector2A, vector2B);
            AssertBinaryNodes(NoiseGraph.Min(vector3A, vector3B), NoiseNodeType.Min__a_b__min, vector3A, vector3B);

            AssertCompilesAccurately(NoiseGraph.Min(scalarA, scalarB), x => MathF.Min(x, 0.5f));
        }

        [Test]
        public void MaxTests()
        {
            NoiseScalar scalarA = NoiseGraph.X;
            NoiseScalar scalarB = NoiseGraph.Constant(-0.5f);
            NoiseVector2 vector2A = NoiseGraph.XY;
            NoiseVector2 vector2B = NoiseGraph.Constant(-0.5f, 0.25f);
            NoiseVector3 vector3A = NoiseGraph.XYZ;
            NoiseVector3 vector3B = NoiseGraph.Constant(-0.5f, 0.25f, -1.25f);

            AssertBinaryNode(NoiseGraph.Max(scalarA, scalarB), NoiseNodeType.Max__a_b__max, scalarA, scalarB);
            AssertBinaryNodes(NoiseGraph.Max(vector2A, vector2B), NoiseNodeType.Max__a_b__max, vector2A, vector2B);
            AssertBinaryNodes(NoiseGraph.Max(vector3A, vector3B), NoiseNodeType.Max__a_b__max, vector3A, vector3B);

            AssertCompilesAccurately(NoiseGraph.Max(scalarA, scalarB), x => MathF.Max(x, -0.5f));
        }

        [Test]
        public void PowTests()
        {
            NoiseScalar scalarValue = NoiseGraph.X + NoiseGraph.Constant(4f);
            NoiseScalar scalarPower = NoiseGraph.Constant(2.5f);
            NoiseVector2 vector2Value = NoiseGraph.XY;
            NoiseVector2 vector2Power = NoiseGraph.Constant(2f, 3f);
            NoiseVector3 vector3Value = NoiseGraph.XYZ;
            NoiseVector3 vector3Power = NoiseGraph.Constant(2f, 3f, 4f);

            AssertBinaryNode(
                NoiseGraph.Pow(scalarValue, scalarPower),
                NoiseNodeType.Pow__value_power__result,
                scalarValue,
                scalarPower);
            AssertBinaryNodes(
                NoiseGraph.Pow(vector2Value, vector2Power),
                NoiseNodeType.Pow__value_power__result,
                vector2Value,
                vector2Power);
            AssertBinaryNodes(
                NoiseGraph.Pow(vector3Value, vector3Power),
                NoiseNodeType.Pow__value_power__result,
                vector3Value,
                vector3Power);
            AssertVectorScalarPow(NoiseGraph.Pow(vector2Value, scalarPower), vector2Value, scalarPower);
            AssertVectorScalarPow(NoiseGraph.Pow(vector3Value, scalarPower), vector3Value, scalarPower);

            AssertCompilesAccurately(
                NoiseGraph.Pow(scalarValue, scalarPower),
                x => MathF.Pow(x + 4f, 2.5f));
        }

        static void AssertVectorScalarPow(NoiseVector2 actual, NoiseVector2 value, NoiseScalar power)
        {
            AssertBinaryNode(actual.X, NoiseNodeType.Pow__value_power__result, value.X, power);
            AssertBinaryNode(actual.Y, NoiseNodeType.Pow__value_power__result, value.Y, power);
        }

        static void AssertVectorScalarPow(NoiseVector3 actual, NoiseVector3 value, NoiseScalar power)
        {
            AssertBinaryNode(actual.X, NoiseNodeType.Pow__value_power__result, value.X, power);
            AssertBinaryNode(actual.Y, NoiseNodeType.Pow__value_power__result, value.Y, power);
            AssertBinaryNode(actual.Z, NoiseNodeType.Pow__value_power__result, value.Z, power);
        }

        [Test]
        public void SmoothStepTests()
        {
            NoiseScalar scalar = NoiseGraph.X;
            NoiseVector2 vector2 = NoiseGraph.XY;
            NoiseVector3 vector3 = NoiseGraph.XYZ;

            AssertUnaryNode(NoiseGraph.SmoothStep(scalar), NoiseNodeType.SmoothStep01__value__result, scalar);
            AssertUnaryNodes(NoiseGraph.SmoothStep(vector2), NoiseNodeType.SmoothStep01__value__result, vector2);
            AssertUnaryNodes(NoiseGraph.SmoothStep(vector3), NoiseNodeType.SmoothStep01__value__result, vector3);

            AssertCompilesAccurately(NoiseGraph.SmoothStep(scalar), SmoothStepReference);
        }

        [Test]
        public void LerpTests()
        {
            NoiseScalar scalarA = NoiseGraph.Constant(-2f);
            NoiseScalar scalarB = NoiseGraph.Constant(6f);
            NoiseScalar scalarT = NoiseGraph.X;
            NoiseVector2 vector2A = NoiseGraph.XY;
            NoiseVector2 vector2B = NoiseGraph.Constant(2f, 3f);
            NoiseVector2 vector2T = NoiseGraph.Constant(0.25f, 0.75f);
            NoiseVector3 vector3A = NoiseGraph.XYZ;
            NoiseVector3 vector3B = NoiseGraph.Constant(2f, 3f, 4f);
            NoiseVector3 vector3T = NoiseGraph.Constant(0.25f, 0.5f, 0.75f);

            AssertTernaryNode(
                NoiseGraph.Lerp(scalarA, scalarB, scalarT),
                NoiseNodeType.Lerp__a_b_t__result,
                scalarA,
                scalarB,
                scalarT);
            AssertVectorScalarLerp(NoiseGraph.Lerp(vector2A, vector2B, scalarT), vector2A, vector2B, scalarT);
            AssertVectorScalarLerp(NoiseGraph.Lerp(vector3A, vector3B, scalarT), vector3A, vector3B, scalarT);
            AssertTernaryNodes(
                NoiseGraph.Lerp(vector2A, vector2B, vector2T),
                NoiseNodeType.Lerp__a_b_t__result,
                vector2A,
                vector2B,
                vector2T);
            AssertTernaryNodes(
                NoiseGraph.Lerp(vector3A, vector3B, vector3T),
                NoiseNodeType.Lerp__a_b_t__result,
                vector3A,
                vector3B,
                vector3T);

            AssertCompilesAccurately(
                NoiseGraph.Lerp(scalarA, scalarB, scalarT),
                x => -2f + (6f - -2f) * x);
        }

        static void AssertTernaryNode(
            NoiseScalar actual,
            NoiseNodeType type,
            NoiseScalar a,
            NoiseScalar b,
            NoiseScalar t)
        {
            Assert.That(actual.Node.Type, Is.EqualTo(type));
            Assert.That(actual.ChannelIndex, Is.Zero);
            Assert.That(actual.Node.Inputs.ToArray(), Is.EqualTo(new[] { a, b, t }));
        }

        static void AssertTernaryNodes(
            NoiseVector2 actual,
            NoiseNodeType type,
            NoiseVector2 a,
            NoiseVector2 b,
            NoiseVector2 t)
        {
            AssertTernaryNode(actual.X, type, a.X, b.X, t.X);
            AssertTernaryNode(actual.Y, type, a.Y, b.Y, t.Y);
        }

        static void AssertTernaryNodes(
            NoiseVector3 actual,
            NoiseNodeType type,
            NoiseVector3 a,
            NoiseVector3 b,
            NoiseVector3 t)
        {
            AssertTernaryNode(actual.X, type, a.X, b.X, t.X);
            AssertTernaryNode(actual.Y, type, a.Y, b.Y, t.Y);
            AssertTernaryNode(actual.Z, type, a.Z, b.Z, t.Z);
        }

        static void AssertVectorScalarLerp(
            NoiseVector2 actual,
            NoiseVector2 a,
            NoiseVector2 b,
            NoiseScalar t)
        {
            AssertTernaryNode(actual.X, NoiseNodeType.Lerp__a_b_t__result, a.X, b.X, t);
            AssertTernaryNode(actual.Y, NoiseNodeType.Lerp__a_b_t__result, a.Y, b.Y, t);
        }

        static void AssertVectorScalarLerp(
            NoiseVector3 actual,
            NoiseVector3 a,
            NoiseVector3 b,
            NoiseScalar t)
        {
            AssertTernaryNode(actual.X, NoiseNodeType.Lerp__a_b_t__result, a.X, b.X, t);
            AssertTernaryNode(actual.Y, NoiseNodeType.Lerp__a_b_t__result, a.Y, b.Y, t);
            AssertTernaryNode(actual.Z, NoiseNodeType.Lerp__a_b_t__result, a.Z, b.Z, t);
        }

        [Test]
        public void FloorTests()
        {
            NoiseScalar scalar = NoiseGraph.X;
            NoiseVector2 vector2 = NoiseGraph.XY;
            NoiseVector3 vector3 = NoiseGraph.XYZ;

            AssertUnaryNode(NoiseGraph.Floor(scalar), NoiseNodeType.Floor__value__result, scalar);
            AssertUnaryNodes(NoiseGraph.Floor(vector2), NoiseNodeType.Floor__value__result, vector2);
            AssertUnaryNodes(NoiseGraph.Floor(vector3), NoiseNodeType.Floor__value__result, vector3);

            AssertCompilesAccurately(NoiseGraph.Floor(scalar), MathF.Floor);
        }

        static void AssertUnaryNode(NoiseScalar actual, NoiseNodeType type, NoiseScalar value)
        {
            Assert.That(actual.Node.Type, Is.EqualTo(type));
            Assert.That(actual.ChannelIndex, Is.Zero);
            Assert.That(actual.Node.Inputs.ToArray(), Is.EqualTo(new[] { value }));
        }

        static void AssertBinaryNode(
            NoiseScalar actual,
            NoiseNodeType type,
            NoiseScalar a,
            NoiseScalar b)
        {
            Assert.That(actual.Node.Type, Is.EqualTo(type));
            Assert.That(actual.ChannelIndex, Is.Zero);
            Assert.That(actual.Node.Inputs.ToArray(), Is.EqualTo(new[] { a, b }));
        }

        static void AssertUnaryNodes(NoiseVector2 actual, NoiseNodeType type, NoiseVector2 value)
        {
            AssertUnaryNode(actual.X, type, value.X);
            AssertUnaryNode(actual.Y, type, value.Y);
        }

        static void AssertUnaryNodes(NoiseVector3 actual, NoiseNodeType type, NoiseVector3 value)
        {
            AssertUnaryNode(actual.X, type, value.X);
            AssertUnaryNode(actual.Y, type, value.Y);
            AssertUnaryNode(actual.Z, type, value.Z);
        }

        static void AssertBinaryNodes(
            NoiseVector2 actual,
            NoiseNodeType type,
            NoiseVector2 a,
            NoiseVector2 b)
        {
            AssertBinaryNode(actual.X, type, a.X, b.X);
            AssertBinaryNode(actual.Y, type, a.Y, b.Y);
        }

        static void AssertBinaryNodes(
            NoiseVector3 actual,
            NoiseNodeType type,
            NoiseVector3 a,
            NoiseVector3 b)
        {
            AssertBinaryNode(actual.X, type, a.X, b.X);
            AssertBinaryNode(actual.Y, type, a.Y, b.Y);
            AssertBinaryNode(actual.Z, type, a.Z, b.Z);
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

        static float SmoothStepReference(float value)
        {
            float clamped = Math.Clamp(value, 0f, 1f);
            return clamped * clamped * (3f - 2f * clamped);
        }
    }
}
