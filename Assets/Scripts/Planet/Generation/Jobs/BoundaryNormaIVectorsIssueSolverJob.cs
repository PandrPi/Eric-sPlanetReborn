using Planet.Generation.Jobs.Helpers;
using Planet.Noises;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Planet.Generation.Jobs
{
	/// <summary>
	/// Solves the issue with incorrect calculation of boundary normal vectors
	/// </summary>
	//[BurstCompile]
	struct BoundaryNormaIVectorsIssueSolverJob : IJob
	{
		public FunctionPointer<BetterNormalizationHelper.BetterNormalizeDelegate> BetterNormalize;

		public NativeArray<float3> Vertices;
		public NativeArray<float3> BoundaryVertices;
		public NativeArray<int> BoundaryTriangles;
		public NativeArray<float3> Normals;
		public NativeHashMap<int, int> GridIndexToActualIndex;

		[ReadOnly] public float4x4 ChunkToPlanet;
		[ReadOnly] public float4x4 PlanetToChunk;
		[ReadOnly] public float MeshSize;
		[ReadOnly] public float Radius;
		[ReadOnly] public float DoubledInversedRadius;
		[ReadOnly] public int MeshResolution;
		[ReadOnly] public ValueDerivativeNoise ValueDerivativeNoise;
		[ReadOnly] public SimplexNoise SimplexNoise;
		[ReadOnly] public RidgedNoise RidgedNoise;

		private const float eps = 0.0005f;
		private static readonly float2 e = new float2(1.0f, -1.0f) * eps;

		public void Execute()
		{
			int verticesLength = MeshResolution * MeshResolution;
			int borderResolution = MeshResolution + 2;
			int borderResolutionSqr = borderResolution * borderResolution;
			int boundaryVerticesLength = MeshResolution * 4 + 4;

			float distanceBetweenTwoVertices = MeshSize / (MeshResolution - 1.0f);
			float borderMeshSize = MeshSize + distanceBetweenTwoVertices * 2.0f;

			int borderVertexIndex = -1;
			for (int z = 0; z < borderResolution; z++)
			{
				float zPos = ((float)z / (borderResolution - 1) - .5f) * borderMeshSize;
				for (int x = 0; x < borderResolution; x++)
				{
					if (IsBorderVertex(x, z, borderResolution) == true)
					{
						// Process only boundary vertices here

						float xPos = ((float)x / (borderResolution - 1) - .5f) * borderMeshSize;

						
						int gridIndex = x + z * borderResolution;
						GridIndexToActualIndex[gridIndex] = borderVertexIndex;
						AddVertex(GenerateVertex(xPos, zPos), borderVertexIndex);

						borderVertexIndex--;
					}
				}
			}

			for (int z = 0; z < borderResolution; z++)
			{
				for (int x = 0; x < borderResolution; x++)
				{
					if (IsBorderVertex(x, z, borderResolution) == true)
					{
						// Process only boundary vertices here

						int gridIndex = x + z * MeshResolution;
						float3 normal = new float3();

						if (x == 0)
						{

						}
					}

				}
			}
		}

		private bool IsBorderVertex(int x, int z, int meshResolution)
		{
			return z == 0 || z == meshResolution - 1 || x == 0 || x == meshResolution - 1;
		}

		private float3 GenerateVertex(float xPos, float zPos)
		{
			var boundarySourceVertex = new float4(xPos, 0, zPos, 1.0f);
			float4 newVertex = math.mul(ChunkToPlanet, boundarySourceVertex);

			float noise = RidgedNoise.GetValue(newVertex.xyz);
			var vertexInversed = newVertex.xyz * DoubledInversedRadius;
			BetterNormalize.Invoke(ref vertexInversed);
			newVertex.xyz = vertexInversed * (noise + Radius);

			return math.mul(PlanetToChunk, newVertex).xyz;
		}

		private void AddVertex(float3 vertexPosition, int vertexIndex)
		{
			if (vertexIndex < 0)
			{
				BoundaryVertices[-vertexIndex - 1] = vertexPosition;
			}
		}

		private float3 GetVertexByXZ(int x, int z)
		{
			int resultX = 0, resultZ = 0;
			if (x < 0)
			{

			}

			return x + z * MeshResolution;
		}

		private float3 CalculateNormal(float3 v1, float3 v2, float3 v3)
		{
			// Calculate triangle edges
			float3 edge1 = v2 - v1;
			float3 edge2 = v3 - v1;
			// Calculating cross product of the triangle's edges
			return math.normalize(math.cross(edge1, edge2));
		}
	}
}
