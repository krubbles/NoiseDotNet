using UnityEngine;

public class Tester : MonoBehaviour
{
    const int SampleSize = 128;
    const int SampleCount = SampleSize * SampleSize;
    const float LowestOctaveUnitsPerPixel = 0.05f;

    [SerializeField] Texture2D atlasTexture;
    [SerializeField] int seed = 12345;
    [SerializeField] int octaves = 5;
    [SerializeField] float persistence = 0.5f;
    [SerializeField] float lacunarity = 2.0f;

    float[] xCoords2D;
    float[] yCoords2D;

    float[] xCoords3D;
    float[] yCoords3D;
    float[] zCoords3D;

    float[] outputA;
    float[] outputB;

    public Texture2D AtlasTexture => atlasTexture;

    void Start()
    {
        GenerateAtlas();
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
