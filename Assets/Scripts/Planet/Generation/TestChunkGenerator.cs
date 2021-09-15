using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections;
using Planet.Noises;
using Planet.Generation.Jobs;

public class TestChunkGenerator : MonoBehaviour
{
	[SerializeField] private int iterations = 15;
	[SerializeField] private Material chunkMaterial;
	[SerializeField] private float chunkSize = 100;
	[SerializeField, Range(4, 250)] private int chunkResolution = 250;
	[SerializeField] private ValueDerivativeNoise valueDerivativeNoise;
	[SerializeField] private SimplexNoise simplexNoise;
	[SerializeField] private RidgedNoise ridgedNoise;

	private MeshRenderer meshRenderer;
	private MeshFilter meshFilter;

	private Mesh originalMesh;
	private NativeArray<float3> Vertices;
	private NativeArray<float3> Normals;
	private NativeArray<float4> Tangents;
	private NativeArray<float2> UVs;
	private NativeArray<int> Triangles;

	private NativeArray<float3> Tan1;
	private NativeArray<float3> Tan2;
	private NativeArray<int> MatchCounter;

	private void Start()
	{
		meshRenderer = GetComponent<MeshRenderer>();
		meshFilter = GetComponent<MeshFilter>();

		int verticesNumber = chunkResolution * chunkResolution;
		int trianglesNumber = (chunkResolution - 1) * (chunkResolution - 1) * 6;

		// Allocate resources
		Vertices = new NativeArray<float3>(verticesNumber, Allocator.Persistent);
		Normals = new NativeArray<float3>(verticesNumber, Allocator.Persistent);
		Tangents = new NativeArray<float4>(verticesNumber, Allocator.Persistent);
		UVs = new NativeArray<float2>(verticesNumber, Allocator.Persistent);
		Triangles = new NativeArray<int>(trianglesNumber, Allocator.Persistent);
		Tan1 = new NativeArray<float3>(verticesNumber, Allocator.Persistent);
		Tan2 = new NativeArray<float3>(verticesNumber, Allocator.Persistent);
		MatchCounter = new NativeArray<int>(verticesNumber, Allocator.Persistent);
	}

	private void OnDestroy()
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

	private void Update()
	{
		//if (Input.GetKeyDown(KeyCode.E)) DoTesting();
	}

	private void DoTesting()
	{
		List<float> generationTimes = new List<float>(iterations);

		//generationTimes.Clear();
		//for (int i = 0; i < iterations; i++)
		//{
		//	float start = Time.realtimeSinceStartup;
		//	originalMesh.Clear();
		//	originalMesh = PlaneMeshGenerator.Create(chunkSize, chunkResolution);
		//	GenerateLandNaive(originalMesh);
		//	float totalTime = (Time.realtimeSinceStartup - start);
		//	generationTimes.Add(totalTime);
		//}
		//Debug.Log($"Generation time naive: {generationTimes.Average() * 1000.0f} ms"); // ~700 ms

		//generationTimes.Clear();
		//for (int i = 0; i < iterations; i++)
		//{
		//	float start = Time.realtimeSinceStartup;
		//	//originalMesh.Clear();
		//	//originalMesh = null;
		//	originalMesh = GenerateLand3(chunkSize, chunkResolution, originalMesh);
		//	float totalTime = (Time.realtimeSinceStartup - start);
		//	generationTimes.Add(totalTime);
		//}
		//Debug.Log($"Generation time3: {generationTimes.Average() * 1000.0f} ms");

		originalMesh = GenerateLand3(chunkSize, chunkResolution, originalMesh);
		//originalMesh = PlaneMeshGenerator.Create(chunkSize, chunkResolution);
		//originalMesh = PlaneMeshGenerator.PlaneTest(chunkSize, chunkResolution);

		meshFilter.sharedMesh = null;
		meshFilter.sharedMesh = originalMesh;
		meshRenderer.sharedMaterial = null;
		meshRenderer.sharedMaterial = chunkMaterial;
	}

	void GenerateLandNaive(Mesh mesh)
	{
		var verticesArray = mesh.vertices;

		float4x4 ChunkToPlanet = Helper.Matrix4x4ToFloat4x4(Matrix4x4.identity);
		float4x4 PlanetToChunk = Helper.Matrix4x4ToFloat4x4(Matrix4x4.identity);

		float4 up = new float4(0, 1, 0, 1);

		for (int i = 0; i < verticesArray.Length; i++)
		{
			var inVertex = verticesArray[i];
			float4 tempFloat4 = new float4(inVertex.x, inVertex.y, inVertex.z, 1.0f);
			float4 ctpPos = math.mul(ChunkToPlanet, tempFloat4);

			const float frequency = 0.1f;
			const float amplitude = 5f;

			//float y = 1 * 5f;
			float y = noise.cnoise(ctpPos.xyz * frequency) * amplitude;
			ctpPos += up * y;
			ctpPos.w = 1.0f;

			var outVertex = math.mul(PlanetToChunk, ctpPos);
			verticesArray[i] = new Vector3(outVertex.x, outVertex.y, outVertex.z);
		}

		// Assign new vertices to the mesh
		mesh.vertices = verticesArray;
		// Recalculate bounds and normals
		mesh.RecalculateBounds();
		mesh.RecalculateNormals();
		mesh.RecalculateTangents();
	}

	private Mesh GenerateLand3(float meshSize, int meshResolution, Mesh mesh = null)
	{
		if (mesh is null) mesh = new Mesh();
		mesh.Clear();

		const int batchCount = 250;
		int facesNumber = (meshResolution - 1) * (meshResolution - 1);
		int verticesNumber = meshResolution * meshResolution;
		int indicesNumber = facesNumber * 6;

		var planetTRS = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);
		var chunkTRS = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
		var chunkToPlanet = planetTRS.inverse * chunkTRS;
		var planetToChunk = chunkTRS.inverse * planetTRS;

		// Schedule and complete the jobs
		var job1 = new VerticesAndUVsGenerationJob()
		{
			Vertices = Vertices,
			Normals = Normals,
			UVs = UVs,
			MeshResolution = meshResolution,
			MeshSize = meshSize,
			Radius = meshSize,
			DoubledInversedRadius = 1.0f / meshSize * 2.0f,
			ChunkToPlanet = Helper.Matrix4x4ToFloat4x4(chunkToPlanet),
			PlanetToChunk = Helper.Matrix4x4ToFloat4x4(planetToChunk),
			ValueDerivativeNoise = valueDerivativeNoise,
			SimplexNoise = simplexNoise,
			RidgedNoise = ridgedNoise,
		};

		var jobHandle1 = job1.Schedule(verticesNumber, batchCount);
		jobHandle1.Complete();

		var tangentsJob1 = new ChunkSurfaceCreationJob
		{
			Vertices = Vertices,
			Normals = Normals,
			MatchCounter = MatchCounter,
			Triangles = Triangles,
			MeshResolution = meshResolution,
			FacesNumber = facesNumber,
			UVs = UVs,
			Tan1 = Tan1,
			Tan2 = Tan2
		};
		var tangentsJobHandle1 = tangentsJob1.Schedule(indicesNumber, batchCount);

		var tangentsJob2 = new NormalsAndTangentsCalculationJob
		{
			Tangents = Tangents,
			Normals = Normals,
			MatchCounter = MatchCounter,
			Tan1 = Tan1,
			Tan2 = Tan2
		};
		var tangentsJobHandle2 = tangentsJob2.Schedule(verticesNumber, batchCount, tangentsJobHandle1);
		tangentsJobHandle2.Complete();

		// Assign the generated data to the mesh
		mesh.SetVertices(Vertices);
		mesh.SetUVs(0, UVs);
		mesh.SetIndices(Triangles, MeshTopology.Triangles, 0, false);
		//mesh.SetNormals(Gradients);
		mesh.SetNormals(Normals);
		mesh.SetTangents(Tangents);

		// Recalculate bounds and normals
		mesh.RecalculateBounds();
		//mesh.RecalculateNormals();
		//mesh.RecalculateTangents(); 

		return mesh;
	}
}