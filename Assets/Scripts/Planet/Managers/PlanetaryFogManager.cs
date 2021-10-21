using Unity.Mathematics;
using UnityEngine;

namespace Planet.Managers
{
	/// <summary>
	/// This class is used to manage the exponential squared fog settings for a specific planet.
	/// </summary>
	public class PlanetaryFogManager : MonoBehaviour
	{
		[SerializeField] private Material atmosphereMaterial;
		[SerializeField] private Material planetMaterial;
		[SerializeField] private float fogDensity;
		[SerializeField] private float maxFogDistance; // Distance at which a fragment will be almost completely foggy

		private float _prevFogDistance = float.MaxValue;
		private float _fogParam;

		private const float FOG_VALUE_FOR_DENSITY_CALCULATION = 0.99f;

		private static readonly float LN2_SQRT = math.sqrt(math.LN2);
		private static readonly int FogParamID = Shader.PropertyToID("_FogParam");

		private void Start()
		{
			UpdateFogDensity();
		}

		private void Update()
		{
			// We have to check whether the maxFogDistance was changed in order to send a new fog data to the shader only
			// if it is necessary
			if (maxFogDistance != _prevFogDistance) UpdateFogDensity();
		}

		private void OnValidate()
		{
			UpdateFogDensity();
		}

		/// <summary>
		/// Calculates the density of the fog and sends it to the shader
		/// </summary>
		private void UpdateFogDensity()
		{
			fogDensity = GetFogDensityFromDistance(maxFogDistance);
			_fogParam = fogDensity / LN2_SQRT;
			planetMaterial.SetFloat(FogParamID, _fogParam);
			atmosphereMaterial.SetFloat(FogParamID, _fogParam);

			_prevFogDistance = maxFogDistance;
		}

		/// <summary>
		/// Returns a fog density value at which the fog function returns a value of FOG_VALUE_FOR_DENSITY_CALCULATION
		/// at a specified distance (ie a fragment that is located at a specified distance from the camera will be foggy by
		/// FOG_VALUE_FOR_DENSITY_CALCULATION * 100 percentages at a returning fog density)
		/// </summary>
		private float GetFogDensityFromDistance(float distance)
		{
			return math.sqrt(-math.log(1.0f - FOG_VALUE_FOR_DENSITY_CALCULATION) / (distance * distance));
		}
	}
}
