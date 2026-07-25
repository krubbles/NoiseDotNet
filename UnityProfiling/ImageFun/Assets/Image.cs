using System;
using System.Collections.Generic;
using NoiseDotNet;
using UnityEngine;

public class Image : MonoBehaviour
{
    const float NoiseModePersistence = 0.5f;
    const float NoiseModeLacunarity = 2f;
    const int NoiseModeResolution = 256;

    public enum DisplayMode
    {
        Image,
        Heightfield
    }

    public enum HeightfieldInitMode
    {
        SlopeNoise,
        ImageBrightness
    }

    public Texture2D Tex;

    [Header("Simulation")]
    [Min(1)] public int PassesPerFrame = 1;
    [Min(1)] public int DropsPerPass = 20000;
    [Min(0f)] public float Strength = 0.04f;
    [Min(0.001f)] public float Falloff = 0.09f;
    public int Seed = 17;

    [Header("Terrain")]
    public HeightfieldInitMode HeightfieldInit = HeightfieldInitMode.SlopeNoise;
    public float LeftHeight = 1.35f;
    public float RightHeight = 0.65f;
    public float NoiseScale = 48f;
    public float NoiseAmplitude = 0.055f;
    public float BrightnessHeightScale = 1f;
    public float BrightnessHeightOffset = 0f;

    [Header("Noise Altitude Mode")]
    public bool UseNoiseAltitudeMode = false;
    public Color NoiseModeStartColor = Color.gray;
    [Min(1)] public int NoiseModeOctaves = 6;
    [Min(0.0001f)] public float NoiseModeFrequency = 8f;
    [Min(0.0001f)] public float NoiseModeWarpFrequency = 3f;
    [Min(0f)] public float NoiseModeWarpStrength = 0.4f;
    [Min(0f)] public float NoiseModeAltitudePerPass = 0.001f;
    [Min(0f)] public float NoiseModePlaneSpeed = 0.01f;
    [Range(0f, 1f)] public float NoiseModeAltitudeDecay = 0.999f;

    [Header("Color Flow")]
    [Range(0f, 1f)] public float DropletToImageBlend = 0.08f;
    [Range(0f, 1f)] public float ImageToDropletBlend = 0.08f;

    [Header("Display")]
    public bool ResetOnEnable = true;
    public DisplayMode Mode = DisplayMode.Image;
    public bool ShowWater = true;
    public Color WaterTint = new Color(0.13f, 0.45f, 0.95f, 1f);
    [Range(0f, 1f)] public float WaterTintStrength = 0.08f;

    int _width;
    int _height;
    int _pass;
    float[] _heightField;
    float[] _flowField;
    float[] _riverSedimentField;
    float[] _noiseXCoords;
    float[] _noiseYCoords;
    float[] _noiseZCoords;
    float[] _noiseWarpX;
    float[] _noiseWarpY;
    float[] _noiseValues;
    Color[] _sourcePixels;
    Color[] _pixels;
    Color[] _displayPixels;
    float[] _heightSortBuffer;
    Texture2D _output;
    Sprite _outputSprite;
    bool _activeNoiseAltitudeMode;

    void OnEnable()
    {
        if (ResetOnEnable)
            ResetSimulation();
    }

    void Start()
    {
        if (_output == null)
            ResetSimulation();
    }

    void Update()
    {
        if (_output == null || _activeNoiseAltitudeMode != UseNoiseAltitudeMode)
            ResetSimulation();

        if (_output == null)
            return;

        for (int i = 0; i < PassesPerFrame; ++i)
            RunPass();

        UploadTexture();
    }

    [ContextMenu("Reset Simulation")]
    public void ResetSimulation()
    {
        if (!UseNoiseAltitudeMode && Tex == null)
            return;

        _width = UseNoiseAltitudeMode ? NoiseModeResolution : Tex.width;
        _height = UseNoiseAltitudeMode ? NoiseModeResolution : Tex.height;
        int count = _width * _height;

        _sourcePixels = UseNoiseAltitudeMode ? null : ReadPixels(Tex);
        _pixels = new Color[count];
        _displayPixels = new Color[count];
        _heightField = new float[count];
        _flowField = new float[count];
        _riverSedimentField = new float[count];
        _noiseXCoords = new float[count];
        _noiseYCoords = new float[count];
        _noiseZCoords = new float[count];
        _noiseWarpX = new float[count];
        _noiseWarpY = new float[count];
        _noiseValues = new float[count];
        _heightSortBuffer = new float[count];

        if (UseNoiseAltitudeMode)
            Array.Fill(_pixels, NoiseModeStartColor);
        else
            Array.Copy(_sourcePixels, _pixels, count);

        InitHeightField();
        _activeNoiseAltitudeMode = UseNoiseAltitudeMode;
        _pass = 0;

        if (_output != null)
            Destroy(_output);

        _output = new Texture2D(_width, _height, TextureFormat.RGBA32, false);
        _output.wrapMode = UseNoiseAltitudeMode || Tex == null ? TextureWrapMode.Clamp : Tex.wrapMode;
        _output.filterMode = FilterMode.Point;
        AttachOutputTexture();
        UploadTexture();
    }

    void InitHeightField()
    {
        if (UseNoiseAltitudeMode)
        {
            Array.Clear(_heightField, 0, _heightField.Length);
            return;
        }

        if (HeightfieldInit == HeightfieldInitMode.ImageBrightness)
        {
            InitBrightnessHeightField();
            return;
        }

        float invWidth = _width <= 1 ? 0f : 1f / (_width - 1);
        float invHeight = _height <= 1 ? 0f : 1f / (_height - 1);

        for (int y = 0; y < _height; ++y)
        {
            for (int x = 0; x < _width; ++x)
            {
                float tx = x * invWidth;
                float ty = y * invHeight;
                float slope = Mathf.Lerp(LeftHeight, RightHeight, tx);
                float warble = FractalPerlin(tx * NoiseScale + 11.37f, ty * NoiseScale + 4.91f) - 0.5f;
                _heightField[x + y * _width] = slope + warble * NoiseAmplitude;
            }
        }
    }

    void InitBrightnessHeightField()
    {
        for (int i = 0; i < _heightField.Length; ++i)
            _heightField[i] = _sourcePixels[i].grayscale * BrightnessHeightScale + BrightnessHeightOffset;
    }

    static float FractalPerlin(float x, float y)
    {
        const int octaves = 8;
        const float lacunarity = 2f;
        const float persistence = 0.5f;

        float sum = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float amplitudeSum = 0f;

        for (int octave = 0; octave < octaves; ++octave)
        {
            sum += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
            amplitudeSum += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return amplitudeSum > 0f ? sum / amplitudeSum : 0f;
    }

    void RunPass()
    {
        if (UseNoiseAltitudeMode)
            AddNoiseToAltitude();

        Array.Clear(_flowField, 0, _flowField.Length);
        Array.Clear(_riverSedimentField, 0, _riverSedimentField.Length);

        RobantsDropletErosion.TraceDroplets(
            _width,
            _height,
            _heightField,
            _pixels,
            _flowField,
            _riverSedimentField,
            Seed + _pass++,
            DropsPerPass,
            Strength,
            DropletToImageBlend,
            ImageToDropletBlend);
    }

    void AddNoiseToAltitude()
    {
        for (int i = 0; i < _heightField.Length; ++i)
            _heightField[i] *= NoiseModeAltitudeDecay;

        float invWidth = _width <= 1 ? 0f : 1f / (_width - 1);
        float invHeight = _height <= 1 ? 0f : 1f / (_height - 1);
        float planeZ = _pass * NoiseModePlaneSpeed;
        int seed = Seed;
        FractalSettings fractalSettings = new FractalSettings(Mathf.Max(1, NoiseModeOctaves), NoiseModePersistence, NoiseModeLacunarity);
        float amplitudeScale = 1f / GetFractalAmplitudeSum(fractalSettings);

        if (NoiseModeAltitudePerPass <= 0f)
            return;

        for (int y = 0; y < _height; ++y)
        {
            for (int x = 0; x < _width; ++x)
            {
                int index = x + y * _width;
                float nx = x * invWidth;
                float ny = y * invHeight;
                _noiseXCoords[index] = nx * NoiseModeWarpFrequency + 13.71f;
                _noiseYCoords[index] = ny * NoiseModeWarpFrequency + 41.23f;
                _noiseZCoords[index] = planeZ + 5.17f;
            }
        }

        Noise.GradientNoise3DFractal(
            _noiseXCoords,
            _noiseYCoords,
            _noiseZCoords,
            _noiseWarpX,
            new NoiseSettings(1f, 1f, 1f, amplitudeScale, 1f, seed + 101),
            fractalSettings);

        for (int y = 0; y < _height; ++y)
        {
            for (int x = 0; x < _width; ++x)
            {
                int index = x + y * _width;
                float nx = x * invWidth;
                float ny = y * invHeight;
                _noiseXCoords[index] = nx * NoiseModeWarpFrequency - 29.37f;
                _noiseYCoords[index] = ny * NoiseModeWarpFrequency + 7.89f;
                _noiseZCoords[index] = planeZ + 19.43f;
            }
        }

        Noise.GradientNoise3DFractal(
            _noiseXCoords,
            _noiseYCoords,
            _noiseZCoords,
            _noiseWarpY,
            new NoiseSettings(1f, 1f, 1f, amplitudeScale, 1f, seed + 211),
            fractalSettings);

        for (int y = 0; y < _height; ++y)
        {
            for (int x = 0; x < _width; ++x)
            {
                int index = x + y * _width;
                float nx = x * invWidth;
                float ny = y * invHeight;
                _noiseXCoords[index] = nx * NoiseModeFrequency + _noiseWarpX[index] * NoiseModeWarpStrength;
                _noiseYCoords[index] = ny * NoiseModeFrequency + _noiseWarpY[index] * NoiseModeWarpStrength;
                _noiseZCoords[index] = planeZ;
            }
        }

        Noise.GradientNoise3DFractal(
            _noiseXCoords,
            _noiseYCoords,
            _noiseZCoords,
            _noiseValues,
            new NoiseSettings(1f, 1f, 1f, amplitudeScale, 1f, seed + 307),
            fractalSettings);

        for (int i = 0; i < _heightField.Length; ++i)
            _heightField[i] += _noiseValues[i] * NoiseModeAltitudePerPass;
    }

    static float GetFractalAmplitudeSum(FractalSettings fractalSettings)
    {
        int octaves = Mathf.Max(1, fractalSettings.Octaves);
        float amplitude = 1f;
        float amplitudeSum = 0f;

        for (int octave = 0; octave < octaves; ++octave)
        {
            amplitudeSum += amplitude;
            amplitude *= fractalSettings.Persistence;
        }

        return amplitudeSum > 0f ? amplitudeSum : 1f;
    }

    void UploadTexture()
    {
        if (Mode == DisplayMode.Heightfield)
        {
            UploadHeightfield();
            return;
        }

        if (ShowWater)
        {
            for (int i = 0; i < _pixels.Length; ++i)
            {
                float wetness = Mathf.Clamp01(_flowField[i] * WaterTintStrength);
                _displayPixels[i] = Color.Lerp(_pixels[i], WaterTint, wetness);
                _displayPixels[i].a = _pixels[i].a;
            }

            _output.SetPixels(_displayPixels);
        }
        else
        {
            _output.SetPixels(_pixels);
        }

        _output.Apply(false);
    }

    void UploadHeightfield()
    {
        int finiteCount = 0;

        for (int i = 0; i < _heightField.Length; ++i)
        {
            float height = _heightField[i];
            if (float.IsNaN(height) || float.IsInfinity(height))
                continue;

            _heightSortBuffer[finiteCount++] = height;
        }

        if (finiteCount == 0)
        {
            Array.Fill(_displayPixels, Color.black);
            _output.SetPixels(_displayPixels);
            _output.Apply(false);
            return;
        }

        Array.Sort(_heightSortBuffer, 0, finiteCount);
        float low = _heightSortBuffer[Mathf.Clamp(Mathf.FloorToInt((finiteCount - 1) * 0.01f), 0, finiteCount - 1)];
        float high = _heightSortBuffer[Mathf.Clamp(Mathf.CeilToInt((finiteCount - 1) * 0.99f), 0, finiteCount - 1)];

        if (Mathf.Approximately(low, high))
        {
            Array.Fill(_displayPixels, Color.black);
            _output.SetPixels(_displayPixels);
            _output.Apply(false);
            return;
        }

        for (int i = 0; i < _heightField.Length; ++i)
        {
            float height = _heightField[i];
            float value = float.IsNaN(height) || float.IsInfinity(height) ? 0f : Mathf.Clamp01(Mathf.InverseLerp(low, high, height));
            _displayPixels[i] = new Color(value, value, value, 1f);
        }

        _output.SetPixels(_displayPixels);
        _output.Apply(false);
    }

    void AttachOutputTexture()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.mainTexture = _output;

            if (renderer.material.HasProperty("_BaseMap"))
                renderer.material.SetTexture("_BaseMap", _output);
        }

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            if (_outputSprite != null)
                Destroy(_outputSprite);

            _outputSprite = Sprite.Create(
                _output,
                new Rect(0f, 0f, _width, _height),
                new Vector2(0.5f, 0.5f),
                100f);
            spriteRenderer.sprite = _outputSprite;
        }
    }

    Color[] ReadPixels(Texture2D texture)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(texture, temporary);
        RenderTexture.active = temporary;

        Texture2D readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
        readable.Apply(false);

        Color[] pixels = readable.GetPixels();
        Destroy(readable);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);
        return pixels;
    }

    void OnDestroy()
    {
        if (_output != null)
            Destroy(_output);

        if (_outputSprite != null)
            Destroy(_outputSprite);
    }

    static class RobantsDropletErosion
    {
        const int AngleCount = 16;
        const float StepLength = 5f;

        static readonly float[] DistancesInv;
        static readonly int[] XOffsets;
        static readonly int[] YOffsets;
        static readonly int[][] XPaths;
        static readonly int[][] YPaths;

        static RobantsDropletErosion()
        {
            XOffsets = new int[AngleCount];
            YOffsets = new int[AngleCount];
            DistancesInv = new float[AngleCount];
            XPaths = new int[AngleCount][];
            YPaths = new int[AngleCount][];

            List<int> pathX = new List<int>();
            List<int> pathY = new List<int>();

            for (int i = 0; i < AngleCount; ++i)
            {
                float angle = Mathf.PI * 2f / AngleCount * (i + 0.5f);
                float dx = Mathf.Cos(angle) * StepLength;
                float dy = Mathf.Sin(angle) * StepLength;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                XOffsets[i] = Mathf.RoundToInt(dx);
                YOffsets[i] = Mathf.RoundToInt(dy);
                DistancesInv[i] = 1f;

                pathX.Clear();
                pathY.Clear();
                pathX.Add(0);
                pathY.Add(0);

                for (int step = 0; step < 100; ++step)
                {
                    int xStep = Mathf.RoundToInt(Mathf.Cos(angle) * distance / 99f * step);
                    int yStep = Mathf.RoundToInt(Mathf.Sin(angle) * distance / 99f * step);
                    if (xStep != pathX[pathX.Count - 1] || yStep != pathY[pathY.Count - 1])
                    {
                        pathX.Add(xStep);
                        pathY.Add(yStep);
                    }
                }

                pathX.RemoveAt(0);
                pathY.RemoveAt(0);
                XPaths[i] = pathX.ToArray();
                YPaths[i] = pathY.ToArray();
            }
        }

        public static void TraceDroplets(
            int width,
            int height,
            float[] altitudeField,
            Color[] colorField,
            float[] flowOut,
            float[] sedimentOut,
            int seed,
            int dropCount,
            float strength,
            float dropletToImageBlend,
            float imageToDropletBlend)
        {
            int buffer = Mathf.Min(10, Mathf.Max(1, (Mathf.Min(width, height) - Mathf.CeilToInt(StepLength * 2f) - 2) / 2));

            int upperXBound = width - buffer;
            int upperYBound = height - buffer;
            if (upperXBound <= buffer || upperYBound <= buffer)
                return;

            System.Random random = new System.Random(seed);

            for (int drop = 0; drop < dropCount; ++drop)
            {
                int x = random.Next(buffer, upperXBound);
                int y = random.Next(buffer, upperYBound);
                TraceDroplet(width, height, altitudeField, colorField, flowOut, sedimentOut, x, y, random, strength, dropletToImageBlend, imageToDropletBlend, buffer);
            }
        }

        static void TraceDroplet(
            int width,
            int height,
            float[] altitudeField,
            Color[] colorField,
            float[] flowOut,
            float[] sedimentOut,
            int xStart,
            int yStart,
            System.Random random,
            float strength,
            float dropletToImageBlend,
            float imageToDropletBlend,
            int buffer)
        {
            int upperXBound = width - buffer;
            int upperYBound = height - buffer;
            int x = xStart;
            int y = yStart;
            float speed = 0f;
            float sediment = 0f;
            Color dropletColor = colorField[x + y * width];
            const int maxIterations = 512;

            for (int iteration = 0; iteration < maxIterations; ++iteration)
            {
                int index = x + y * width;
                float currentAltitude = altitudeField[index];
                float downSlopeBest = float.MinValue;
                float downSlopeSecondBest = float.MinValue;
                int bestIndex = -1;
                int secondBestIndex = -1;

                for (int i = 0; i < AngleCount; ++i)
                {
                    int xOther = x + XOffsets[i];
                    int yOther = y + YOffsets[i];
                    if (xOther < 1 || yOther < 1 || xOther >= width - 1 || yOther >= height - 1)
                        continue;

                    float otherAltitude = altitudeField[xOther + yOther * width];
                    float downSlope = (currentAltitude - otherAltitude) * DistancesInv[i];
                    if (downSlope > downSlopeBest)
                    {
                        secondBestIndex = bestIndex;
                        downSlopeSecondBest = downSlopeBest;
                        bestIndex = i;
                        downSlopeBest = downSlope;
                    }
                    else if (downSlope > downSlopeSecondBest)
                    {
                        secondBestIndex = i;
                        downSlopeSecondBest = downSlope;
                    }
                }

                if (downSlopeSecondBest > 0f && random.NextDouble() * (downSlopeBest + downSlopeSecondBest) < downSlopeSecondBest)
                {
                    downSlopeBest = downSlopeSecondBest;
                    bestIndex = secondBestIndex;
                }

                if (bestIndex < 0 || downSlopeBest <= 0f)
                {
                    DepositAt(index, width, height, altitudeField, sedimentOut, sediment);
                    return;
                }

                int bestIndexOffset = NextChoiceProb(random, random.Next(0, 2) == 0 ? -1 : 1, 0, 0.2f);
                bestIndex += bestIndexOffset;
                if (bestIndex < 0)
                    bestIndex += AngleCount;
                else if (bestIndex >= AngleCount)
                    bestIndex -= AngleCount;

                int[] dxPath = XPaths[bestIndex];
                int[] dyPath = YPaths[bestIndex];
                speed *= 0.05f;
                speed += Mathf.Min(downSlopeBest, 16f) * 4f;

                float erode = (speed - sediment) * 0.1f * strength;
                float erodePerTile = erode / dxPath.Length;

                for (int step = 0; step < dxPath.Length; ++step)
                {
                    int xNew = x + dxPath[step];
                    int yNew = y + dyPath[step];
                    if (xNew < 1 || yNew < 1 || xNew >= width - 1 || yNew >= height - 1)
                    {
                        DepositAt(index, width, height, altitudeField, sedimentOut, sediment);
                        return;
                    }

                    int indexNew = xNew + yNew * width;
                    float targetAltitude = currentAltitude - downSlopeBest * (step + 1f) / dxPath.Length;
                    float altitude = altitudeField[indexNew];
                    altitudeField[indexNew] = 0.03f * targetAltitude + 0.97f * altitude - erodePerTile;

                    float water = Mathf.Abs(erodePerTile);
                    flowOut[indexNew] += water;

                    Color imageColor = colorField[indexNew];
                    colorField[indexNew] = Color.Lerp(imageColor, dropletColor, dropletToImageBlend);
                    dropletColor = Color.Lerp(dropletColor, imageColor, imageToDropletBlend);

                    if (erodePerTile > 0f)
                    {
                        sediment += erodePerTile;
                    }
                    else if (erodePerTile < 0f && sediment > 0.000001f)
                    {
                        float depositAmount = Mathf.Min(-erodePerTile, sediment);
                        sediment -= depositAmount;
                        sedimentOut[indexNew] += depositAmount;
                    }
                }

                x += XOffsets[bestIndex];
                y += YOffsets[bestIndex];

                if (x < buffer || y < buffer || x >= upperXBound || y >= upperYBound)
                {
                    DepositAt(index, width, height, altitudeField, sedimentOut, sediment);
                    return;
                }
            }
        }

        static void DepositAt(
            int index,
            int width,
            int height,
            float[] altitudeField,
            float[] sedimentOut,
            float sediment)
        {
            if (sediment <= 0.000001f)
                return;

            float center = sediment * 0.5f;
            float arm = sediment * 0.125f;
            float totalWeight = 0f;

            AddWeight(index, center);
            AddWeight(index + 1, arm);
            AddWeight(index - 1, arm);
            AddWeight(index + width, arm);
            AddWeight(index - width, arm);

            if (totalWeight <= 0f)
                return;

            Deposit(index, center);
            Deposit(index + 1, arm);
            Deposit(index - 1, arm);
            Deposit(index + width, arm);
            Deposit(index - width, arm);

            void AddWeight(int depositIndex, float amount)
            {
                if (depositIndex < 0 || depositIndex >= altitudeField.Length)
                    return;

                totalWeight += amount;
            }

            void Deposit(int depositIndex, float amount)
            {
                if (depositIndex < 0 || depositIndex >= altitudeField.Length)
                    return;

                altitudeField[depositIndex] += amount;
                sedimentOut[depositIndex] += amount;
            }
        }

        static int NextChoiceProb(System.Random random, int choice, int fallback, float probability)
        {
            return random.NextDouble() < probability ? choice : fallback;
        }
    }
}
