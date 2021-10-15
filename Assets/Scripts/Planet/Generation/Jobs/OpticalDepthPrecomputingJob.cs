using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Planet.Generation.Jobs
{
	// TODO: Enable BurstCompile attribute
	[BurstCompile]
	public struct OpticalDepthPrecomputingJob : IJobParallelFor
	{
		public NativeArray<Color32> TextureData;
		public int textureSize;
		public int numOutScatteringSteps;
		public float atmosphereRadius;
		//public float avgDensityHeight01;
		public float densityFalloff;

		private const float planetRadius = 1.0f;
		private const float eps = 0.0001f;
		private const float skinWidth = planetRadius / 1000.0f;

		public void Execute(int index)
		{
			float2 uv = new float2(index % textureSize, index / textureSize) / (float)textureSize;
			float height01 = uv.y;
			//float angle = uv.x * math.PI;
			//angle = (1-math.cos(angle))/2;
			//float2 dir = new float2(math.sin(angle), math.cos(angle));
			float y = -2 * uv.x + 1;
			float x = math.sin(math.acos(y));
			float2 dir = new float2(x, y);

			float2 inPoint = new float2(0, math.lerp(planetRadius, atmosphereRadius, height01));
			float dstThroughAtmosphere = RaySphere(0, atmosphereRadius, new float3(inPoint, 0), new float3(dir, 0)).y;
			//float2 outPoint = inPoint + dir * RaySphere(0, atmosphereRadius, new float3(inPoint, 0), new float3(dir, 0)).y;
			//float outScattering = CalculateOutScattering(inPoint, outPoint);
			float outScattering = OpticalDepth(inPoint + dir * eps, dir, dstThroughAtmosphere - eps * 2.0f);

			byte value = (byte)(outScattering * 255);
			TextureData[index] = new Color32(value, value, value, 255);
			//TextureData[index] = (byte)(outScattering * 256);
		}

		// Returns vector (dstToSphere, dstThroughSphere)
		// If ray origin is inside sphere, dstToSphere = 0
		// If ray misses sphere, dstToSphere = maxValue; dstThroughSphere = 0
		private float2 RaySphere(float3 sphereCentre, float sphereRadius, float3 rayOrigin, float3 rayDir)
		{
			float3 offset = rayOrigin - sphereCentre;
			float a = 1; // Set to dot(rayDir, rayDir) if rayDir might not be normalized
			float b = 2 * math.dot(offset, rayDir);
			float c = math.dot(offset, offset) - sphereRadius * sphereRadius;
			float d = b * b - 4 * a * c; // Discriminant from quadratic formula

			// Number of intersections: 0 when d < 0; 1 when d = 0; 2 when d > 0
			if (d > 0)
			{
				float s = math.sqrt(d);
				float dstToSphereNear = math.max(0, (-b - s) / (2 * a));
				float dstToSphereFar = (-b + s) / (2 * a);

				// Ignore intersections that occur behind the ray
				if (dstToSphereFar >= 0)
				{
					return new float2(dstToSphereNear, dstToSphereFar - dstToSphereNear);
				}
			}
			// Ray did not intersect sphere
			return new float2(float.MaxValue, 0);
		}

		private float DensityAtPoint(float2 densitySamplePoint)
		{
			float2 planetCentre = float2.zero;

			float heightAboveSurface = math.length(densitySamplePoint - planetCentre) - planetRadius;
			float height01 = heightAboveSurface / (atmosphereRadius - planetRadius);
			float localDensity = math.exp(-height01 * densityFalloff) * (1 - height01);
			return localDensity;
		}

		private float OpticalDepth(float2 rayOrigin, float2 rayDir, float rayLength)
		{
			int numOpticalDepthPoints = numOutScatteringSteps;

			float2 densitySamplePoint = rayOrigin;
			float stepSize = rayLength / (numOpticalDepthPoints - 1);
			float opticalDepth = 0;

			for (int i = 0; i < numOpticalDepthPoints; i++)
			{
				float localDensity = DensityAtPoint(densitySamplePoint);
				opticalDepth += localDensity * stepSize;
				densitySamplePoint += rayDir * stepSize;
			}
			return opticalDepth;
		}


		private float CalculateOutScattering(float2 inPoint, float2 outPoint)
		{
			float lightTravelDst = math.length(outPoint - inPoint);
			float2 outScatterPoint = inPoint;
			float2 rayDir = (outPoint - inPoint) / lightTravelDst;
			float stepSize = (lightTravelDst - skinWidth) / (numOutScatteringSteps);

			float outScatterAmount = 0;

			for (int i = 0; i < numOutScatteringSteps; i++)
			{
				outScatterPoint += rayDir * stepSize;

				// height at planet surface = 0, at furthest extent of atmosphere = 1
				float height = math.length(outScatterPoint - 0) - planetRadius;

				float height01 = math.saturate(height / (atmosphereRadius - planetRadius));
				outScatterAmount += math.exp(-height01 * densityFalloff) * stepSize;

			}

			return outScatterAmount;
		}
	}
}