using Planet.Generation.Jobs;
using Planet.Generation.Jobs.Helpers;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Planet.Managers
{
	/// <summary>
	/// Generates a chunk based on chunk data instance
	/// </summary>
	public class ChunkGenerator : System.IDisposable
	{
		public bool IsFree { get; set; }
		public NativeArray<float3> Vertices { get; private set; }
		public NativeArray<float3> BoundaryVertices { get; private set; }
		public NativeArray<int> BoundaryTriangles { get; private set; }
		public NativeArray<float4> Colors { get; private set; }
		public NativeArray<float3> Normals { get; private set; }
		public NativeArray<float4> Tangents { get; private set; }
		public NativeArray<float2> UVs { get; private set; }
		public NativeArray<int> Triangles { get; private set; }

		public NativeArray<float3> Tan1 { get; private set; }
		public NativeArray<float3> Tan2 { get; private set; }

		public ChunkGenerator(int meshResolution)
		{
			IsFree = true;

			// Allocate resources
			AllocateNativeArrays(meshResolution);
		}

		/// <summary>
		/// Initializes NativeArray instances needed for chunk generation
		/// </summary>
		/// <param name="meshResolution">Represents the resolution of the generated mesh</param>
		private void AllocateNativeArrays(int meshResolution)
		{
			int verticesNumber = meshResolution * meshResolution;
			int trianglesNumber = (meshResolution - 1) * (meshResolution - 1) * 6;
			int boundaryVerticesNumber = meshResolution * 4 + 4;
			int boundaryTrianglesNumber = meshResolution * 24;

			Vertices = new NativeArray<float3>(verticesNumber, Allocator.Persistent);
			BoundaryVertices = new NativeArray<float3>(boundaryVerticesNumber, Allocator.Persistent);
			BoundaryTriangles = new NativeArray<int>(boundaryTrianglesNumber, Allocator.Persistent);
			Colors = new NativeArray<float4>(verticesNumber, Allocator.Persistent);
			Normals = new NativeArray<float3>(verticesNumber, Allocator.Persistent);
			Tangents = new NativeArray<float4>(verticesNumber, Allocator.Persistent);
			UVs = new NativeArray<float2>(verticesNumber, Allocator.Persistent);
			Triangles = new NativeArray<int>(trianglesNumber, Allocator.Persistent);
			Tan1 = new NativeArray<float3>(verticesNumber, Allocator.Persistent);
			Tan2 = new NativeArray<float3>(verticesNumber, Allocator.Persistent);
		}

		public void SetMeshResolution(int meshResolution)
		{
			// Release resources
			Dispose();

			// Reallocate resources
			AllocateNativeArrays(meshResolution);
		}

		/// <summary>
		/// Starts the chunk generation jobs filled with the specified chunk data
		/// </summary>
		/// <param name="chunkData">The ChunkData object that stores an actual data for generation</param>
		/// <returns>A JobHandle object</returns>
		public JobHandle StartChunkGeneration(ChunkData chunkData)
		{
			// Mark generator as unavailable
			IsFree = false;

			float meshSize = chunkData.meshSize;
			int meshResolution = chunkData.meshResolution;

			const int batchCount = 250;
			int facesNumber = (meshResolution - 1) * (meshResolution - 1);
			int verticesNumber = meshResolution * meshResolution;
			int indicesNumber = facesNumber * 6;
			// TODO: Check whether we need to replace meshSize with actual planet radius in the next line
			float doubledInversedRadius = 1.0f / meshSize * 2.0f;

			var planetTRS = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);
			var chunkTRS = chunkData.GetTransformationMatrix();
			var chunkToPlanet = Helper.Matrix4x4ToFloat4x4(planetTRS.inverse * chunkTRS);
			var planetToChunk = Helper.Matrix4x4ToFloat4x4(chunkTRS.inverse * planetTRS);

			// Schedule the jobs. All the jobs depends on each other so when the last job will be completed we can surely
			// say that all the previous jobs will be completed too.
			var job1 = new VerticesAndUVsGenerationJob()
			{
				BetterNormalize = BetterNormalizationHelper.BetterNormalizePointer,
				GenerateVertex = VertexGenerationHelper.GenerateVertexPointer,
				Vertices = Vertices,
				Normals = Normals,
				UVs = UVs,
				MeshResolution = meshResolution,
				MeshSize = meshSize,
				Radius = meshSize,
				DoubledInversedRadius = doubledInversedRadius,
				ChunkToPlanet = chunkToPlanet,
				PlanetToChunk = planetToChunk,
				ValueDerivativeNoise = ChunkGenerationManager.Instance.valueDerivativeNoise,
				SimplexNoise = ChunkGenerationManager.Instance.simplexNoise,
				RidgedNoise = ChunkGenerationManager.Instance.ridgedNoise,
			};

			var jobHandle1 = job1.Schedule(verticesNumber, batchCount);

			var job2 = new ChunkSurfaceCreationJob
			{
				Vertices = Vertices,
				Normals = Normals,
				Triangles = Triangles,
				MeshResolution = meshResolution,
				FacesNumber = facesNumber,
				UVs = UVs,
				Tan1 = Tan1,
				Tan2 = Tan2
			};
			var jobHandle2 = job2.Schedule(indicesNumber, batchCount, jobHandle1);

			var job3 = new BoundaryNormaIVectorsIssueSolverJob
			{
				BetterNormalize = BetterNormalizationHelper.BetterNormalizePointer,
				GenerateVertex = VertexGenerationHelper.GenerateVertexPointer,
				Vertices = Vertices,
				BoundaryVertices = BoundaryVertices,
				BoundaryTriangles = BoundaryTriangles,
				Normals = Normals,
				MeshResolution = meshResolution,
				MeshSize = meshSize,
				Radius = meshSize,
				DoubledInversedRadius = doubledInversedRadius,
				ChunkToPlanet = chunkToPlanet,
				PlanetToChunk = planetToChunk,
				ValueDerivativeNoise = ChunkGenerationManager.Instance.valueDerivativeNoise,
				SimplexNoise = ChunkGenerationManager.Instance.simplexNoise,
				RidgedNoise = ChunkGenerationManager.Instance.ridgedNoise,
			};
			job3.Initialize();
			var jobHandle3 = job3.Schedule(jobHandle2);

			var job4 = new NormalsAndTangentsCalculationJob
			{
				Tangents = Tangents,
				Normals = Normals,
				Tan1 = Tan1,
				Tan2 = Tan2
			};
			var jobHandle4 = job4.Schedule(verticesNumber, batchCount, jobHandle3);

			// Return the last jobHandle so we can check the completeness of the jobs later
			return jobHandle4;
		}

		/// <summary>
		/// Assigns the generated mesh data to the currentChunkData object
		/// </summary>
		public void AssignMeshData(ChunkData chunkData)
		{
			var mesh = chunkData.mesh;
			mesh.Clear();

			int meshResolution = chunkData.meshResolution;
			int verticesNumber = meshResolution * meshResolution;
			int trianglesNumber = (meshResolution - 1) * (meshResolution - 1) * 6;

			// Assign the generated data to the mesh
			mesh.SetVertices(Vertices, 0, verticesNumber);
			mesh.SetColors(Colors, 0, verticesNumber);
			mesh.SetUVs(0, UVs, 0, verticesNumber);
			mesh.SetIndices(Triangles, 0, trianglesNumber, MeshTopology.Triangles, 0, false);
			mesh.SetNormals(Normals, 0, verticesNumber);
			mesh.SetTangents(Tangents, 0, verticesNumber);

			// Recalculate bounds and normals
			mesh.RecalculateBounds();
			//mesh.RecalculateNormals();
			//mesh.RecalculateTangents();

			// Firstly, we have to assign the null to a current meshFilter's sharedMesh in order to release the old mesh
			chunkData.meshFilter.sharedMesh = null;
			// And assign the new mesh
			chunkData.meshFilter.sharedMesh = mesh;

			// Mark the generator as free
			IsFree = true;
		}

		// Releases all the allocated resources
		public void Dispose()
		{
			// Release resources
			Vertices.Dispose();
			BoundaryVertices.Dispose();
			BoundaryTriangles.Dispose();
			Colors.Dispose();
			Normals.Dispose();
			Tangents.Dispose();
			UVs.Dispose();
			Triangles.Dispose();
			Tan1.Dispose();
			Tan2.Dispose();
		}
	}
}
