using System;
using UnityEngine;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class NoiseProfiler : MonoBehaviour
{
    public bool RunProfiling;

    public const float ProfilingTimeMS = 10000;
    public const int Size = 224;
    public const int SampleCount = Size * Size;
    NoiseDotNet.NoiseSettings settings = new(xFreq: 0.1f, yFreq: 0.1f, zFreq: 0.1f, seed: 100);

    float[] xCoords = new float[SampleCount];
    float[] yCoords = new float[SampleCount];
    float[] zCoords = new float[SampleCount];
    float[] output = new float[SampleCount];
    float[] output2 = new float[SampleCount];

    void Start()
    {
        if (RunProfiling)
        {
            InitCoordinateBuffers();
            RunAllProfiles();
        }
    }

    void RunAllProfiles()
    {
        LogProfile("GradientNoise2D", () =>
        {
            NoiseDotNet.Noise.GradientNoise2D(xCoords, yCoords, output, settings);
        });

        LogProfile("GradientNoise3D", () =>
        {
            NoiseDotNet.Noise.GradientNoise3D(xCoords, yCoords, zCoords, output, settings);
        });

        LogProfile("CellularNoise2D", () =>
        {
            NoiseDotNet.Noise.CellularNoise2D(xCoords, yCoords, output, output2, settings);
        });

        LogProfile("CellularNoise3D", () =>
        {
            NoiseDotNet.Noise.CellularNoise3D(xCoords, yCoords, zCoords, output, output2, settings);
        });
    }

    void LogProfile(string label, Action action)
    {
        float avgMs = Profile(action, ProfilingTimeMS);
        float averageNS = avgMs * 1e6f / SampleCount;
        Debug.Log($"{label}: {averageNS:F4} ns per sample");
    }

    void InitCoordinateBuffers()
    {
        int index = 0;
        for (int y = 0; y < Size; ++y)
            for (int x = 0; x < Size; ++x)
            {
                xCoords[index] = x;
                yCoords[index] = y;
                zCoords[index] = x + y;
                index++;
            }
    }

    float Profile(Action action, float timeMS = 1000)
    {
        action();

        var sw = Stopwatch.StartNew();
        action();
        sw.Stop();

        double singleRunMs = Math.Max(sw.Elapsed.TotalMilliseconds, 3e-5f);
        int runCount = Math.Max(1, (int)Math.Floor(timeMS / singleRunMs));

        float[] samples = new float[runCount];

        for (int i = 0; i < runCount; i++)
        {
            sw.Restart();
            action();
            sw.Stop();
            samples[i] = (float)sw.Elapsed.TotalMilliseconds;
        }

        Array.Sort(samples);

        int trim = (int)Math.Floor(runCount * 0.05);
        int start = trim;
        int end = runCount - trim;

        if (end <= start)
        {
            start = 0;
            end = runCount;
        }

        double sum = 0.0;
        for (int i = start; i < end; i++)
            sum += samples[i];

        return (float)(sum / (end - start));
    }
}
    