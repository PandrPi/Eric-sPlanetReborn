﻿using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Planet.Noises
{
	[Serializable]
	public struct RidgedNoise : IGenerationNoise
	{
		[SerializeField] public int Octaves;
		[SerializeField] public float Frequency;
		[SerializeField] public float Amplitude;
		[SerializeField] public float Lacunarity;
		[SerializeField] public float Gain;

		/// <summary>
		/// Calculates the Ridged noise value for the specified vector
		/// </summary>
		/// <param name="x">Vector for which the noise value should be calculated</param>
		/// <returns></returns>
		public float GetValue(float3 x)
		{
			return NoiseFBM(x, Octaves, Frequency, Amplitude, Lacunarity, Gain);
		}

		/// <summary>
		/// Calculates Fractional Brownian Motion of the Ridged noise for the specified vector
		/// </summary>
		/// <param name="x">Vector for which the noise value should be calculated</param>
		/// <param name="octaves">Number of octaves</param>
		/// <param name="frequency">Frequency modifier</param>
		/// <param name="amplitude">Amplitude modifier</param>
		/// <param name="lacunarity">Represents how much the noise frequency should increase with each new octave</param>
		/// <param name="gain">Represents how much the noise amplitude should increase with each new octave</param>
		/// <returns>Returns a fractal sum of Ridged noise values</returns>
		public static float NoiseFBM(
			float3 x,
			int octaves,
			float frequency,
			float amplitude,
			float lacunarity,
			float gain)
		{
			// Declare the result value
			float value = 0.0f;
			float weight = 1.0f;

			// Loop throught the octaves
			for (int i = 0; i < octaves; i++)
			{
				// Compute the noise value for the x vectore
				float n = 1.0f - math.abs(noise.snoise(x * frequency));
				n *= n;
				n *= weight;
				weight = n;
				// Apply the amplitude modifier to the computed noise value and add it to the result value
				value += n * amplitude;
				// Apply lacunarity and amplitude gain
				frequency *= lacunarity;
				amplitude *= gain;
			}

			return value;
		}
	}
}
