#if NETCOREAPP
using System.Numerics;
#endif

namespace CSharpNoise
{
    public struct CoordinateGrid
    {
        public float xStart, yStart, zStart;
        public float xStep, yStep, zStep;
        public int width, height, depth;

        public CoordinateGrid(int width, int height, float xStart, float yStart, float xStep, float yStep) 
        {
            this.width = width;
            this.height = height;
            this.xStart = xStart;
            this.yStart = yStart;
            this.xStep = xStep;
            this.yStep = yStep;
            depth = 0;
            zStart = 0;
            zStep = 0;
        }

        public CoordinateGrid(int width, int height, int depth, float xStart, float yStart, float zStart, float xStep, float yStep, float zStep)
        {
            this.width = width;
            this.height = height;
            this.depth = depth;
            this.xStart = xStart;
            this.yStart = yStart;
            this.zStart = zStart;
            this.xStep = xStep;
            this.yStep = yStep;
            this.zStep = zStep;
        }
    }


    public static class Noise
	{
		static Noise()
		{
		}
	}
}