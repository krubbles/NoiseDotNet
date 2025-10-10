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

By default the Gradient noise calls use the Quadratic noise algorithm, which is a modified version of Perlin noise with improved quality. You can swap it to use Perlin noise by removing the `#define Quadratic` statement at the top of the Noise.cs file. 

## Preformance
Here is a preformance chart for NoiseDotNet, with FastNoise2 included as a baseline. 
Preformance is measured in nanoseconds per sample. Profiled on a Ryzen 9 6900HS.

|                        | NoiseDotNet (CoreCLR) | NoiseDotNet (Unity) | FastNoise2 (Clang) |
| :--------------------- | --------------------: | ------------------: | -----------------: |
| Gradient2D (quadratic) | 1.35                  | 1.37                | N/A                |
| Gradient2D (perlin)    | 1.14                  | 1.11                | 1.62               |
| Gradient3D (quadratic) | 2.97                  | 2.39                | N/A                |
| Gradient3D (perlin)    | 2.57                  | 1.94                | 3.93               |
| Cellular2D             | 2.67                  | 2.13                | 7.29               |
| Cellular3D             | 16.3                  | 9.32                | 22.7               |

## How to add to your project
Copy the file Noise.cs from the NoiseDotNet folder into your project. That's it!

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

NoiseDotNet.Noise.GradientNoise2D(
    xCoords: xCoords,
    yCoords: yCoords,
    output: output,
    xFreq: 0.1f, // x-coordinates are multiplied by this value before being used
    yFreq: 0.1f, // y-coordinates are multiplied by this value before being used
    amplitude: 1f, // the result of the noise function is multiplied by this value
    seed: 100);

// The result of the noise function calculations is now in the output buffer.
// output[i] = GradientNoise(xCoords[i], yCoords[i])
```
