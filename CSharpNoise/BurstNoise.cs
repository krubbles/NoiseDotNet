#if UNITY_2017_1_OR_NEWER
using System.Runtime.CompilerServices;
namespace CSharpNoise
{

	public static class BurstNoise
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe float QuadraticNoise(float x, float y, int seed)
		{
			float xFloor = UnityEngine.Mathf.Floor(x);
			float yFloor = UnityEngine.Mathf.Floor(y);
			int ix = (int)xFloor;
			int iy = (int)yFloor;
			float fx = x - xFloor;
			float fy = y - yFloor;

			const int ConstX = 180601904, ConstY = 174181987, ConstXOR = 203663684;

			int llHash = ix * ConstX + iy * ConstY + seed;
			int lrHash = llHash + ConstX;
			int ulHash = llHash + ConstY;
			int urHash = llHash + ConstX + ConstY;

			llHash *= llHash ^ ConstXOR;
			lrHash *= lrHash ^ ConstXOR;
			ulHash *= ulHash ^ ConstXOR;
			urHash *= urHash ^ ConstXOR;

			const int GradAndMask = unchecked((int)0b11000000001100000000100000000111);
			const int GradOrMask = unchecked((int)0b00011111100001111111001111101000);

			llHash = (llHash & GradAndMask) | GradOrMask;
			lrHash = (lrHash & GradAndMask) | GradOrMask;
			ulHash = (ulHash & GradAndMask) | GradOrMask;
			urHash = (urHash & GradAndMask) | GradOrMask;

			float fxm1 = fx - 1;
			float fym1 = fy - 1;

			const int GradShift1 = 1, GradShift2 = 20, GradShift3 = 11;

			float llGrad = (llHash < 0 ? fx : fy) * ReinterpI2F(llHash << GradShift1) + (llHash < 0 ? fy : fx) * ReinterpI2F(llHash << GradShift2);
			float lrGrad = (lrHash < 0 ? fxm1 : fy) * ReinterpI2F(lrHash << GradShift1) + (lrHash < 0 ? fy : fxm1) * ReinterpI2F(lrHash << GradShift2);
			float ulGrad = (ulHash < 0 ? fx : fym1) * ReinterpI2F(ulHash << GradShift1) + (ulHash < 0 ? fym1 : fx) * ReinterpI2F(ulHash << GradShift2);
			float urGrad = (urHash < 0 ? fxm1 : fym1) * ReinterpI2F(urHash << GradShift1) + (urHash < 0 ? fym1 : fxm1) * ReinterpI2F(urHash << GradShift2);

			// this is the quadratic part. Removing this gives you pure Perlin Noise.    
			llGrad += llGrad * llGrad * ReinterpI2F(llHash << GradShift3);
			lrGrad += lrGrad * lrGrad * ReinterpI2F(lrHash << GradShift3);
			ulGrad += ulGrad * ulGrad * ReinterpI2F(ulHash << GradShift3);
			urGrad += urGrad * urGrad * ReinterpI2F(urHash << GradShift3);

			float sx = fx * fx * fx * ((6f * fx - 15f) * fx + 10f);
			float sy = fy * fy * fy * ((6f * fy - 15f) * fy + 10f);

			float lLerp = llGrad + (lrGrad - llGrad) * sx;
			float uLerp = ulGrad + (urGrad - ulGrad) * sx;
			float result = lLerp + (uLerp - lLerp) * sy;
			return result;
		}
	}
}
#endif
