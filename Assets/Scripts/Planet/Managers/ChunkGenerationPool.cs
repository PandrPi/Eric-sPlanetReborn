using Planet.Generation.Jobs;
using Planet.Noises;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class ChunkGenerationPool : MonoBehaviour
{
	public static ChunkGenerationPool Instance { get; private set; }

	[SerializeField, Range(2, 250)] private int maxChunkMeshResolution; 
	[SerializeField] private ValueDerivativeNoise valueDerivativeNoise;
	[SerializeField] private SimplexNoise simplexNoise;
	[SerializeField] private RidgedNoise ridgedNoise;

	private ChunkGenerationSlot generationSlot;
	private ChunkData currentChunkData;
	private JobHandle currentJobHandle;
	private readonly Queue<ChunkData> chunksToGenerate = new Queue<ChunkData>();

	private void Awake()
	{
		Instance = this;
		generationSlot = new ChunkGenerationSlot(maxChunkMeshResolution);
	}


	private void Update()
	{
		// Chunks are generated one by one. We use Jobs to generate a mesh data in parallel, not the chunks in parallel, so 
		// there is no case when several chunks are generated simultaneously 
		if (generationSlot.IsFree == true)
		{
			if (chunksToGenerate.Count > 0)
			{
				// Here we can start generating the chunk
				// Extract the cunk data from the queue
				currentChunkData = chunksToGenerate.Dequeue();
				// Start chunk generation
				currentJobHandle = StartChunkGeneration(currentChunkData);
				// Mark generation slot as unavailable
				generationSlot.IsFree = false;
			}
		}
		else
		{
			// Here we can ckeck whether our currentJobHandle is completed
			if (currentJobHandle.IsCompleted == true)
			{
				// Our generation jobs is completed but we have to manually call the Complete method
				currentJobHandle.Complete();
				AssignMeshData();

				// Mark the generation slot as free
				generationSlot.IsFree = true;
				// Remove reference to the current ChunkData object
				currentChunkData = null;
			}
		}
	}

	/// <summary>
	/// Adds the specified ChunkData object to the generation queue
	/// </summary>
	/// <param name="chunkData">The ChunkData object that stores an actual data for generation</param>
	public void AddToQueue(ChunkData chunkData)
	{
		chunksToGenerate.Enqueue(chunkData);
	}

	/// <summary>
	/// Starts the chunk generation jobs filled with the specified chunk data
	/// </summary>
	/// <param name="chunkData">The ChunkData object that stores an actual data for generation</param>
	/// <returns>A JobHandle object</returns>
	private JobHandle StartChunkGeneration(ChunkData chunkData)
	{
		float meshSize = chunkData.meshSize;
		int meshResolution = chunkData.meshResolution;

		const int batchCount = 250;
		int facesNumber = (meshResolution - 1) * (meshResolution - 1);
		int verticesNumber = meshResolution * meshResolution;
		int indicesNumber = facesNumber * 6;
		// TODO: Check whether we need to replace meshSize with actual planet radius in the next line
		float doubledInversedRadius = 1.0f / meshSize * 2.0f;

		var planetTRS = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);
		var chunkTRS = Matrix4x4.TRS(chunkData.chunkTRS.position, chunkData.chunkTRS.rotation, Vector3.one);
		var chunkToPlanet = planetTRS.inverse * chunkTRS;
		var planetToChunk = chunkTRS.inverse * planetTRS;

		// Schedule the jobs
		var job1 = new VerticesAndUVsGenerationJob()
		{
			Vertices = generationSlot.Vertices,
			Normals = generationSlot.Normals,
			UVs = generationSlot.UVs,
			MeshResolution = meshResolution,
			MeshSize = meshSize,
			Radius = meshSize,
			DoubledInversedRadius = doubledInversedRadius,
			ChunkToPlanet = Helper.Matrix4x4ToFloat4x4(chunkToPlanet),
			PlanetToChunk = Helper.Matrix4x4ToFloat4x4(planetToChunk),
			ValueDerivativeNoise = valueDerivativeNoise,
			SimplexNoise = simplexNoise,
			RidgedNoise = ridgedNoise,
		};

		var jobHandle1 = job1.Schedule(verticesNumber, batchCount);

		var job2 = new ChunkSurfaceCreationJob
		{
			Vertices = generationSlot.Vertices,
			Normals = generationSlot.Normals,
			MatchCounter = generationSlot.MatchCounter,
			Triangles = generationSlot.Triangles,
			MeshResolution = meshResolution,
			FacesNumber = facesNumber,
			UVs = generationSlot.UVs,
			Tan1 = generationSlot.Tan1,
			Tan2 = generationSlot.Tan2
		};
		var jobHandle2 = job2.Schedule(indicesNumber, batchCount, jobHandle1);

		var job3 = new NormalsAndTangentsCalculationJob
		{
			Tangents = generationSlot.Tangents,
			Normals = generationSlot.Normals,
			MatchCounter = generationSlot.MatchCounter,
			Tan1 = generationSlot.Tan1,
			Tan2 = generationSlot.Tan2
		};
		var jobHandle3 = job3.Schedule(verticesNumber, batchCount, jobHandle2);

		// Return the last jobHandle so we can check the completeness of the jobs later
		return jobHandle3;
	}

	/// <summary>
	/// Assigns the generated mesh data to the currentChunkData object
	/// </summary>
	private void AssignMeshData()
	{
		var mesh = currentChunkData.mesh;
		mesh.Clear();

		int meshResolution = currentChunkData.meshResolution;
		int verticesNumber = meshResolution * meshResolution;
		int trianglesNumber = (meshResolution - 1) * (meshResolution - 1) * 6;

		// Assign the generated data to the mesh
		mesh.SetVertices(generationSlot.Vertices, 0, verticesNumber);
		mesh.SetUVs(0, generationSlot.UVs, 0, verticesNumber);
		mesh.SetIndices(generationSlot.Triangles, 0, trianglesNumber, MeshTopology.Triangles, 0, false);
		mesh.SetNormals(generationSlot.Normals, 0, verticesNumber);
		mesh.SetTangents(generationSlot.Tangents, 0, verticesNumber);

		// Recalculate bounds and normals
		mesh.RecalculateBounds();
		//mesh.RecalculateNormals();
		//mesh.RecalculateTangents();

		// Firstly, we have to assign the null to a current meshFilter's sharedMesh in order to release the old mesh
		currentChunkData.meshFilter.sharedMesh = null;
		// And assign the new mesh
		currentChunkData.meshFilter.sharedMesh = mesh;
	}

	private void OnDestroy()
	{
		// Release resources here
		generationSlot.Dispose();
	}
}

public class ChunkGenerationSlot : System.IDisposable
{
	public bool IsFree { get; set; }
	public NativeArray<float3> Vertices { get; private set; }
	public NativeArray<float3> Normals { get; private set; }
	public NativeArray<float4> Tangents { get; private set; }
	public NativeArray<float2> UVs { get; private set; }
	public NativeArray<int> Triangles { get; private set; }

	public NativeArray<float3> Tan1 { get; private set; }
	public NativeArray<float3> Tan2 { get; private set; }
	public NativeArray<int> MatchCounter { get; private set; }

	public ChunkGenerationSlot(int meshResolution)
	{
		IsFree = true;

		int verticesNumber = meshResolution * meshResolution;
		int trianglesNumber = (meshResolution - 1) * (meshResolution - 1) * 6;

		// Allocate resources
		AllocateNativeArrays(verticesNumber, trianglesNumber);
	}

	private void AllocateNativeArrays(int verticesNumber, int trianglesNumber)
	{
		Vertices = new NativeArray<float3>(verticesNumber, Allocator.Persistent);
		Normals = new NativeArray<float3>(verticesNumber, Allocator.Persistent);
		Tangents = new NativeArray<float4>(verticesNumber, Allocator.Persistent);
		UVs = new NativeArray<float2>(verticesNumber, Allocator.Persistent);
		Triangles = new NativeArray<int>(trianglesNumber, Allocator.Persistent);
		Tan1 = new NativeArray<float3>(verticesNumber, Allocator.Persistent);
		Tan2 = new NativeArray<float3>(verticesNumber, Allocator.Persistent);
		MatchCounter = new NativeArray<int>(verticesNumber, Allocator.Persistent);
	}

	public void SetMeshResolution(int meshResolution)
	{
		// Release resources
		Dispose();

		// Reallocate resources
		int verticesNumber = meshResolution * meshResolution;
		int trianglesNumber = (meshResolution - 1) * (meshResolution - 1) * 6;
		AllocateNativeArrays(verticesNumber, trianglesNumber);
	}

	public void Dispose()
	{
		// Release resources
		Vertices.Dispose();
		Normals.Dispose();
		Tangents.Dispose();
		UVs.Dispose();
		Triangles.Dispose();
		Tan1.Dispose();
		Tan2.Dispose();
		MatchCounter.Dispose();
	}
}
