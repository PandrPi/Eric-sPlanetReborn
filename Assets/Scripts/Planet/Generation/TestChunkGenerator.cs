using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using System.Linq;

public class TestChunkGenerator : MonoBehaviour
{
	[SerializeField] private int iterations = 15;
	[SerializeField] private bool useGeneratedNormals;
	[SerializeField] private Material chunkMaterial;

	private MeshRenderer meshRenderer;
	private MeshFilter meshFilter;

	private const float chunkSize = 100;
	private const int chunkResolution = 250;
	private const int verticesNumberConst = chunkResolution * chunkResolution;
	private const int trianglesNumberConst = (chunkResolution - 1) * (chunkResolution - 1) * 6;

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

		// Allocate resources
		Vertices = new NativeArray<float3>(verticesNumberConst, Allocator.Persistent);
		Normals = new NativeArray<float3>(verticesNumberConst, Allocator.Persistent);
		Tangents = new NativeArray<float4>(verticesNumberConst, Allocator.Persistent);
		UVs = new NativeArray<float2>(verticesNumberConst, Allocator.Persistent);
		Triangles = new NativeArray<int>(trianglesNumberConst, Allocator.Persistent);
		Tan1 = new NativeArray<float3>(verticesNumberConst, Allocator.Persistent);
		Tan2 = new NativeArray<float3>(verticesNumberConst, Allocator.Persistent);
		MatchCounter = new NativeArray<int>(verticesNumberConst, Allocator.Persistent);
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
		if (Input.GetKeyDown(KeyCode.E)) DoTesting();
	}

	private void DoTesting()
	{
		//Mesh originalMesh = new Mesh();
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
		//	originalMesh.Clear();
		//	originalMesh = PlaneMeshGenerator.Create(chunkSize, chunkResolution);
		//	GenerateLand1(originalMesh);
		//	float totalTime = (Time.realtimeSinceStartup - start);
		//	generationTimes.Add(totalTime);
		//}
		//Debug.Log($"Generation time1: {generationTimes.Average() * 1000.0f} ms");

		//generationTimes.Clear();
		//for (int i = 0; i < iterations; i++)
		//{
		//	float start = Time.realtimeSinceStartup;
		//	originalMesh.Clear();
		//	originalMesh = PlaneMeshGenerator.Create(chunkSize, chunkResolution);
		//	GenerateLand2(originalMesh);
		//	float totalTime = (Time.realtimeSinceStartup - start);
		//	generationTimes.Add(totalTime);
		//}
		//Debug.Log($"Generation time2: {generationTimes.Average() * 1000.0f} ms");

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

		float4x4 ChunkToPlanet = Matrix4x4ToFloat4x4(Matrix4x4.identity);
		float4x4 PlanetToChunk = Matrix4x4ToFloat4x4(Matrix4x4.identity);

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
	}

	private void GenerateLand1(Mesh mesh)
	{
		var verticesArray = mesh.vertices;
		var verticesNativeArray = new NativeArray<float3>(verticesArray.Length, Allocator.TempJob);

		// Convert vertices array to the native array of float3
		for (int i = 0; i < verticesArray.Length; i++)
		{
			var sourceVertex = verticesArray[i];
			verticesNativeArray[i] = new float3(sourceVertex.x, sourceVertex.y, sourceVertex.z);
		}

		var job = new LandGenerationJob1()
		{
			Vertices = verticesNativeArray,
			ChunkToPlanet = Matrix4x4ToFloat4x4(Matrix4x4.identity),
			PlanetToChunk = Matrix4x4ToFloat4x4(Matrix4x4.identity),
		};

		// Schedule and complete the job
		const int batchCount = 250;
		var jobHandle = job.Schedule(verticesArray.Length, batchCount);
		jobHandle.Complete();

		// Convert the modified vertices native array back to the usual vertices array
		for (int i = 0; i < verticesArray.Length; i++)
		{
			var vertex = verticesNativeArray[i];
			verticesArray[i] = new Vector3(vertex.x, vertex.y, vertex.z);
		}

		// Assign new vertices to the mesh
		mesh.vertices = verticesArray;
		// Recalculate bounds and normals
		mesh.RecalculateBounds();
		mesh.RecalculateNormals();

		// Release resources
		verticesNativeArray.Dispose();
	}

	private void GenerateLand2(Mesh mesh)
	{
		var verticesArray = mesh.vertices;
		var verticesNativeArray = new NativeArray<float3>(verticesArray.Length, Allocator.TempJob);

		// Convert vertices array to the native array of float3
		for (int i = 0; i < verticesArray.Length; i++)
		{
			var sourceVertex = verticesArray[i];
			verticesNativeArray[i] = new float3(sourceVertex.x, sourceVertex.y, sourceVertex.z);
		}

		var job = new LandGenerationJob1()
		{
			Vertices = verticesNativeArray,
			ChunkToPlanet = Matrix4x4ToFloat4x4(Matrix4x4.identity),
			PlanetToChunk = Matrix4x4ToFloat4x4(Matrix4x4.identity),
		};

		// Schedule and complete the job
		const int batchCount = 250;
		var jobHandle = job.Schedule(verticesArray.Length, batchCount);
		jobHandle.Complete();

		// Convert the modified vertices native array back to the usual vertices array
		for (int i = 0; i < verticesArray.Length; i++)
		{
			var vertex = verticesNativeArray[i];
			verticesArray[i] = new Vector3(vertex.x, vertex.y, vertex.z);
		}

		// Assign new vertices to the mesh
		mesh.vertices = verticesArray;
		// Recalculate bounds and normals
		mesh.RecalculateBounds();
		mesh.RecalculateNormals();

		// Release resources
		verticesNativeArray.Dispose();
	}

	private Mesh GenerateLand3(float meshSize, int meshResolution, Mesh mesh = null)
	{
		if (mesh is null) mesh = new Mesh();
		mesh.Clear();

		const int batchCount = 250;
		int facesNumber = (meshResolution - 1) * (meshResolution - 1);
		int verticesNumber = meshResolution * meshResolution;
		int indicesNumber = facesNumber * 6;

		var job1 = new PlaneCreationAndLandGenerationJob()
		{
			Vertices = Vertices,
			UVs = UVs,
			MeshResolution = meshResolution,
			MeshSize = meshSize,
			ChunkToPlanet = Matrix4x4ToFloat4x4(Matrix4x4.identity),
			PlanetToChunk = Matrix4x4ToFloat4x4(Matrix4x4.identity),
		};

		// Schedule and complete the jobs
		var jobHandle1 = job1.Schedule(verticesNumber, batchCount);
		jobHandle1.Complete();

		var job2 = new PlaneTriangulationJob
		{
			Triangles = Triangles,
			MeshResolution = meshResolution,
			FacesNumber = facesNumber
		};
		var jobHandle2 = job2.Schedule(indicesNumber, batchCount);
		jobHandle2.Complete();

		var normalsJob1 = new NormalsCalculationJob
		{
			Normals = Normals,
			MatchCounter = MatchCounter,
			Vertices = Vertices,
			Triangles = Triangles,
		};
		var normalsJobHandle1 = normalsJob1.Schedule(indicesNumber, batchCount);

		var normalsJob2 = new NormalsAveragingJob
		{
			MatchCounter = MatchCounter,
			Normals = Normals
		};
		var normalsJobHandle2 = normalsJob2.Schedule(verticesNumber, batchCount, normalsJobHandle1);
		normalsJobHandle2.Complete();

		var tangentsJob1 = new TangentsCalculationJob1
		{
			Vertices = Vertices,
			Triangles = Triangles,
			UVs = UVs,
			Tan1 = Tan1,
			Tan2 = Tan2
		};
		var tangentsJobHandle1 = tangentsJob1.Schedule(indicesNumber, batchCount);

		var tangentsJob2 = new TangentsCalculationJob2
		{
			Tangents = Tangents,
			Normals = Normals,
			Tan1 = Tan1,
			Tan2 = Tan2
		};
		var tangentsJobHandle2 = tangentsJob2.Schedule(verticesNumber, batchCount, tangentsJobHandle1);
		tangentsJobHandle2.Complete();

		// Assign the generated data to the mesh
		mesh.SetVertices(Vertices);
		mesh.SetUVs(0, UVs);
		mesh.SetIndices(Triangles, MeshTopology.Triangles, 0, false);
		mesh.SetNormals(Normals);
		mesh.SetTangents(Tangents);

		// Recalculate bounds and normals
		mesh.RecalculateBounds();
		//mesh.RecalculateNormals();
		//mesh.RecalculateTangents();

		return mesh;
	}

	private static float4x4 Matrix4x4ToFloat4x4(Matrix4x4 m)
	{
		return new float4x4(m.GetColumn(0), m.GetColumn(1), m.GetColumn(2), m.GetColumn(3));
	}
}

[BurstCompile]
public struct LandGenerationJob1 : IJobParallelFor
{
	public NativeArray<float3> Vertices;
	public float4x4 ChunkToPlanet;
	public float4x4 PlanetToChunk;

	private static readonly float4 up = new float4(0, 1, 0, 1);

	public void Execute(int index)
	{
		var sourceVertex = Vertices[index];
		float4 tempFloat4 = new float4(sourceVertex.x, sourceVertex.y, sourceVertex.z, 1.0f);
		float4 ctpPos = math.mul(ChunkToPlanet, tempFloat4);

		const float frequency = 0.1f;
		const float amplitude = 5f;

		//float y = 1 * 5f;
		float y = noise.cnoise(ctpPos.xyz * frequency) * amplitude;
		ctpPos += up * y;
		ctpPos.w = 1.0f;

		Vertices[index] = math.mul(PlanetToChunk, ctpPos).xyz;
	}
}

[BurstCompile]
public struct PlaneCreationAndLandGenerationJob : IJobParallelFor
{
	[WriteOnly] public NativeArray<float3> Vertices;
	[WriteOnly] public NativeArray<float2> UVs;
	[ReadOnly] public float4x4 ChunkToPlanet;
	[ReadOnly] public float4x4 PlanetToChunk;
	[ReadOnly] public float MeshSize;
	[ReadOnly] public int MeshResolution;

	private static readonly float4 up = new float4(0, 1, 0, 1);

	public void Execute(int index)
	{
		var indexX = index % MeshResolution;
		var indexZ = index / MeshResolution;

		float zPos = ((float)indexZ / (MeshResolution - 1) - .5f) * MeshSize;
		float xPos = ((float)indexX / (MeshResolution - 1) - .5f) * MeshSize;
		var sourceVertex = new float4(xPos, 0, zPos, 1.0f);

		float4 ctpPos = math.mul(ChunkToPlanet, sourceVertex);

		const float frequency = 0.1f;
		const float amplitude = 5f;

		//float y = 1 * 5f;
		float y = noise.snoise(ctpPos.xyz * frequency) * amplitude;
		ctpPos += up * y;
		ctpPos.w = 1.0f;
		ctpPos = math.mul(PlanetToChunk, ctpPos);

		Vertices[index] = ctpPos.xyz;
		UVs[index] = new float2((float)indexX / (MeshResolution - 1), (float)indexZ / (MeshResolution - 1));
	}
}

[BurstCompile]
public struct PlaneTriangulationJob : IJobParallelFor
{
	[WriteOnly] public NativeArray<int> Triangles;
	[ReadOnly] public int MeshResolution;
	[ReadOnly] public int FacesNumber;

	public void Execute(int index)
	{
		int face = index / 6;
		int i = face % (MeshResolution - 1) + (face / (MeshResolution - 1) * MeshResolution);

		if (index % 6 == 0) Triangles[index] = i + MeshResolution;
		if (index % 6 == 1) Triangles[index] = i + 1;
		if (index % 6 == 2) Triangles[index] = i;
		if (index % 6 == 3) Triangles[index] = i + MeshResolution;
		if (index % 6 == 4) Triangles[index] = i + MeshResolution + 1;
		if (index % 6 == 5) Triangles[index] = i + 1;
	}
}

[BurstCompile]
public struct NormalsCalculationJob : IJobParallelFor
{
	[NativeDisableParallelForRestriction] public NativeArray<int> MatchCounter;
	[NativeDisableParallelForRestriction] public NativeArray<float3> Normals;
	[NativeDisableParallelForRestriction] [ReadOnly] public NativeArray<int> Triangles;
	[NativeDisableParallelForRestriction] [ReadOnly] public NativeArray<float3> Vertices;

	public void Execute(int index)
	{
		if (index % 3 != 0) return;

		int i0 = Triangles[index + 0];
		int i1 = Triangles[index + 1];
		int i2 = Triangles[index + 2];

		// get the three vertices that make the faces
		float3 p1 = Vertices[i0];
		float3 p2 = Vertices[i1];
		float3 p3 = Vertices[i2];

		float3 v1 = p2 - p1;
		float3 v2 = p3 - p1;
		float3 normal = math.normalize(math.cross(v1, v2));

		// Store the face's normal for each of the vertices that make up the face.
		Normals[i0] += normal;
		Normals[i1] += normal;
		Normals[i2] += normal;

		MatchCounter[i0] += 1;
		MatchCounter[i1] += 1;
		MatchCounter[i2] += 1;
	}
}

[BurstCompile]
public struct NormalsAveragingJob : IJobParallelFor
{
	public NativeArray<float3> Normals;
	[ReadOnly] public NativeArray<int> MatchCounter;

	public void Execute(int index)
	{
		// Read the specific counter value
		int counter = MatchCounter[index];
		// Average and normalize the current normal vector
		if (counter > 0) Normals[index] = math.normalize(Normals[index] / (float)counter);
	}
}

[BurstCompile]
public struct TangentsCalculationJob1 : IJobParallelFor
{
	[NativeDisableParallelForRestriction] [ReadOnly] public NativeArray<int> Triangles;
	[NativeDisableParallelForRestriction] [ReadOnly] public NativeArray<float3> Vertices;
	[NativeDisableParallelForRestriction] [ReadOnly] public NativeArray<float2> UVs;
	[NativeDisableParallelForRestriction] public NativeArray<float3> Tan1;
	[NativeDisableParallelForRestriction] public NativeArray<float3> Tan2;

	public void Execute(int index)
	{
		if (index % 3 != 0) return;

		int i1 = Triangles[index + 0];
		int i2 = Triangles[index + 1];
		int i3 = Triangles[index + 2];

		float3 v1 = Vertices[i1];
		float3 v2 = Vertices[i2];
		float3 v3 = Vertices[i3];

		float2 w1 = UVs[i1];
		float2 w2 = UVs[i2];
		float2 w3 = UVs[i3];

		float x1 = v2.x - v1.x;
		float x2 = v3.x - v1.x;
		float y1 = v2.y - v1.y;
		float y2 = v3.y - v1.y;
		float z1 = v2.z - v1.z;
		float z2 = v3.z - v1.z;

		float s1 = w2.x - w1.x;
		float s2 = w3.x - w1.x;
		float t1 = w2.y - w1.y;
		float t2 = w3.y - w1.y;

		float r = 1.0f / (s1 * t2 - s2 * t1);

		float3 sdir = new float3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
		float3 tdir = new float3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);

		Tan1[i1] += sdir;
		Tan1[i2] += sdir;
		Tan1[i3] += sdir;

		Tan2[i1] += tdir;
		Tan2[i2] += tdir;
		Tan2[i3] += tdir;
	}
}

[BurstCompile]
public struct TangentsCalculationJob2 : IJobParallelFor
{
	[WriteOnly] public NativeArray<float4> Tangents;
	[ReadOnly] public NativeArray<float3> Normals;
	[ReadOnly] public NativeArray<float3> Tan1;
	[ReadOnly] public NativeArray<float3> Tan2;

	public void Execute(int index)
	{
		float3 n = Normals[index];
		float3 t = Tan1[index];

		Vector3 tmp = math.normalize(t - n * math.dot(n, t));
		var result = new float4
		{
			x = tmp.x,
			y = tmp.y,
			z = tmp.z,
			w = (math.dot(math.cross(n, t), Tan2[index]) < 0.0f) ? -1.0f : 1.0f
		};

		Tangents[index] = result;
	}
}