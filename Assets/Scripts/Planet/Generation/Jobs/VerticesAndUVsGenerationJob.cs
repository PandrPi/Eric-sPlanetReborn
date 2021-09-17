using Planet.Noises;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Planet.Generation.Jobs
{

	/// <summary>
	/// Creates a vertices grid based on MeshSize and MeshResolution. Applies a noise to vertices to create a chunk
	/// </summary>
	[BurstCompile]
	public struct VerticesAndUVsGenerationJob : IJobParallelFor
	{
		[WriteOnly] public NativeArray<float3> Vertices;
		[WriteOnly] public NativeArray<float3> Normals;
		[WriteOnly] public NativeArray<float2> UVs;
		[ReadOnly] public float4x4 ChunkToPlanet;
		[ReadOnly] public float4x4 PlanetToChunk;
		[ReadOnly] public float MeshSize;
		[ReadOnly] public float Radius;
		[ReadOnly] public float DoubledInversedRadius;
		[ReadOnly] public int MeshResolution;
		[ReadOnly] public ValueDerivativeNoise ValueDerivativeNoise;
		[ReadOnly] public SimplexNoise SimplexNoise;
		[ReadOnly] public RidgedNoise RidgedNoise;

		public void Execute(int index)
		{
			var indexX = index % MeshResolution;
			var indexZ = index / MeshResolution;

			float zPos = ((float)indexZ / (MeshResolution - 1) - .5f) * MeshSize;
			float xPos = ((float)indexX / (MeshResolution - 1) - .5f) * MeshSize;
			var sourceVertex = new float4(xPos, 0, zPos, 1.0f);

			// Convert sourceVertex to a planetary space
			float4 newVertex = math.mul(ChunkToPlanet, sourceVertex);

			//float noise = ValueDerivativeNoise.GetValueD(newVertex.xyz).x;
			//float noise = SimplexNoise.GetValue(newVertex.xyz);
			float noise = RidgedNoise.GetValue(newVertex.xyz);
			//float noise = DomainWrapping(newVertex.xyz);

			//newVertex = math.mul(PlanetToChunk, math.normalize(newVertex) * (noise + radius));
			newVertex.xyz = CustomNormalize(newVertex.xyz * DoubledInversedRadius) * (noise + Radius);

			Vertices[index] = math.mul(PlanetToChunk, newVertex).xyz;
			UVs[index] = new float2((float)indexX / (MeshResolution - 1), (float)indexZ / (MeshResolution - 1));
			Normals[index] = float3.zero;
		}

		private float DomainWrapping(float3 p)
		{
			float3 q = new float3(
				RidgedNoise.GetValue(p + float3.zero),
				0.0f,
				RidgedNoise.GetValue(p + new float3(5.2f, 1.3f, 6.7f)));

			float3 r = new float3(
				RidgedNoise.GetValue(p + 4.0f * q + new float3(1.7f, 9.2f, 2.3f)),
				0,
				RidgedNoise.GetValue(p + 4.0f * q + new float3(8.3f, 2.8f, 5.7f)));

			return RidgedNoise.GetValue(p + 4.0f * r);
		}

		/// <summary>
		/// Does a more uniform vector normalization
		/// </summary>
		/// <param name="v">The vector for normalization</param>
		/// <returns>The normalized float3 vector</returns>
		private float3 CustomNormalize(float3 v)
		{
			const float inverseTwo = 1.0f / 2.0f;
			const float inverseThree = 1.0f / 3.0f;
			float3 v2 = v * v;
			float3 s = new float3(
				math.sqrt(1f - (v2.y * inverseTwo) - (v2.z * inverseTwo) + (v2.y * v2.z * inverseThree)),
				math.sqrt(1f - (v2.x * inverseTwo) - (v2.z * inverseTwo) + (v2.x * v2.z * inverseThree)),
				math.sqrt(1f - (v2.x * inverseTwo) - (v2.y * inverseTwo) + (v2.x * v2.y * inverseThree)));

			return s * v;
		}
	}
}