# NoiseDotNet
## Overview
NoiseDotNet is a coherent noise library written in C#. It is 
- Extremely optimized (w/ SIMD acceleration)
- Lightweight (single file you can drop into your project)
- Compatible with both CoreCLR and Unity

It supports 4 noise functions:
- GradientNoise2D 
- GradientNoise3D
- CellularNoise2D
- CellularNoise3D

By default `GradientNoise2D` and `GradientNoise3D` calls use the [Quadratic noise algorithm](https://milesoetzel.substack.com/p/introducing-quadratic-noise-a-better), which is a modified version of Perlin noise with improved quality. You can swap it to use Perlin noise by removing the `#define QUADRATIC` statement at the top of the NoiseImpls.cs file. 

## Preformance
Here are some preformance charts for NoiseDotNet, with FastNoise2 included as a baseline. Preformance is measured in nanoseconds per sample. 

Profiled on a Ryzen 9 6900HS, AVX2 compadibility level, Windows 11.

|                        | NoiseDotNet (CoreCLR) | NoiseDotNet (Unity) | FastNoise2 (Clang) |
| :--------------------- | --------------------: | ------------------: | -----------------: |
| Gradient2D (quadratic) | 1.35ns                | 1.37ns              | N/A                |
| Gradient2D (perlin)    | 1.14ns                | 1.11ns              | 1.62ns             |
| Gradient3D (quadratic) | 2.97ns                | 2.39ns              | N/A                |
| Gradient3D (perlin)    | 2.57ns                | 1.94ns              | 3.93ns             |
| Cellular2D             | 2.67ns                | 2.13ns              | 7.29ns             |
| Cellular3D             | 16.3ns                | 9.32ns              | 22.7ns             |

Profiled on an M4 Macbook Air. Will update with Unity results shortly. 

|                        | NoiseDotNet (CoreCLR) | NoiseDotNet (Unity) | FastNoise2 (Clang) |
| :--------------------- | --------------------: | ------------------: | -----------------: |
| Gradient2D (quadratic) | 1.71ns                | 1.49ns              | N/A                |
| Gradient2D (perlin)    | 1.32ns                | 2.90ns              | 2.55ns             |
| Gradient3D (quadratic) | 2.98ns                | 2.90ns              | N/A                |
| Gradient3D (perlin)    | 2.44ns                | Not tested yet.     | 5.03ns             |
| Cellular2D             | 2.78ns                | 4.06ns              | 9.40ns             |
| Cellular3D             | 10.89ns               | 14.2ns              | 25.8ns             |

## How to add to your project
Copy the NoiseDotNet folder into your project, and remove the .csproj file if you are using Unity. That's it!

## How to use
The noise functions are in the static `Noise` class in the `NoiseDotNet` namespace. Here is a short example of using the `Noise` class:

```csharp
int width = 16, height = 16;
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

// The result of the noise function calculation is now in the output buffer.
// output[i] = GradientNoise(xCoords[i], yCoords[i])
```

*Note: This repo is a newer version of my Icaria-Noise repo.*
