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
	[BurstCompile]
	struct BoundaryNormaIVectorsIssueSolverJob : IJob
	{
		public FunctionPointer<BetterNormalizationHelper.BetterNormalizeDelegate> BetterNormalize;
		public FunctionPointer<VertexGenerationHelper.GenerateVertexDelegate> GenerateVertex;

		public NativeArray<float3> Vertices;
		public NativeArray<float3> BoundaryVertices;
		public NativeArray<int> BoundaryTriangles;
		public NativeArray<float3> Normals;

		[ReadOnly] public float4x4 ChunkToPlanet;
		[ReadOnly] public float4x4 PlanetToChunk;
		[ReadOnly] public float MeshSize;
		[ReadOnly] public float Radius;
		[ReadOnly] public float DoubledInversedRadius;
		[ReadOnly] public int MeshResolution;
		[ReadOnly] public ValueDerivativeNoise ValueDerivativeNoise;
		[ReadOnly] public SimplexNoise SimplexNoise;
		[ReadOnly] public RidgedNoise RidgedNoise;

		private int borderTriangleIndex;
		private int verticesLength;
		private int borderMeshResolution;
		private int boundaryTrianglesLength;
		private float distanceBetweenTwoVertices;
		private float borderMeshSize;

		/// <summary>
		/// Initializes the additional variables needed for solving the normals issue
		/// </summary>
		public void Initialize()
		{
			borderTriangleIndex = 0;

			verticesLength = MeshResolution * MeshResolution;
			borderMeshResolution = MeshResolution + 2;
			boundaryTrianglesLength = MeshResolution * 24;
			distanceBetweenTwoVertices = MeshSize / (MeshResolution - 1.0f);
			borderMeshSize = MeshSize + distanceBetweenTwoVertices * 2.0f;
		}

		public void Execute()
		{
			// Generate boundary vertices position
			for (int z = 0; z < borderMeshResolution; z++)
			{
				float zPos = ((float)z / (borderMeshResolution - 1) - .5f) * borderMeshSize;
				for (int x = 0; x < borderMeshResolution; x++)
				{
					// We want to process only border positions
					if (IsBorderVertex(x, z) == false) continue;

					// Calculate border vertex position
					float xPos = ((float)x / (borderMeshResolution - 1) - .5f) * borderMeshSize;
					int vertexIndex = GetActualBorderVertexIndexByXZ(x, z);
					GenerateVertex.Invoke(out float3 vertexPos, BetterNormalize, xPos, zPos, ref ChunkToPlanet,
						ref PlanetToChunk, ref RidgedNoise, Radius, DoubledInversedRadius);
					BoundaryVertices[vertexIndex] = vertexPos;

					// Add border triangles
					if (x < borderMeshResolution - 1 && z < borderMeshResolution - 1)
					{
						AddTriangles(x, z);
					}
					if (x - 1 > 0 && z - 1 > 0)
					{
						int newX = x - 1;
						int newZ = z - 1;
						AddTriangles(newX, newZ);
					}
				}
			}

			// Calculate normal vectors
			for (int i = 0; i < boundaryTrianglesLength / 3; i++)
			{
				int normalTriangleIndex = i * 3;
				int vertexIndexA = BoundaryTriangles[normalTriangleIndex + 0];
				int vertexIndexB = BoundaryTriangles[normalTriangleIndex + 1];
				int vertexIndexC = BoundaryTriangles[normalTriangleIndex + 2];

				// We do not need to calculate normals if the current triangle consists only from boundary vertices
				if (vertexIndexA >= verticesLength && vertexIndexB >= verticesLength && vertexIndexC >= verticesLength) continue;

				float3 vertexA = GetVertex(vertexIndexA);
				float3 vertexB = GetVertex(vertexIndexB);
				float3 vertexC = GetVertex(vertexIndexC);

				float3 triangleNormal = CalculateNormal(vertexA, vertexB, vertexC);

				if (vertexIndexA < verticesLength) Normals[vertexIndexA] += triangleNormal;
				if (vertexIndexB < verticesLength) Normals[vertexIndexB] += triangleNormal;
				if (vertexIndexC < verticesLength) Normals[vertexIndexC] += triangleNormal;
			}
		}

		/// <summary>
		/// Returns True if the specified two-dimentional vertex index belongs to an boundary vertex
		/// </summary>
		/// <param name="x">X position</param>
		/// <param name="z">Z position</param>
		private bool IsBorderVertex(int x, int z)
		{
			return z == 0 || z == borderMeshResolution - 1 || x == 0 || x == borderMeshResolution - 1;
		}

		/// <summary>
		/// Returns a position vector of a usual vertex if the specified vertexIndex is smaller than total number of 
		/// usual vertices and a position vector of a boundary vertex otherwise
		/// </summary>
		/// <param name="vertexIndex">The index of the vertex to return</param>
		private float3 GetVertex(int vertexIndex)
		{
			if (vertexIndex < verticesLength)
			{
				return Vertices[vertexIndex];
			}
			else
			{
				return BoundaryVertices[vertexIndex - verticesLength];
			}
		}
		/// <summary>
		/// Adds a single triangle based on the specified a, b and c vertex indices
		/// </summary>
		private void AddTriangle(int a, int b, int c)
		{
			BoundaryTriangles[borderTriangleIndex] = a;
			BoundaryTriangles[borderTriangleIndex + 1] = b;
			BoundaryTriangles[borderTriangleIndex + 2] = c;
			borderTriangleIndex += 3;
		}

		/// <summary>
		/// Adds two triangles for a border mesh fragment based on the specified x and z parameters
		/// </summary>
		/// <param name="x">The X position</param>
		/// <param name="z">The Z position</param>
		private void AddTriangles(int x, int z)
		{
			int a = GetGridIndexByXZ(x, z);
			int b = GetGridIndexByXZ(x, z + 1);
			int c = GetGridIndexByXZ(x + 1, z);
			int d = GetGridIndexByXZ(x + 1, z + 1);
			AddTriangle(b, c, a);
			AddTriangle(b, d, c);
		}

		/// <summary>
		/// Calculates the index based on x + z * resolution formula
		/// </summary>
		private int GetGridIndexByXZ(int x, int z)
		{
			if (IsBorderVertex(x, z) == true)
			{
				return GetActualBorderVertexIndexByXZ(x, z) + verticesLength;
			}
			else
			{
				return (x - 1) + (z - 1) * MeshResolution;
			}
		}

		/// <summary>
		/// Converts the specified two-dimentional vertex index to an actual one-dimentional index for its further usage
		/// with 
		/// </summary>
		/// <param name="x">X position</param>
		/// <param name="z">Z position</param>
		/// <returns></returns>
		private int GetActualBorderVertexIndexByXZ(int x, int z)
		{
			if (z == 0)
			{
				return x;
			}
			else if (z == borderMeshResolution - 1)
			{
				int index = borderMeshResolution + (z - 1) * 2;
				index += x;

				return index;
			}
			else
			{
				int index = borderMeshResolution + (z - 1) * 2;
				index += x == 0 ? 0 : 1;

				return index;
			}
		}

		/// <summary>
		/// Calculates a normal vector from the specified points
		/// </summary>
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
