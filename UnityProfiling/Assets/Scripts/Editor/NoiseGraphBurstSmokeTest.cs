// TEMPORARY smoke test used to verify NoiseGraph bytecode evaluation compiles and runs
// correctly under Burst in Unity, and that the graph-evaluated noise ops match the
// direct (non-graph) Noise API. Safe to delete after verification.
using System;
using NoiseDotNet;
using UnityEditor;
using UnityEngine;

public static class NoiseGraphBurstSmokeTest
{
    const int Seed = 12345;
    const float Freq = 0.13f;
    const int SampleCount = 37; // deliberately not a multiple of common SIMD widths

    public static void Run()
    {
        try
        {
            RunInternal();
            Debug.Log("NOISEGRAPH_SMOKE_TEST: PASSED");
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogError("NOISEGRAPH_SMOKE_TEST: FAILED - " + ex);
            EditorApplication.Exit(1);
        }
    }

    static void RunInternal()
    {
        float[] xCoords = new float[SampleCount];
        float[] yCoords = new float[SampleCount];
        for (int i = 0; i < SampleCount; i++)
        {
            xCoords[i] = i * 0.7f - 3.1f;
            yCoords[i] = i * -0.4f + 1.9f;
        }

        // Exercises the Perlin2D noise op plus every arithmetic op
        // (Min, Max, Pow, SmoothStep01, Lerp, Floor, Negate, Multiply, Inverse, Add).
        NoiseScalar noise = NoiseGraph.Perlin(NoiseGraph.Coordinates(NoiseGraph.Constant(Freq, Freq)));
        NoiseScalar a = NoiseGraph.Max(NoiseGraph.Min(noise, NoiseGraph.Constant(0.5f)), NoiseGraph.Constant(-0.5f));
        NoiseScalar b = NoiseGraph.Pow(NoiseGraph.Abs(a) + NoiseGraph.Constant(1f), NoiseGraph.Constant(2f));
        NoiseScalar c = NoiseGraph.SmoothStep(NoiseGraph.Saturate(a));
        NoiseScalar d = NoiseGraph.Lerp(b, c, NoiseGraph.Constant(0.5f));
        NoiseScalar e = NoiseGraph.Floor(d * NoiseGraph.Constant(10f));
        NoiseScalar f = NoiseGraph.Negate(e);
        NoiseScalar arithmeticGraph = NoiseGraph.Inverse(f + NoiseGraph.Constant(100f));

        float[] graphOutput = new float[SampleCount];
        NoiseGraph.Evaluate2D(arithmeticGraph, xCoords, yCoords, graphOutput, Seed);
        for (int i = 0; i < SampleCount; i++)
        {
            if (float.IsNaN(graphOutput[i]) || float.IsInfinity(graphOutput[i]))
                throw new Exception($"Arithmetic graph output[{i}] is not finite: {graphOutput[i]}");
        }

        // Cross-check the graph-evaluated Perlin2D noise against the direct (non-graph) Noise API.
        float[] graphNoiseOnly = new float[SampleCount];
        NoiseGraph.Evaluate2D(noise, xCoords, yCoords, graphNoiseOnly, Seed);

        float[] directNoise = new float[SampleCount];
        Noise.GradientNoise2D(xCoords, yCoords, directNoise, new NoiseSettings(Freq, Freq, 1f, 1f, Seed));

        for (int i = 0; i < SampleCount; i++)
            CheckClose("Perlin2D", i, graphNoiseOnly[i], directNoise[i]);

        // 2D cellular noise op, cross-checked the same way.
        NoiseVector2 xyScaled = NoiseGraph.Coordinates(NoiseGraph.Constant(Freq, Freq));
        (NoiseScalar center2D, NoiseScalar edge2D) = NoiseGraph.Cellular(xyScaled);
        float[] graphCenter2D = new float[SampleCount];
        float[] graphEdge2D = new float[SampleCount];
        NoiseGraph.Evaluate2D(center2D, edge2D, xCoords, yCoords, graphCenter2D, graphEdge2D, Seed);

        float[] directCenter2D = new float[SampleCount];
        float[] directEdge2D = new float[SampleCount];
        Noise.CellularNoise2D(xCoords, yCoords, directCenter2D, directEdge2D, new NoiseSettings(Freq, Freq, 1f, 1f, Seed));

        for (int i = 0; i < SampleCount; i++)
        {
            CheckClose("Cellular2D center", i, graphCenter2D[i], directCenter2D[i]);
            CheckClose("Cellular2D edge", i, graphEdge2D[i], directEdge2D[i]);
        }

        // 3D graph: exercises Perlin3D + Cellular3 noise ops.
        float[] xCoords3D = new float[SampleCount];
        float[] yCoords3D = new float[SampleCount];
        float[] zCoords3D = new float[SampleCount];
        for (int i = 0; i < SampleCount; i++)
        {
            xCoords3D[i] = i * 0.5f - 2.3f;
            yCoords3D[i] = i * -0.3f + 1.1f;
            zCoords3D[i] = i * 0.2f - 0.7f;
        }

        NoiseVector3 xyzScaled = NoiseGraph.Coordinates(NoiseGraph.Constant(Freq, Freq, Freq));
        NoiseScalar perlin3D = NoiseGraph.Perlin(xyzScaled);
        (NoiseScalar center3D, NoiseScalar edge3D) = NoiseGraph.Cellular(xyzScaled);

        float[] graphPerlin3D = new float[SampleCount];
        float[] graphCenter3D = new float[SampleCount];
        float[] graphEdge3D = new float[SampleCount];
        NoiseGraph.Evaluate3D(perlin3D, center3D, edge3D, xCoords3D, yCoords3D, zCoords3D, graphPerlin3D, graphCenter3D, graphEdge3D, Seed);

        float[] directPerlin3D = new float[SampleCount];
        Noise.GradientNoise3D(xCoords3D, yCoords3D, zCoords3D, directPerlin3D, new NoiseSettings(Freq, Freq, Freq, 1f, 1f, Seed));

        float[] directCenter3D = new float[SampleCount];
        float[] directEdge3D = new float[SampleCount];
        Noise.CellularNoise3D(xCoords3D, yCoords3D, zCoords3D, directCenter3D, directEdge3D, new NoiseSettings(Freq, Freq, Freq, 1f, 1f, Seed));

        for (int i = 0; i < SampleCount; i++)
        {
            CheckClose("Perlin3D", i, graphPerlin3D[i], directPerlin3D[i]);
            CheckClose("Cellular3D center", i, graphCenter3D[i], directCenter3D[i]);
            CheckClose("Cellular3D edge", i, graphEdge3D[i], directEdge3D[i]);
        }
    }

    static void CheckClose(string label, int index, float graphValue, float directValue)
    {
        if (float.IsNaN(graphValue) || float.IsInfinity(graphValue))
            throw new Exception($"{label}[{index}] is not finite: {graphValue}");

        float diff = Math.Abs(graphValue - directValue);
        if (diff > 1e-4f)
            throw new Exception($"{label} mismatch at {index}: graph={graphValue} direct={directValue} diff={diff}");
    }
}
