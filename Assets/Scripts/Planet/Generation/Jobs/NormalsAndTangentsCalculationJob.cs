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
		[ReadOnly] public NativeArray<int> MatchCounter;
		[ReadOnly] public NativeArray<float3> Tan1;
		[ReadOnly] public NativeArray<float3> Tan2;
		public NativeArray<float3> Normals;

		public void Execute(int index)
		{
			// Read the specific counter value, there is no situation when it could be equal to zero
			int counter = MatchCounter[index];
			// If we only normalize the normals without averaging (by dividing them by the counter), 
			// then our calculations may not be as accurate as we would like.
			float3 n = math.normalize(Normals[index] / (float)counter);
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