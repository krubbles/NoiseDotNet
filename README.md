# NoiseDotNet
NoiseDotNet is a coherent noise library. It is 
- Extremely optimized (w/ SIMD acceleration)
- Lightweight (single-file code you can drop into your project)
- Compatible with both CoreCLR and Unity

It supports 4 noise functions:
- GradientNoise2D 
- GradientNoise3D
- CellularNoise2D
- CellularNoise3D

By default the Gradient noise calls use the Quadratic noise algorithm, which is a modified version of Perlin noise with improved quality. You can swap it to use Perlin noise by removing the `#define Quadratic` statement at the top of the Noise.cs file. 

Performance data:

| Noise Type x Impl.     | NoiseDotNet (CoreCLR) | NoiseDotNet (Unity) | FastNoise2 (Clang) |
| Gradient2D (quadratic) | 1.35                  | 1.37                | N/A                |
| Gradient2D (perlin)    | 1.14                  | 1.11                | 1.62               |
| Gradient3D (quadratic) | 2.97                  | 2.39                | N/A                |
| Gradient3D (perlin)    | 2.57                  | 1.94                | 3.93               |
| Cellular2D             | 2.67                  | 2.13                | 7.29               |
| Cellular3D             | 16.3                  | 9.32                | 22.7               |
