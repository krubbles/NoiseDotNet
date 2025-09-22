using CSharpNoise;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Attributes;

BenchmarkRunner.Run<Benchmark>();
int width = 120, height = 100;
float[] result = new float[width * height];
if (true) ;
{
    float[] xArray = new float[width * height];
    float[] yArray = new float[width * height];
    float[] zArray = new float[width * height];

    int arrayIndex = 0;
    for (int y = 0; y < height; ++y)
        for (int x = 0; x < width; ++x) 
        {
            xArray[arrayIndex] = x * 1f;
            yArray[arrayIndex] = y * 1f;
            zArray[arrayIndex++] = x * 1f;
        }

    Noise.QuadraticNoise3D(xArray, yArray, zArray, 0.1f, 0.1f, 0.1f, 1f, 1, result);
}

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
    float[] xArray, yArray, zArray;
    float[] result;

    [GlobalSetup]
    public void Setup()
    {
        coordGrid = new() { xStart = 0, yStart = 0, xStep = 1f, yStep = 1f, height = 600, width = 1200 };
        result  = new float[coordGrid.width * coordGrid.height];
        xArray = new float[coordGrid.width * coordGrid.height];
        yArray = new float[xArray.Length];
        zArray = new float[xArray.Length];
        int index = 0;
        for (int y = 0; y < coordGrid.height; ++y)
            for (int x = 0; x < coordGrid.width; ++x)
            {
                xArray[index] = x;
                yArray[index] = y;
                xArray[index++] = 0;
            }
    }


    [Benchmark(OperationsPerInvoke = 600 * 1200)]
    public void QuadraticNoise2D()
    {
        Noise.QuadraticNoise2D(xArray, yArray, 0.1f, 1f, 1f, 1, result);
    }

    [Benchmark(OperationsPerInvoke = 600 * 1200)]
    public void QuadraticNoise3D()
    {
        Noise.QuadraticNoise3D(xArray, yArray, zArray, 0.1f, 0.1f, 0.1f, 1f, 1, result);
    }

}