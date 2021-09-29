using Planet.Generation.Jobs.Helpers;
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
		public FunctionPointer<BetterNormalizationHelper.BetterNormalizeDelegate> BetterNormalize;
		public FunctionPointer<VertexGenerationHelper.GenerateVertexDelegate> GenerateVertex;

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

			GenerateVertex.Invoke(out float3 vertexPos, BetterNormalize, xPos, zPos, ref ChunkToPlanet, ref PlanetToChunk,
						ref RidgedNoise, Radius, DoubledInversedRadius);

			Vertices[index] = vertexPos;
			//Vertices[index] = math.mul(PlanetToChunk, newVertex).xyz;
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
	}
}