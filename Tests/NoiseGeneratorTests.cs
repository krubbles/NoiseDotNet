using System.Runtime.InteropServices;
using NoiseDotNet;

namespace Tests
{
    public class NoiseGeneratorTests
    {
        const float Epsilon = 0.00001f;
        const int Seed = 37;

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
        public void PerlinTests()
        {
            NoiseScalar perlin2D = NoiseGraph.Perlin(NoiseGraph.XY);
            AssertNode(
                perlin2D,
                NoiseNodeType.Perlin2D_noise__x_y__noise,
                NoiseGraph.XY.X,
                NoiseGraph.XY.Y);

            float[][] actual2D = Evaluate(NoiseGraphByteCodeCompiler.Compile(perlin2D));
            float[] expected2D = new float[XCoordinates.Length];
            Noise.GradientNoise2D(
                XCoordinates,
                YCoordinates,
                expected2D,
                new NoiseSettings(xFreq: 1f, yFreq: 1f, seed: Seed));
            AssertOutputsEqual(expected2D, actual2D[0]);

            NoiseScalar perlin3D = NoiseGraph.Perlin(NoiseGraph.XYZ);
            AssertNode(
                perlin3D,
                NoiseNodeType.Perlin3D_noise__x_y_z__noise,
                NoiseGraph.XYZ.X,
                NoiseGraph.XYZ.Y,
                NoiseGraph.XYZ.Z);

            float[][] actual3D = Evaluate(NoiseGraphByteCodeCompiler.Compile(perlin3D));
            float[] expected3D = new float[XCoordinates.Length];
            Noise.GradientNoise3D(
                XCoordinates,
                YCoordinates,
                ZCoordinates,
                expected3D,
                new NoiseSettings(xFreq: 1f, yFreq: 1f, zFreq: 1f, seed: Seed));
            AssertOutputsEqual(expected3D, actual3D[0]);
        }

        [Test]
        public void CellularTests()
        {
            (NoiseScalar centerDist, NoiseScalar edgeDist) cellular2D =
                NoiseGraph.Cellular(NoiseGraph.XY);
            Assert.That(cellular2D.centerDist.Node, Is.SameAs(cellular2D.edgeDist.Node));
            AssertNode(
                cellular2D.centerDist,
                NoiseNodeType.Cellular2_noise__x_y__center_edge,
                NoiseGraph.XY.X,
                NoiseGraph.XY.Y);
            Assert.That(cellular2D.centerDist.ChannelIndex, Is.Zero);
            Assert.That(cellular2D.edgeDist.ChannelIndex, Is.EqualTo(1));

            float[][] actual2D = Evaluate(NoiseGraphByteCodeCompiler.Compile(
                cellular2D.centerDist,
                cellular2D.edgeDist));
            float[] expectedCenter2D = new float[XCoordinates.Length];
            float[] expectedEdge2D = new float[XCoordinates.Length];
            Noise.CellularNoise2D(
                XCoordinates,
                YCoordinates,
                expectedCenter2D,
                expectedEdge2D,
                new NoiseSettings(xFreq: 1f, yFreq: 1f, seed: Seed));
            AssertOutputsEqual(expectedCenter2D, actual2D[0]);
            AssertOutputsEqual(expectedEdge2D, actual2D[1]);

            (NoiseScalar centerDist, NoiseScalar edgeDist) cellular3D =
                NoiseGraph.Cellular(NoiseGraph.XYZ);
            Assert.That(cellular3D.centerDist.Node, Is.SameAs(cellular3D.edgeDist.Node));
            AssertNode(
                cellular3D.centerDist,
                NoiseNodeType.Cellular3_noise__x_y_z__center_edge,
                NoiseGraph.XYZ.X,
                NoiseGraph.XYZ.Y,
                NoiseGraph.XYZ.Z);
            Assert.That(cellular3D.centerDist.ChannelIndex, Is.Zero);
            Assert.That(cellular3D.edgeDist.ChannelIndex, Is.EqualTo(1));

            float[][] actual3D = Evaluate(NoiseGraphByteCodeCompiler.Compile(
                cellular3D.centerDist,
                cellular3D.edgeDist));
            float[] expectedCenter3D = new float[XCoordinates.Length];
            float[] expectedEdge3D = new float[XCoordinates.Length];
            Noise.CellularNoise3D(
                XCoordinates,
                YCoordinates,
                ZCoordinates,
                expectedCenter3D,
                expectedEdge3D,
                new NoiseSettings(xFreq: 1f, yFreq: 1f, zFreq: 1f, seed: Seed));
            AssertOutputsEqual(expectedCenter3D, actual3D[0]);
            AssertOutputsEqual(expectedEdge3D, actual3D[1]);
        }

        static void AssertNode(
            NoiseScalar actual,
            NoiseNodeType type,
            params NoiseScalar[] inputs)
        {
            Assert.That(actual.Node.Type, Is.EqualTo(type));
            Assert.That(actual.Node.Inputs.ToArray(), Is.EqualTo(inputs));
        }

        static void AssertOutputsEqual(float[] expected, float[] actual)
        {
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(
                    actual[i],
                    Is.EqualTo(expected[i]).Within(Epsilon),
                    $"Compiled sample {i} was inaccurate.");
            }
        }

        static float[][] Evaluate(NoiseGraphByteCode compiled)
        {
            ByteCodeInfo info = MemoryMarshal.Read<ByteCodeInfo>(compiled.ByteCode);
            int batchSize = XCoordinates.Length;
            float[] registers = new float[checked(info.RegisterCount * batchSize)];
            XCoordinates.CopyTo(registers, 0);
            YCoordinates.CopyTo(registers, batchSize);
            if (info.InputCount == 3)
                ZCoordinates.CopyTo(registers, batchSize * 2);

            NoiseGraphByteCodeEval.EvaluateByteCode(compiled.ByteCode, Seed, registers, batchSize);

            float[][] outputs = new float[info.OutputCount][];
            for (int outputIndex = 0; outputIndex < outputs.Length; outputIndex++)
                outputs[outputIndex] = registers.AsSpan(outputIndex * batchSize, batchSize).ToArray();
            return outputs;
        }
    }
}
