using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Planet.Generation.Jobs
{
	/// <summary>
	/// Calculates normals and tangents vectors based on pre-prepared data.
	/// </summary>
	[BurstCompile]
	public struct NormalsAndTangentsCalculationJob : IJobParallelFor
	{
		[WriteOnly] public NativeArray<float4> Tangents;
		[ReadOnly] public NativeArray<float3> Tan1;
		[ReadOnly] public NativeArray<float3> Tan2;
		public NativeArray<float3> Normals;

		public void Execute(int index)
		{
			// Normalize the normal vector
			float3 n = math.normalize(Normals[index]);
			float3 t = Tan1[index];


			float3 tmp = math.normalize(t - n * math.dot(n, t));
			var result = new float4
			{
				x = tmp.x,
				y = tmp.y,
				z = tmp.z,
				w = (math.dot(math.cross(n, t), Tan2[index]) < 0.0f) ? -1.0f : 1.0f
			};

			Normals[index] = n;
			Tangents[index] = result;
		}
	}
}