namespace Tests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        static bool EqualEnough(float a, float b) => MathF.Abs(a - b) < 0.00001f;

        [Test]
        public void GradientNoise2DContinuity()
        {
            Random random = new(1);
            int testCount = 1000000;
            float deltaLength = 0.01f;
            float[] xBuffer = new float[testCount];
            float[] yBuffer = new float[testCount];
            for (int i = 0; i < testCount; i += 2)
            {
                xBuffer[i] = (float)random.NextSingle() * 100f;
                yBuffer[i] = (float)random.NextSingle() * 100f;
                xBuffer[i + 1] = xBuffer[i] + deltaLength * random.NextSingle();
                yBuffer[i + 1] = yBuffer[i] + deltaLength * random.NextSingle();
            }
            float[] outputBuffer = new float[testCount];
            NoiseDotNet.Noise.GradientNoise2D(xBuffer, yBuffer, outputBuffer, 1f, 1f, 1f, 1);
            bool continuous = true;
            float averageDelta = 0;
            for (int i = 0; i < testCount; i += 2)
            {
                float delta = MathF.Abs(outputBuffer[i] - outputBuffer[i + 1]);
                averageDelta += delta;
                if (delta > 0.04f) // should not be possible
                    continuous = false;
            }
            averageDelta /= testCount;
            Assert.That(averageDelta > 0.001f, "GradientNoise2D is too flat");
            Assert.That(continuous, "GradientNoise2D is not continuous");

            float[] secondOutputBuffer = new float[15];
            NoiseDotNet.Noise.GradientNoise2D(xBuffer.AsSpan()[0..15], yBuffer.AsSpan()[0..15], secondOutputBuffer, 1f, 1f, 1f, 1);

            bool bufferSizeDoesNotMatter = true;
            for (int i = 0; i < 15; ++i)
            {
                bufferSizeDoesNotMatter &= EqualEnough(outputBuffer[i], secondOutputBuffer[i]);
            }
            Assert.That(bufferSizeDoesNotMatter, "GradientNoise2D does not have consistent behavior across different buffer sizes");

            NoiseDotNet.Noise.GradientNoise2D(xBuffer.AsSpan()[0..15], yBuffer.AsSpan()[0..15], secondOutputBuffer, 1f, 1f, 3f, 1);
            bool amplitudeWorks = true;
            for (int i = 0; i < 15; ++i)
            {
                amplitudeWorks &= EqualEnough(outputBuffer[i] * 3f, secondOutputBuffer[i]);
            }
            Assert.That(amplitudeWorks, "GradientNoise2D amplitude does not work correctly");

            for (int i = 0; i < 15; ++i)
            {
                xBuffer[i] = xBuffer[i] * 3f;
                yBuffer[i] = yBuffer[i] * 4f;
            }

            NoiseDotNet.Noise.GradientNoise2D(xBuffer.AsSpan()[0..15], yBuffer.AsSpan()[0..15], secondOutputBuffer, 1f / 3f, 1f / 4f, 1f, 1);
            bool frequencyWorks = true;
            for (int i = 0; i < 15; ++i)
            {
                frequencyWorks &= EqualEnough(outputBuffer[i], secondOutputBuffer[i]);
            }
            Assert.That(frequencyWorks, "GradientNoise2D frequency does not work correctly");
        }

        [Test]
        public void GradientNoise2DSizeIrrelevance()
        {
            int count = 16;

            GenerateXYZBuffers(count, out float[] xBuffer, out float[] yBuffer, out float[] zBuffer);
            float[] outputBuffer = new float[count];
            NoiseDotNet.Noise.GradientNoise2D(xBuffer, yBuffer, outputBuffer, 1.5f, 1.8f, 0.5f, 1);

            count = 15;
            float[] secondOutputBuffer = new float[count];
            NoiseDotNet.Noise.GradientNoise2D(xBuffer.AsSpan()[0..count], yBuffer.AsSpan()[0..count], secondOutputBuffer, 1.5f, 1.8f, 0.5f, 1);

            bool bufferSizeDoesNotMatter = true;
            for (int i = 0; i < count; ++i)
                bufferSizeDoesNotMatter &= EqualEnough(outputBuffer[i], secondOutputBuffer[i]);

            count = 7;
            secondOutputBuffer = new float[count];
            NoiseDotNet.Noise.GradientNoise2D(xBuffer.AsSpan()[0..count], yBuffer.AsSpan()[0..count], secondOutputBuffer, 1.5f, 1.8f, 0.5f, 1);

            for (int i = 0; i < count; ++i)
                bufferSizeDoesNotMatter &= EqualEnough(outputBuffer[i], secondOutputBuffer[i]);

            Assert.That(bufferSizeDoesNotMatter, "GradientNoise2D does not have consistent behavior across different buffer sizes");
        }

        [Test]
        public void GradientNoise2DExtraParameters()
        {
            for (int count = 7; count < 16; count += 8)
            {
                GenerateXYZBuffers(count, out float[] xBuffer, out float[] yBuffer, out _);

                float[] outputBuffer = new float[count];
                NoiseDotNet.Noise.GradientNoise2D(xBuffer, yBuffer, outputBuffer, 1f, 1f, 1f, 1);

                float[] secondOutputBuffer = new float[count];
                NoiseDotNet.Noise.GradientNoise2D(xBuffer, yBuffer, secondOutputBuffer, 1f, 1f, 3f, 1);

                bool amplitudeWorks = true;
                for (int i = 0; i < count; ++i)
                    amplitudeWorks &= EqualEnough(outputBuffer[i] * 3f, secondOutputBuffer[i]);
                Assert.That(amplitudeWorks, "GradientNoise2D amplitude does not work correctly");

                for (int i = 0; i < count; ++i)
                {
                    xBuffer[i] = xBuffer[i] * 3f;
                    yBuffer[i] = yBuffer[i] * 4f;
                }

                NoiseDotNet.Noise.GradientNoise2D(xBuffer, yBuffer, secondOutputBuffer, 1f / 3f, 1f / 4f, 1f, 1);

                bool frequencyWorks = true;
                for (int i = 0; i < count; ++i)
                    frequencyWorks &= EqualEnough(outputBuffer[i], secondOutputBuffer[i]);
                Assert.That(frequencyWorks, "GradientNoise2D frequency does not work correctly");

                NoiseDotNet.Noise.GradientNoise2D(xBuffer, yBuffer, secondOutputBuffer, 1f / 3f, 1f / 4f, 1f, 2);

                bool seedWorks = false;
                for (int i = 0; i < count; ++i)
                    seedWorks |= !EqualEnough(outputBuffer[i], secondOutputBuffer[i]);
                Assert.That(seedWorks, "GradientNoise2D seed does not work correctly");
            }
        }

        [Test]
        public void CellularNoise2DSizeIrrelevance()
        {
            int count = 16;

            GenerateXYZBuffers(count, out float[] xBuffer, out float[] yBuffer, out _);
            float[] outputBufferA = new float[count], outputBufferB = new float[count];
            NoiseDotNet.Noise.CellularNoise2D(xBuffer, yBuffer, outputBufferA, outputBufferB, 1.5f, 1.8f, 0.5f, 0.8f, 1);

            count = 15;
            float[] secondOutputBufferA = new float[count], secondOutputBufferB = new float[count];
            NoiseDotNet.Noise.CellularNoise2D(xBuffer, yBuffer, secondOutputBufferA, secondOutputBufferB, 1.5f, 1.8f, 0.5f, 0.8f, 1);

            bool bufferSizeDoesNotMatter = true;
            for (int i = 0; i < count; ++i)
            {
                bufferSizeDoesNotMatter &= EqualEnough(outputBufferA[i], secondOutputBufferA[i]);
                bufferSizeDoesNotMatter &= EqualEnough(outputBufferB[i], secondOutputBufferB[i]);
            }

            count = 7;
            secondOutputBufferA = new float[count];
            secondOutputBufferB = new float[count];
            NoiseDotNet.Noise.CellularNoise2D(xBuffer, yBuffer, secondOutputBufferA, secondOutputBufferB, 1.5f, 1.8f, 0.5f, 0.8f, 1);

            for (int i = 0; i < count; ++i)
            {
                bufferSizeDoesNotMatter &= EqualEnough(outputBufferA[i], secondOutputBufferA[i]);
                bufferSizeDoesNotMatter &= EqualEnough(outputBufferB[i], secondOutputBufferB[i]);
            }

            Assert.That(bufferSizeDoesNotMatter, "CellularNoise2D does not have consistent behavior across different buffer sizes");
        }

        [Test]
        public void CellularNoise2DExtraParameters()
        {
            for (int count = 7; count < 16; count += 8)
            {
                GenerateXYZBuffers(count, out float[] xBuffer, out float[] yBuffer, out float[] zBuffer);

                float[] outputBufferA = new float[count], outputBufferB = new float[count];
                NoiseDotNet.Noise.CellularNoise2D(xBuffer, yBuffer, outputBufferA, outputBufferB, 1f, 1f, 1f, 1f, 1);

                float[] secondOutputBufferA = new float[count], secondOutputBufferB = new float[count];
                NoiseDotNet.Noise.CellularNoise2D(xBuffer, yBuffer, secondOutputBufferA, secondOutputBufferB, 1f, 1f, 3f, 4f, 1);

                bool amplitudeWorks = true;
                for (int i = 0; i < count; ++i)
                {
                    amplitudeWorks &= EqualEnough(outputBufferA[i] * 3f, secondOutputBufferA[i]);
                    amplitudeWorks &= EqualEnough(outputBufferB[i] * 4f, secondOutputBufferB[i]);
                }
                Assert.That(amplitudeWorks, "CellularNoise2D amplitude does not work correctly");

                for (int i = 0; i < count; ++i)
                {
                    xBuffer[i] = xBuffer[i] * 3f;
                    yBuffer[i] = yBuffer[i] * 4f;
                }

                NoiseDotNet.Noise.CellularNoise2D(xBuffer, yBuffer, secondOutputBufferA, secondOutputBufferB, 1f / 3f, 1f / 4f, 1f, 1f, 1);

                bool frequencyWorks = true;
                for (int i = 0; i < count; ++i)
                {
                    frequencyWorks &= EqualEnough(outputBufferA[i], secondOutputBufferA[i]);
                    frequencyWorks &= EqualEnough(outputBufferB[i], secondOutputBufferB[i]);
                }
                Assert.That(frequencyWorks, "CellularNoise2D frequency does not work correctly");

                NoiseDotNet.Noise.CellularNoise2D(xBuffer, yBuffer, secondOutputBufferA, secondOutputBufferB, 1f / 3f, 1f / 4f, 1f, 1f, 2);

                bool seedWorks = false;
                for (int i = 0; i < count; ++i)
                    seedWorks |= !EqualEnough(outputBufferA[i], secondOutputBufferA[i]);
                Assert.That(seedWorks, "CellularNoise2D seed does not work correctly");
            }
        }

        [Test]
        public void GradientNoise3DSizeIrrelevance()
        {
            int count = 16;

            GenerateXYZBuffers(count, out float[] xBuffer, out float[] yBuffer, out float[] zBuffer);
            float[] outputBuffer = new float[count];
            NoiseDotNet.Noise.GradientNoise3D(xBuffer, yBuffer, zBuffer, outputBuffer, 1.5f, 1.8f, 2.1f, 0.5f, 1);

            count = 15;
            float[] secondOutputBuffer = new float[count];
            NoiseDotNet.Noise.GradientNoise3D(xBuffer.AsSpan()[0..count], yBuffer.AsSpan()[0..count], zBuffer.AsSpan()[0..count], secondOutputBuffer, 1.5f, 1.8f, 2.1f, 0.5f, 1);

            bool bufferSizeDoesNotMatter = true;
            for (int i = 0; i < count; ++i)
                bufferSizeDoesNotMatter &= EqualEnough(outputBuffer[i], secondOutputBuffer[i]);

            count = 7;
            secondOutputBuffer = new float[count];
            NoiseDotNet.Noise.GradientNoise3D(xBuffer.AsSpan()[0..count], yBuffer.AsSpan()[0..count], zBuffer.AsSpan()[0..count], secondOutputBuffer, 1.5f, 1.8f, 2.1f, 0.5f, 1);

            for (int i = 0; i < count; ++i)
                bufferSizeDoesNotMatter &= EqualEnough(outputBuffer[i], secondOutputBuffer[i]);

            Assert.That(bufferSizeDoesNotMatter, "GradientNoise3D does not have consistent behavior across different buffer sizes");
        }

        [Test]
        public void GradientNoise3DExtraParameters()
        {
            for (int count = 7; count < 16; count += 8)
            {
                GenerateXYZBuffers(count, out float[] xBuffer, out float[] yBuffer, out float[] zBuffer);

                float[] outputBuffer = new float[count];
                NoiseDotNet.Noise.GradientNoise3D(xBuffer, yBuffer, zBuffer, outputBuffer, 1f, 1f, 1f, 1f, 1);

                float[] secondOutputBuffer = new float[count];
                NoiseDotNet.Noise.GradientNoise3D(xBuffer, yBuffer, zBuffer, secondOutputBuffer, 1f, 1f, 1f, 3f, 1);

                bool amplitudeWorks = true;
                for (int i = 0; i < count; ++i)
                    amplitudeWorks &= EqualEnough(outputBuffer[i] * 3f, secondOutputBuffer[i]);
                Assert.That(amplitudeWorks, "GradientNoise3D amplitude does not work correctly");

                for (int i = 0; i < count; ++i)
                {
                    xBuffer[i] = xBuffer[i] * 3f;
                    yBuffer[i] = yBuffer[i] * 4f;
                    zBuffer[i] = zBuffer[i] * 5f;
                }

                NoiseDotNet.Noise.GradientNoise3D(xBuffer, yBuffer, zBuffer, secondOutputBuffer, 1f / 3f, 1f / 4f, 1 / 5f, 1f, 1);

                bool frequencyWorks = true;
                for (int i = 0; i < count; ++i)
                    frequencyWorks &= EqualEnough(outputBuffer[i], secondOutputBuffer[i]);
                Assert.That(frequencyWorks, "GradientNoise3D frequency does not work correctly");

                NoiseDotNet.Noise.GradientNoise3D(xBuffer, yBuffer, zBuffer, secondOutputBuffer, 1f / 3f, 1f / 4f, 1 / 5f, 1f, 2);

                bool seedWorks = false;
                for (int i = 0; i < count; ++i)
                    seedWorks |= !EqualEnough(outputBuffer[i], secondOutputBuffer[i]);
                Assert.That(seedWorks, "GradientNoise3D seed does not work correctly");
            }
        }

        [Test]
        public void CellularNoise3DSizeIrrelevance()
        {
            int count = 16;

            GenerateXYZBuffers(count, out float[] xBuffer, out float[] yBuffer, out float[] zBuffer);
            float[] outputBufferA = new float[count], outputBufferB = new float[count];
            NoiseDotNet.Noise.CellularNoise3D(xBuffer, yBuffer, zBuffer, outputBufferA, outputBufferB, 1.5f, 1.8f, 2.1f, 0.5f, 0.8f, 1);

            count = 15;
            float[] secondOutputBufferA = new float[count], secondOutputBufferB = new float[count];
            NoiseDotNet.Noise.CellularNoise3D(xBuffer, yBuffer, zBuffer, secondOutputBufferA, secondOutputBufferB, 1.5f, 1.8f, 2.1f, 0.5f, 0.8f, 1);

            bool bufferSizeDoesNotMatter = true;
            for (int i = 0; i < count; ++i)
            {
                bufferSizeDoesNotMatter &= EqualEnough(outputBufferA[i], secondOutputBufferA[i]);
                bufferSizeDoesNotMatter &= EqualEnough(outputBufferB[i], secondOutputBufferB[i]);
            }

            count = 7;
            secondOutputBufferA = new float[count];
            secondOutputBufferB = new float[count];
            NoiseDotNet.Noise.CellularNoise3D(xBuffer, yBuffer, zBuffer, secondOutputBufferA, secondOutputBufferB, 1.5f, 1.8f, 2.1f, 0.5f, 0.8f, 1);

            for (int i = 0; i < count; ++i)
            {
                bufferSizeDoesNotMatter &= EqualEnough(outputBufferA[i], secondOutputBufferA[i]);
                bufferSizeDoesNotMatter &= EqualEnough(outputBufferB[i], secondOutputBufferB[i]);
            }

            Assert.That(bufferSizeDoesNotMatter, "CellularNoise3D does not have consistent behavior across different buffer sizes");
        }

        [Test]
        public void CellularNoise3DExtraParameters()
        {
            for (int count = 7; count < 16; count += 8)
            {
                GenerateXYZBuffers(count, out float[] xBuffer, out float[] yBuffer, out float[] zBuffer);

                float[] outputBufferA = new float[count], outputBufferB = new float[count];
                NoiseDotNet.Noise.CellularNoise3D(xBuffer, yBuffer, zBuffer, outputBufferA, outputBufferB, 1f, 1f, 1f, 1f, 1f, 1);

                float[] secondOutputBufferA = new float[count], secondOutputBufferB = new float[count];
                NoiseDotNet.Noise.CellularNoise3D(xBuffer, yBuffer, zBuffer, secondOutputBufferA, secondOutputBufferB, 1f, 1f, 1f, 3f, 4f, 1);

                bool amplitudeWorks = true;
                for (int i = 0; i < count; ++i)
                {
                    amplitudeWorks &= EqualEnough(outputBufferA[i] * 3f, secondOutputBufferA[i]);
                    amplitudeWorks &= EqualEnough(outputBufferB[i] * 4f, secondOutputBufferB[i]);
                }
                Assert.That(amplitudeWorks, "CellularNoise3D amplitude does not work correctly");

                for (int i = 0; i < count; ++i)
                {
                    xBuffer[i] = xBuffer[i] * 3f;
                    yBuffer[i] = yBuffer[i] * 4f;
                    zBuffer[i] = zBuffer[i] * 5f;
                }

                NoiseDotNet.Noise.CellularNoise3D(xBuffer, yBuffer, zBuffer, secondOutputBufferA, secondOutputBufferB, 1f / 3f, 1f / 4f, 1 / 5f, 1f, 1f, 1);

                bool frequencyWorks = true;
                for (int i = 0; i < count; ++i)
                {
                    frequencyWorks &= EqualEnough(outputBufferA[i], secondOutputBufferA[i]);
                    frequencyWorks &= EqualEnough(outputBufferB[i], secondOutputBufferB[i]);
                }
                Assert.That(frequencyWorks, "CellularNoise3D frequency does not work correctly");

                NoiseDotNet.Noise.CellularNoise3D(xBuffer, yBuffer, zBuffer, secondOutputBufferA, secondOutputBufferB, 1f / 3f, 1f / 4f, 1f / 5f, 1f, 1f, 2);

                bool seedWorks = false;
                for (int i = 0; i < count; ++i)
                    seedWorks |= !EqualEnough(outputBufferA[i], secondOutputBufferA[i]);
                Assert.That(seedWorks, "CellularNoise3D seed does not work correctly");
            }
        }

        /// <summary>
        /// Generates a buffer of random coordinates with length <paramref name="count"/> to use for testing. 
        /// </summary>
        static void GenerateXYZBuffers(int count, out float[] xBuffer, out float[] yBuffer, out float[] zBuffer)
        {
            Random random = new(1);
            int testCount = 16;
            xBuffer = new float[testCount];
            yBuffer = new float[testCount];
            zBuffer = new float[testCount];
            for (int i = 0; i < testCount; i++)
            {
                xBuffer[i] = (float)random.NextSingle() * 100f;
                yBuffer[i] = (float)random.NextSingle() * 100f;
                zBuffer[i] = (float)random.NextSingle() * 100f;
            }
        }
    }
}
