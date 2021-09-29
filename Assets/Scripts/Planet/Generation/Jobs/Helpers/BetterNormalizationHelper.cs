using AOT;
using Unity.Burst;
using Unity.Mathematics;

namespace Planet.Generation.Jobs.Helpers
{
	/// <summary>
	/// Stores BetterNormalize method and its pointer for the further usage inside Burst compiled jobs
	/// </summary>
	[BurstCompile]
	public static class BetterNormalizationHelper
	{
		public static readonly FunctionPointer<BetterNormalizeDelegate> BetterNormalizePointer =
			BurstCompiler.CompileFunctionPointer<BetterNormalizeDelegate>(BetterNormalize);


		public delegate void BetterNormalizeDelegate(ref float3 v);

		/// <summary>
		/// Does a more uniform vector normalization
		/// </summary>
		/// <param name="v">The vector for normalization</param>
		/// <returns>The normalized float3 vector</returns>
		[BurstCompile]
		[MonoPInvokeCallback(typeof(BetterNormalizeDelegate))]
		public static void BetterNormalize(ref float3 v)
		{
			const float inverseTwo = 1.0f / 2.0f;
			const float inverseThree = 1.0f / 3.0f;
			float3 v2 = v * v;
			float3 s = new float3(
				math.sqrt(1f - (v2.y * inverseTwo) - (v2.z * inverseTwo) + (v2.y * v2.z * inverseThree)),
				math.sqrt(1f - (v2.x * inverseTwo) - (v2.z * inverseTwo) + (v2.x * v2.z * inverseThree)),
				math.sqrt(1f - (v2.x * inverseTwo) - (v2.y * inverseTwo) + (v2.x * v2.y * inverseThree)));

			v *= s;
		}
	}
}
