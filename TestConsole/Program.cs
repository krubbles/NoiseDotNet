using NoiseDotNet;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Attributes;

int width = 120, height = 100;
float[] result = new float[width * height];
float[] result2 = new float[width * height];

if (true) 
{
    float[] xArray = new float[width * height];
    float[] yArray = new float[width * height];
    float[] zArray = new float[width * height];

    int arrayIndex = 0;
    for (int y = 0; y < height; ++y)
        for (int x = 0; x < width; ++x) 
        {
            xArray[arrayIndex] = x * 0.707f;
            yArray[arrayIndex] = y * 0.707f;
            zArray[arrayIndex++] = x * 0.707f - y * 0.707f;
        }
#if false
    Noise.QuadraticNoise3D(xArray, yArray, zArray, 0.1f, 0.1f, 0.1f, 1f, 1, result);
#else
    Noise.CellularNoise3D(xArray, yArray, zArray, result, result2, 0.1f, 0.1f, 0.1f, 1f, 1f, 1);
#endif
}
BenchmarkRunner.Run<Benchmark>();


/*
for (int i = 0; i < 20; ++i)
{
    Console.WriteLine(Profiler.ProfileRelative("QN2D vs Itself", 3000,
        () => NetCoreNoise.QuadraticNoise2D(coordGrid, 0.1f, 1f, 1, result),
        () => NetCoreNoise.QuadraticNoise2D(coordGrid, 0.1f, 1f, 1, result)));
}
*/

int index = 0;
for (int y = 0; y < 100; ++y)
{
    System.Text.StringBuilder b = new();
    for (int x = 0; x < 120; ++x)
    {
        float n = result[index++];
#if true
        char c = n switch
        {
            < 0.15f => '.',
            < 0.3f => ':',
            < 0.45f => '+',
            < 0.6f => 'O',
            _ => '#'
        };
#else
        char c = n switch
        {
            < -0.4f => '.',
            < -0.05f => ':',
            < 0.05f => '+',
            < 0.4f => 'O',
            _ => '#'
        };
#endif
        b.Append(c);

    }
    Console.WriteLine(b.ToString());
    b.Clear();
}
Console.ReadLine();

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