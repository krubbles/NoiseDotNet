using CSharpNoise;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Attributes;

BenchmarkRunner.Run<Benchmark>();

CoordinateGrid coordGrid = new() { xStart = 0, yStart = 0, xStep = 1f, yStep = 1f, height = 600, width = 1200 };
float[] result = new float[coordGrid.width * coordGrid.height];
NetCoreNoise.QuadraticNoise2D(coordGrid, 0.1f, 1f, 1, result);

/*
for (int i = 0; i < 20; ++i)
{
    Console.WriteLine(Profiler.ProfileRelative("QN2D vs Itself", 3000,
        () => NetCoreNoise.QuadraticNoise2D(coordGrid, 0.1f, 1f, 1, result),
        () => NetCoreNoise.QuadraticNoise2D(coordGrid, 0.1f, 1f, 1, result)));
}
*/

int index = 0;
for (int y = 0; y < 60; ++y)
{
    System.Text.StringBuilder b = new();
    for (int x = 0; x < 120; ++x)
    {
        float n = result[index++];
        char c = n switch
        {
            < -0.4f => '.',
            < -0.05f => ':',
            < 0.05f => '+',
            < 0.4f => 'O',
            _ => '#'
        };
        b.Append(c);

    }
    Console.WriteLine(b.ToString());
    b.Clear();
}
Console.ReadLine();

public class Benchmark
{
    CoordinateGrid coordGrid;
    float[] result;

    [GlobalSetup]
    public void Setup()
    {
        coordGrid = new() { xStart = 0, yStart = 0, xStep = 1f, yStep = 1f, height = 600, width = 1200 };
        result  = new float[coordGrid.width * coordGrid.height];
    }

    [Benchmark(OperationsPerInvoke = 600 * 1200)]
    public void QN2DBenchmark1()
    {
        NetCoreNoise.QuadraticNoise2D(coordGrid, 0.1f, 1f, 1, result);
    }
}