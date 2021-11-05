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
		public int OutScatteringPointsNum;
		public float DensityScale;
		public float AtmosphereRadius;
		public float PlanetRadius;
		//public int numOutScatteringSteps;
		//public float atmosphereRadius;
		////public float avgDensityHeight01;
		//public float densityFalloff;

		//private const float planetRadius = 1.0f;
		private const float eps = 0.0001f;
		//private const float skinWidth = planetRadius / 1000.0f;

		public void Execute(int index)
		{
			float2 uv = new float2(index % textureSize, index / textureSize) / (float)textureSize;
			float height01 = uv.y;
			float y = -2 * uv.x + 1;
			float x = math.sin(math.acos(y));
			float2 dir = new float2(x, y);

			float2 inPoint = new float2(0, math.lerp(PlanetRadius, AtmosphereRadius, height01));
			float2 intersection = GetRaySphereIntersection(float3.zero, new float3(inPoint, 0), new float3(dir, 0), AtmosphereRadius);
			float dstThroughAtmosphere = intersection.y - intersection.x;
			float outScattering = CalculateOpticalDepth(inPoint, dir, dstThroughAtmosphere);

			byte value = (byte)(math.saturate(outScattering) * 255);
			TextureData[index] = new Color32(value, 0, 0, 255);
		}

		float2 GetRaySphereIntersection(float3 sphereCenter, float3 rayOrigin, float3 rayDir, float radius)
		{
			const float MAX = 1e9f;

			float3 p = rayOrigin - sphereCenter;
			float b = math.dot(p, rayDir);
			float c = math.dot(p, p) - radius * radius;

			float d = b * b - c;
			if (d < 0.0)
			{
				return new float2(MAX, -MAX);
			}

			d = math.sqrt(d);

			return new float2(math.max(0, -b - d), -b + d);
		}

		float CalculateDensity(float2 p)
		{
			float heightAboveSurface = math.length(p) - PlanetRadius;
			float height01 = heightAboveSurface / (AtmosphereRadius - PlanetRadius);
			return math.exp(-height01 * DensityScale) * (1 - height01);
		}

		float CalculateOpticalDepth(float2 p, float2 rayDir, float rayLength)
		{
			float2 q = p + rayDir * rayLength;
			float2 step = (q - p) / (float)OutScatteringPointsNum;
			float2 v = p + step * 0.5f;

			float sum = 0.0f;
			for (int i = 0; i < OutScatteringPointsNum; i++)
			{
				sum += CalculateDensity(v);
				v += step;
			}

			sum *= math.length(step);
			return sum;
		}

		//public void Execute(int index)
		//{
		//	float2 uv = new float2(index % textureSize, index / textureSize) / (float)textureSize;
		//	float height01 = uv.y;
		//	//float angle = uv.x * math.PI;
		//	//angle = (1-math.cos(angle))/2;
		//	//float2 dir = new float2(math.sin(angle), math.cos(angle));
		//	float y = -2 * uv.x + 1;
		//	float x = math.sin(math.acos(y));
		//	float2 dir = new float2(x, y);

		//	float2 inPoint = new float2(0, math.lerp(PlanetRadius, AtmosphereRadius, height01));
		//	float dstThroughAtmosphere = RaySphere(0, AtmosphereRadius, new float3(inPoint, 0), new float3(dir, 0)).y;
		//	//float2 outPoint = inPoint + dir * RaySphere(0, atmosphereRadius, new float3(inPoint, 0), new float3(dir, 0)).y;
		//	//float outScattering = CalculateOutScattering(inPoint, outPoint);
		//	float outScattering = OpticalDepth(inPoint + dir * eps, dir, dstThroughAtmosphere - eps * 2.0f);

		//	byte value = (byte)(outScattering * 255);
		//	TextureData[index] = new Color32(value, value, value, 255);
		//	//TextureData[index] = new Color32((byte)(uv.x * 255), (byte)(uv.y * 255), 0, 255);
		//}

		//// Returns vector (dstToSphere, dstThroughSphere)
		//// If ray origin is inside sphere, dstToSphere = 0
		//// If ray misses sphere, dstToSphere = maxValue; dstThroughSphere = 0
		//private float2 RaySphere(float3 sphereCentre, float sphereRadius, float3 rayOrigin, float3 rayDir)
		//{
		//	float3 offset = rayOrigin - sphereCentre;
		//	float a = 1; // Set to dot(rayDir, rayDir) if rayDir might not be normalized
		//	float b = 2 * math.dot(offset, rayDir);
		//	float c = math.dot(offset, offset) - sphereRadius * sphereRadius;
		//	float d = b * b - 4 * a * c; // Discriminant from quadratic formula

		//	// Number of intersections: 0 when d < 0; 1 when d = 0; 2 when d > 0
		//	if (d > 0)
		//	{
		//		float s = math.sqrt(d);
		//		float dstToSphereNear = math.max(0, (-b - s) / (2 * a));
		//		float dstToSphereFar = (-b + s) / (2 * a);

		//		// Ignore intersections that occur behind the ray
		//		if (dstToSphereFar >= 0)
		//		{
		//			return new float2(dstToSphereNear, dstToSphereFar - dstToSphereNear);
		//		}
		//	}
		//	// Ray did not intersect sphere
		//	return new float2(float.MaxValue, 0);
		//}

		//private float DensityAtPoint(float2 densitySamplePoint)
		//{
		//	float2 planetCentre = float2.zero;

		//	float heightAboveSurface = math.length(densitySamplePoint - planetCentre) - PlanetRadius;
		//	float height01 = heightAboveSurface / (AtmosphereRadius - PlanetRadius);
		//	float localDensity = math.exp(-height01 * DensityScale) * (1 - height01);
		//	return localDensity;
		//}

		//private float OpticalDepth(float2 rayOrigin, float2 rayDir, float rayLength)
		//{
		//	int numOpticalDepthPoints = OutScatteringPointsNum;

		//	float2 densitySamplePoint = rayOrigin;
		//	float stepSize = rayLength / (numOpticalDepthPoints - 1);
		//	float opticalDepth = 0;

		//	for (int i = 0; i < numOpticalDepthPoints; i++)
		//	{
		//		float localDensity = DensityAtPoint(densitySamplePoint);
		//		opticalDepth += localDensity * stepSize;
		//		densitySamplePoint += rayDir * stepSize;
		//	}
		//	return opticalDepth;
		//}


		//private float CalculateOutScattering(float2 inPoint, float2 outPoint)
		//{
		//	float lightTravelDst = math.length(outPoint - inPoint);
		//	float2 outScatterPoint = inPoint;
		//	float2 rayDir = (outPoint - inPoint) / lightTravelDst;
		//	float stepSize = (lightTravelDst - skinWidth) / (numOutScatteringSteps);

		//	float outScatterAmount = 0;

		//	for (int i = 0; i < numOutScatteringSteps; i++)
		//	{
		//		outScatterPoint += rayDir * stepSize;

		//		// height at planet surface = 0, at furthest extent of atmosphere = 1
		//		float height = math.length(outScatterPoint - 0) - planetRadius;

		//		float height01 = math.saturate(height / (atmosphereRadius - planetRadius));
		//		outScatterAmount += math.exp(-height01 * densityFalloff) * stepSize;

		//	}

		//	return outScatterAmount;
		//}
	}
}