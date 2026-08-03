using UnityEngine;

public class Tester : MonoBehaviour
{
    const int SampleSize = 256;
    const int SampleCount = SampleSize * SampleSize;
    const float LowestOctaveUnitsPerPixel = 0.03f;

    [SerializeField] Texture2D atlasTexture;
    [SerializeField] Texture2D graphAtlasTexture;
    [SerializeField] int seed = 12345;
    [SerializeField] int octaves = 1;
    [SerializeField] float persistence = 0.5f;
    [SerializeField] float lacunarity = 2.0f;
    [SerializeField] float domainWarpStrength = 1f;

    float[] xCoords2D;
    float[] yCoords2D;

    float[] xCoords3D;
    float[] yCoords3D;
    float[] zCoords3D;

    float[] outputA;
    float[] outputB;

    public Texture2D AtlasTexture => atlasTexture;
    public Texture2D GraphAtlasTexture => graphAtlasTexture;

    void Start()
    {
        GenerateAtlas();
        GenerateGraphAtlas();
    }

    [ContextMenu("Generate Atlas")]
    public void GenerateAtlas()
    {
        EnsureBuffers();
        Build2DCoordinates();
        BuildRotated3DCoordinates();

        var settings = new NoiseDotNet.NoiseSettings(
            xFreq: LowestOctaveUnitsPerPixel,
            yFreq: LowestOctaveUnitsPerPixel,
            zFreq: LowestOctaveUnitsPerPixel,
            amplitude: 1f,
            amplitude2: 1f,
            seed: seed,
            accumulate: false
        );

        var fractal = new NoiseDotNet.FractalSettings(octaves, persistence, lacunarity);

        Texture2D[] tiles = new Texture2D[6];

        ClearOutputBuffers();
        NoiseDotNet.Noise.GradientNoise2DFractal(xCoords2D, yCoords2D, outputA, settings, fractal);
        tiles[0] = CreateTextureFromSamples(outputA);

        ClearOutputBuffers();
        NoiseDotNet.Noise.GradientNoise3DFractal(xCoords3D, yCoords3D, zCoords3D, outputA, settings, fractal);
        tiles[1] = CreateTextureFromSamples(outputA);

        ClearOutputBuffers();
        NoiseDotNet.Noise.CellularNoise2DFractal(xCoords2D, yCoords2D, outputA, outputB, settings, fractal);
        tiles[2] = CreateTextureFromSamples(outputA);
        tiles[3] = CreateTextureFromSamples(outputB);

        ClearOutputBuffers();
        NoiseDotNet.Noise.CellularNoise3DFractal(xCoords3D, yCoords3D, zCoords3D, outputA, outputB, settings, fractal);
        tiles[4] = CreateTextureFromSamples(outputA);
        tiles[5] = CreateTextureFromSamples(outputB);

        atlasTexture = BuildAtlas(tiles, 3, 2);
    }

    /// <summary>
    /// Builds a few NoiseGraphs (evaluated through the compiled bytecode interpreter, i.e. through
    /// a Burst job in the editor) and renders each to a tile, so the graph API's output can be
    /// visually compared against the direct Noise API output in <see cref="AtlasTexture"/>.
    /// </summary>
    [ContextMenu("Generate Graph Atlas")]
    public void GenerateGraphAtlas()
    {
        EnsureBuffers();
        Build2DCoordinates();
        BuildRotated3DCoordinates();

        float freq = LowestOctaveUnitsPerPixel;

        Texture2D[] tiles = new Texture2D[6];

        // Plain 2D and 3D Perlin noise, for a direct visual comparison against the top-left two
        // tiles of AtlasTexture (which evaluate the same noise through the non-graph Noise API).
        var perlin2D = NoiseDotNet.NoiseGraph.Perlin(NoiseDotNet.NoiseGraph.Coordinates(NoiseDotNet.NoiseGraph.Constant(freq, freq)));
        ClearOutputBuffers();
        NoiseDotNet.NoiseGraph.Evaluate2D(perlin2D, xCoords2D, yCoords2D, outputA, seed);
        tiles[0] = CreateTextureFromSamples(outputA);

        var perlin3D = NoiseDotNet.NoiseGraph.Perlin(NoiseDotNet.NoiseGraph.Coordinates(NoiseDotNet.NoiseGraph.Constant(freq, freq, freq)));
        ClearOutputBuffers();
        NoiseDotNet.NoiseGraph.Evaluate3D(perlin3D, xCoords3D, yCoords3D, zCoords3D, outputA, seed);
        tiles[1] = CreateTextureFromSamples(outputA);

        // 2D cellular noise, both outputs from one compiled graph.
        (var center2D, var edge2D) = NoiseDotNet.NoiseGraph.Cellular(NoiseDotNet.NoiseGraph.Coordinates(NoiseDotNet.NoiseGraph.Constant(freq, freq)));
        ClearOutputBuffers();
        NoiseDotNet.NoiseGraph.Evaluate2D(center2D, edge2D, xCoords2D, yCoords2D, outputA, outputB, seed);
        tiles[2] = CreateTextureFromSamples(outputA);
        tiles[3] = CreateTextureFromSamples(outputB);

        // Two-octave sum built from graph nodes (Perlin, Multiply, Add), exercising the arithmetic
        // bytecode ops rather than just a single noise instruction.
        var octave1 = perlin2D * NoiseDotNet.NoiseGraph.Constant(0.6f);
        var octave2 = NoiseDotNet.NoiseGraph.Perlin(NoiseDotNet.NoiseGraph.Coordinates(NoiseDotNet.NoiseGraph.Constant(freq * 2f, freq * 2f))) * NoiseDotNet.NoiseGraph.Constant(0.4f);
        var twoOctaveSum = octave1 + octave2;
        ClearOutputBuffers();
        NoiseDotNet.NoiseGraph.Evaluate2D(twoOctaveSum, xCoords2D, yCoords2D, outputA, seed);
        tiles[4] = CreateTextureFromSamples(outputA);

        // An fbm field domain warped by another (decorrelated) fbm field: sample position is offset
        // by a vector built from two fbm evaluations before being fed into a third fbm evaluation.
        // Exercises deeper graphs (each fbm is `octaves` chained Perlin + Add + Multiply nodes) and
        // reusing a NoiseVector2 expression, rather than the raw coordinate inputs, as a noise position.
        var position = NoiseDotNet.NoiseGraph.XY;
        var warpX = Fbm(position, freq);
        var warpY = Fbm(position + NoiseDotNet.NoiseGraph.Constant(5.2f, 1.3f), freq);
        var warpOffset = new NoiseDotNet.NoiseVector2(warpX, warpY) * NoiseDotNet.NoiseGraph.Constant(domainWarpStrength / freq);
        var domainWarped = Fbm(position + warpOffset, freq);
        ClearOutputBuffers();
        NoiseDotNet.NoiseGraph.Evaluate2D(domainWarped, xCoords2D, yCoords2D, outputA, seed);
        tiles[5] = CreateTextureFromSamples(outputA);

        graphAtlasTexture = BuildAtlas(tiles, 3, 2);
    }

    /// <summary>
    /// Builds a fractal Brownian motion NoiseScalar graph: a sum of Perlin octaves at
    /// <paramref name="baseFrequency"/> scaled by <see cref="lacunarity"/> each octave, with
    /// amplitude scaled by <see cref="persistence"/> each octave, using <see cref="octaves"/> octaves.
    /// </summary>
    NoiseDotNet.NoiseScalar Fbm(NoiseDotNet.NoiseVector2 position, float baseFrequency)
    {
        var sum = NoiseDotNet.NoiseGraph.Zero;
        float frequency = baseFrequency;
        float amplitude = 1f;
        for (int octave = 0; octave < octaves; octave++)
        {
            var noise = NoiseDotNet.NoiseGraph.Perlin(position * NoiseDotNet.NoiseGraph.Constant(frequency, frequency));
            sum += noise * NoiseDotNet.NoiseGraph.Constant(amplitude);
            frequency *= lacunarity;
            amplitude *= persistence;
        }
        return sum;
    }

    void EnsureBuffers()
    {
        xCoords2D ??= new float[SampleCount];
        yCoords2D ??= new float[SampleCount];

        xCoords3D ??= new float[SampleCount];
        yCoords3D ??= new float[SampleCount];
        zCoords3D ??= new float[SampleCount];

        outputA ??= new float[SampleCount];
        outputB ??= new float[SampleCount];
    }

    void Build2DCoordinates()
    {
        int index = 0;
        for (int y = 0; y < SampleSize; ++y)
        {
            for (int x = 0; x < SampleSize; ++x)
            {
                xCoords2D[index] = x;
                yCoords2D[index] = y;
                index++;
            }
        }
    }

    void BuildRotated3DCoordinates()
    {
        Vector3 axisU = new Vector3(1.0f, 0.8f, 0.6f).normalized;
        Vector3 axisV = new Vector3(-0.4f, 1.0f, 0.7f).normalized;

        int index = 0;
        for (int y = 0; y < SampleSize; ++y)
        {
            for (int x = 0; x < SampleSize; ++x)
            {
                Vector3 samplePoint = axisU * x + axisV * y;
                xCoords3D[index] = samplePoint.x;
                yCoords3D[index] = samplePoint.y;
                zCoords3D[index] = samplePoint.z;
                index++;
            }
        }
    }

    void ClearOutputBuffers()
    {
        System.Array.Clear(outputA, 0, outputA.Length);
        System.Array.Clear(outputB, 0, outputB.Length);
    }

    Texture2D CreateTextureFromSamples(float[] samples)
    {
        float min = float.MaxValue;
        float max = float.MinValue;

        for (int i = 0; i < samples.Length; ++i)
        {
            float value = samples[i];
            if (value < min) min = value;
            if (value > max) max = value;
        }

        float range = max - min;
        if (range < 1e-6f)
            range = 1f;

        Color[] pixels = new Color[SampleCount];
        for (int i = 0; i < samples.Length; ++i)
        {
            float normalized = Mathf.Clamp01((samples[i] - min) / range);
            pixels[i] = new Color(normalized, normalized, normalized, 1f);
        }

        Texture2D tex = new Texture2D(SampleSize, SampleSize, TextureFormat.RGBA32, false, true)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "FractalNoiseSample"
        };

        tex.SetPixels(pixels);
        tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        return tex;
    }

    Texture2D BuildAtlas(Texture2D[] textures, int columns, int rows)
    {
        int atlasWidth = columns * SampleSize;
        int atlasHeight = rows * SampleSize;

        Texture2D atlas = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false, true)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "FractalNoiseAtlas"
        };

        for (int i = 0; i < textures.Length; ++i)
        {
            int col = i % columns;
            int row = i / columns;
            int x = col * SampleSize;
            int y = (rows - 1 - row) * SampleSize;

            atlas.SetPixels(x, y, SampleSize, SampleSize, textures[i].GetPixels());
        }

        atlas.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        return atlas;
    }
}
