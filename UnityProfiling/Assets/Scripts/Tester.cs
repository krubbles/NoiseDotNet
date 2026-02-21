using UnityEngine;

public class Tester : MonoBehaviour
{
    public Texture2D OuptutTexture;

    void Start()
    {
        int width = 128, height = 128;
        int sampleCount = width * height;

        // here we create a 2D grid of points to evaluate the noise function on
        float[] xCoords = new float[sampleCount];
        float[] yCoords = new float[sampleCount];
        int index = 0;
        for (int y = 0; y < height; ++y)
            for (int x = 0; x < width; ++x)
            {
                xCoords[index] = x;
                yCoords[index] = y;
                index++;
            }

        // allocating a buffer to use as the output
        float[] output = new float[sampleCount];

        // settings for the noise function evaluation. Supports xFreq, yFreq, zFreq, seed, amplitude, amplitude2 
        // second amplitude is used by cellular noise which has 2 outputs, cell center dist is amplitude and cell edge dist is amplitude2
        // coordinates are multiplied by their corresponding frequencies before being passed into the noise function 
        // the outputs of the noise function are multipled by their corresponding amplitudes before being passed into the output buffer.
        // note that if the amplitude is zero (which it defaults to if you default construct the settings struct), the output will always be zero.
        // non-default constructors default amplitudes to 1. 
        // zFreq is ignored by 3D functions.
        NoiseDotNet.NoiseSettings settings = new(xFreq: 0.1f, yFreq: 0.1f, seed: 100);

        NoiseDotNet.Noise.GradientNoise2D(
            xCoords: xCoords,
            yCoords: yCoords,
            output: output,
            settings);
        
        Color32[] colors = new Color32[sampleCount];
        for (int i = 0; i < sampleCount; ++i)
        {
            byte value = (byte)(Mathf.Clamp01(output[i] * 0.5f + 0.5f) * 255.99f);
            colors[i] = new Color32(value, value, value, 255);
        }
        OuptutTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        OuptutTexture.SetPixels32(colors);
        OuptutTexture.Apply();
    }

    void OnDestroy()
    {
        if (OuptutTexture != null)
            Destroy(OuptutTexture);
    }
}
    