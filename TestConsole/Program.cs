using NoiseDotNet;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Attributes;

BenchmarkRunner.Run<Benchmark>();

public class Benchmark
{
    float[] xArray, yArray, zArray;
    float[] result, result2;

    [GlobalSetup]
    public void Setup()
    {

        int width = 224, height = 224;
        result  = new float[width * height];
        result2 = new float[width * height];
        xArray = new float[width * height];
        yArray = new float[xArray.Length];
        zArray = new float[xArray.Length];
        int index = 0;
        for (int y = 0; y < height; ++y)
            for (int x = 0; x < width; ++x)
            {
                xArray[index] = x;
                yArray[index] = y;
                zArray[index++] = 0;
            }
    }


    [Benchmark(OperationsPerInvoke = 224 * 224)]
    public void GradientNoise2D()
    {
        Noise.GradientNoise2D(xArray, yArray, result, 0.1f, 0.1f, 1f, 1);
    }

    [Benchmark(OperationsPerInvoke = 224 * 224)]
    public void GradientNoise3D()
    {
        Noise.GradientNoise3D(xArray, yArray, zArray, result, 0.1f, 0.1f, 0.1f, 1f, 1);
    }

    [Benchmark(OperationsPerInvoke = 224 * 224)]
    public void CellularNoise2D()
    {
        Noise.CellularNoise2D(xArray, yArray, result, result2, 0.1f, 0.1f, 1f, 1f, 1);
    }

    [Benchmark(OperationsPerInvoke = 224 * 224)]
    public void CellularNoise3D()
    {
        Noise.CellularNoise3D(xArray, yArray, zArray, result, result2, 0.1f, 0.1f, 0.1f, 1f, 1f, 1);
    }
}