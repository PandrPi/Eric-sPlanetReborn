using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;


namespace Planet.Generation.Jobs
{
	/// <summary>
	/// Creates triangles from the input vertices, resolution and faces number. Prepares the data for normals and 
	/// tangents calculation.
	/// </summary>
	[BurstCompile]
	public struct ChunkSurfaceCreationJob : IJobParallelFor
	{
		[NativeDisableParallelForRestriction] [ReadOnly] public NativeArray<float3> Vertices;
		[NativeDisableParallelForRestriction] [ReadOnly] public NativeArray<float2> UVs;
		[NativeDisableParallelForRestriction] public NativeArray<int> MatchCounter;
		[NativeDisableParallelForRestriction] public NativeArray<float3> Normals;
		[NativeDisableParallelForRestriction] public NativeArray<int> Triangles;
		[NativeDisableParallelForRestriction] public NativeArray<float3> Tan1;
		[NativeDisableParallelForRestriction] public NativeArray<float3> Tan2;
		[ReadOnly] public int MeshResolution;
		[ReadOnly] public int FacesNumber;

		public void Execute(int index)
		{
			if (index % 3 != 0) return;

			int face = index / 6;
			int i = face % (MeshResolution - 1) + (face / (MeshResolution - 1) * MeshResolution);

			// Construct the mesh triangles
			if (index % 6 == 0)
			{
				Triangles[index + 0] = i + MeshResolution;
				Triangles[index + 1] = i + 1;
				Triangles[index + 2] = i;
			}
			else if (index % 6 == 3)
			{
				Triangles[index + 0] = i + MeshResolution;
				Triangles[index + 1] = i + MeshResolution + 1;
				Triangles[index + 2] = i + 1;
			}

			// Read vertices indices
			int i0 = Triangles[index + 0];
			int i1 = Triangles[index + 1];
			int i2 = Triangles[index + 2];

			// Read vertices vectors
			float3 v1 = Vertices[i0];
			float3 v2 = Vertices[i1];
			float3 v3 = Vertices[i2];

			// Create the normal vector
			float3 edge1 = v2 - v1;
			float3 edge2 = v3 - v1;
			float3 normal = math.normalize(math.cross(edge1, edge2));

			// Store the face's normal for each of the vertices that make up the face.
			Normals[i0] += normal;
			Normals[i1] += normal;
			Normals[i2] += normal;

			// Store numbers that represent how many times an exact vertex is part of a face
			MatchCounter[i0] += 1;
			MatchCounter[i1] += 1;
			MatchCounter[i2] += 1;

			// Read UV vectors
			float2 w1 = UVs[i0];
			float2 w2 = UVs[i1];
			float2 w3 = UVs[i2];

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

			Tan1[i0] += sdir;
			Tan1[i1] += sdir;
			Tan1[i2] += sdir;

			Tan2[i0] += tdir;
			Tan2[i1] += tdir;
			Tan2[i2] += tdir;
		}
	}
}