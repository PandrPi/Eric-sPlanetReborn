using AOT;
using Planet.Noises;
using Unity.Burst;
using Unity.Mathematics;

namespace Planet.Generation.Jobs.Helpers
{
	/// <summary>
	/// Stores GenerateVertex method and its pointer for the further usage inside Burst compiled jobs
	/// </summary>
	[BurstCompile]
	public class VertexGenerationHelper
	{
		public readonly static FunctionPointer<GenerateVertexDelegate> GenerateVertexPointer =
			BurstCompiler.CompileFunctionPointer<GenerateVertexDelegate>(GenerateVertex);

		public delegate void GenerateVertexDelegate(out float3 outVertex,
			FunctionPointer<BetterNormalizationHelper.BetterNormalizeDelegate> BetterNormalize,
			float xPos,
			float zPos,
			ref float4x4 ChunkToPlanet,
			ref float4x4 PlanetToChunk,
			ref RidgedNoise generationNoise,
			float Radius,
			float DoubledInversedRadius);

		/// <summary>
		/// Generates a new float3 vertex vector based on the specified xPos and zPos input parameters and applies a noise
		/// values for this vertex
		/// </summary>
		/// <param name="outVertex">The output vertex vector</param>
		/// <param name="BetterNormalize">The FunctionPointer object to the BetterNormalize method</param>
		/// <param name="xPos">The vertex source X position</param>
		/// <param name="zPos">The vertex source Z position</param>
		/// <param name="ChunkToPlanet">The ChunkToPlanet transformation matrix</param>
		/// <param name="PlanetToChunk">The PlanetToChunk transformation matrix</param>
		/// <param name="generationNoise">The noise object that will be used for vertex generation</param>
		/// <param name="Radius">The Radius of the planet</param>
		/// <param name="DoubledInversedRadius">The doubled inversed radius of the planet (1.0 / radius * 2.0)</param>
		[BurstCompile]
		[MonoPInvokeCallback(typeof(GenerateVertexDelegate))]
		public static void GenerateVertex(out float3 outVertex,
			FunctionPointer<BetterNormalizationHelper.BetterNormalizeDelegate> BetterNormalize,
			float xPos, 
			float zPos,
			ref float4x4 ChunkToPlanet,
			ref float4x4 PlanetToChunk,
			ref RidgedNoise generationNoise,
			float Radius,
			float DoubledInversedRadius)
		{
			// Create the source vertex
			var boundarySourceVertex = new float4(xPos, 0, zPos, 1.0f);
			// Convert the source vertex to the planetary space
			float4 newVertex = math.mul(ChunkToPlanet, boundarySourceVertex);

			// Normalize newVertex
			var vertexInversed = newVertex.xyz * DoubledInversedRadius;
			BetterNormalize.Invoke(ref vertexInversed);

			// Generate noise based on the normalized vertex
			float noise = generationNoise.GetValue(vertexInversed);
			// Apply noise to the vertex
			newVertex.xyz = vertexInversed * (noise + Radius);

			// Convert the generated vertex back to the chunk space
			outVertex = math.mul(PlanetToChunk, newVertex).xyz;
		}
	}
}
