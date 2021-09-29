using Unity.Mathematics;

namespace Planet.Noises
{
	public interface IGenerationNoise
	{
		float GetValue(float3 x);
		//float NoiseFBM(float3 x, int octaves, float frequency, float amplitude, float lacunarity, float gain);
	}
}
