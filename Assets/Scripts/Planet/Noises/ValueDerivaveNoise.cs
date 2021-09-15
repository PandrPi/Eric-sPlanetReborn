using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Planet.Noises
{
	[Serializable]
	public struct ValueDerivativeNoise : IGenerationNoise
	{
		[SerializeField] public int Octaves;
		[SerializeField] public float Frequency;
		[SerializeField] public float Amplitude;
		[SerializeField] public float Lacunarity;
		[SerializeField] public float Gain;

		private static readonly float3x3 m3 = new float3x3(0.00f, 0.80f, 0.60f, -0.80f, 0.36f, -0.48f, -0.60f, -0.48f, 0.64f);
		private static readonly float3x3 m3i = new float3x3(0.00f, -0.80f, -0.60f, 0.80f, 0.36f, -0.48f, 0.60f, -0.48f, 0.64f);

		public float4 GetValueD(float3 x)
		{
			return NoiseFBMD(x, Octaves, Frequency, Amplitude, Lacunarity, Gain);
		}

		private static float Hash(float n) => math.frac(math.sin(n) * 753.5453123f);

		private static float4 NoiseD(float3 x)
		{
			float3 p = math.floor(x);
			float3 w = math.frac(x);

			float3 u = w * w * w * (w * (w * 6.0f - 15.0f) + 10.0f);
			float3 du = 30.0f * w * w * (w * (w - 2.0f) + 1.0f);

			float n = p.x + 317.0f * p.y + 157.0f * p.z;

			float a = Hash(n + 0.0f);
			float b = Hash(n + 1.0f);
			float c = Hash(n + 317.0f);
			float d = Hash(n + 318.0f);
			float e = Hash(n + 157.0f);
			float f = Hash(n + 158.0f);
			float g = Hash(n + 474.0f);
			float h = Hash(n + 475.0f);

			float k0 = a;
			float k1 = b - a;
			float k2 = c - a;
			float k3 = e - a;
			float k4 = a - b - c + d;
			float k5 = a - c - e + g;
			float k6 = a - b - e + f;
			float k7 = -a + b + c - d + e - f - g + h;

			float noiseValue = -1.0f + 2.0f * (k0 + k1 * u.x + k2 * u.y + k3 * u.z +
				k4 * u.x * u.y + k5 * u.y * u.z + k6 * u.z * u.x + k7 * u.x * u.y * u.z);

			return new float4(noiseValue, 2.0f * du * new float3(k1 + (k4 * u.y) + (k6 * u.z) + (k7 * u.y * u.z),
											  k2 + k5 * u.z + k4 * u.x + k7 * u.z * u.x,
											  k3 + k6 * u.x + k5 * u.y + k7 * u.x * u.y));
		}

		public static float4 NoiseFBMD(
			float3 x,
			int octaves,
			float frequency,
			float amplitude,
			float lacunarity,
			float gain)
		{
			////float f = 1.72f;
			////float amplitude = 0.5f;
			////float gain = 0.5f;
			//float value = 0.0f;
			//float b = 0.5f;
			//float3 derivative = new float3(0.0f);
			//x *= frequency;
			//float3x3 m = new float3x3(1.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 1.0f);
			//for (int i = 0; i < octaves; i++)
			//{
			//	float4 n = NoiseD(x);
			//	// accumulate derivatives
			//	derivative += b * math.mul(m, n.yzw);
			//	// accumulate values
			//	value += b * n.x / (1.0f + math.dot(derivative, derivative));
			//	b *= gain;
			//	x = frequency * math.mul(m3, x);
			//	m = frequency * math.mul(m3i, m);
			//}
			//return new float4(value * amplitude, derivative);

			float a = 0.0f;
			float b = 0.5f;
			float3 d = new float3(0.0f);
			x *= frequency;
			float3x3 m = new float3x3(1.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 1.0f);
			for (int i = 0; i < octaves; i++)
			{
				float4 n = NoiseD(x);
				d += b * math.mul(m, n.yzw);            // accumulate derivatives
				a += b * n.x / (1.0f + math.dot(d, d)); // accumulate values
				b *= gain;
				x = lacunarity * math.mul(m3, x);
				m = lacunarity * math.mul(m3i, m);
			}
			return new float4(a * amplitude, d);
		}

		[Obsolete("This method is not implemented for this type of noise. Use GetValueD method instead.", false)]
		public float GetValue(float3 x)
		{
			return 0.0f;
		}

		//[Obsolete("This method is not implemented for this type of noise. Use NoiseFBMD method instead.", false)]
		//public float NoiseFBM(float3 x, int octaves, float frequency, float amplitude, float lacunarity, float gain)
		//{
		//	return 0.0f;
		//}
	}
}
