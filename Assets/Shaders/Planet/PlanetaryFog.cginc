/// <summary>
///	This cginc file contains some helper methods for the planetary fog (atmosphere of the planet)
/// <summary>

#include "UnityCG.cginc"
#include "UnityLightingCommon.cginc"

#define PLANETARY_FOG_FACTOR(idx) float fogFactor : TEXCOORD##idx;

/// <summary>
/// Calculates vector from the specified planetCenter to the _WorldSpaceCameraPos, if the length of the calculated 
/// vector is bigger (or less equal, see checkForBigger parameter) that the specified limitationRadius then the
/// calculated vector saves its direction but its length becomes equal to the specified limitationRadius.
/// </summary>
float3 LimitCameraPosByRadius(float3 planetCenter, float limitationRadius, bool checkForBigger)
{
	float3 worldCamPosition = _WorldSpaceCameraPos.xyz;
	if (distance(worldCamPosition, planetCenter) > limitationRadius == checkForBigger)
		worldCamPosition = normalize(worldCamPosition - planetCenter) * limitationRadius;

	return worldCamPosition;
}

/// <summary>
/// Calculates planetary fog factor based on the specified parameters.
/// </summary>
float CalculatePlanetaryFogFactor(float3 vertexWorldPos, float3 planetCenter, float atmosphereRadius, float fogParameter)
{
	float3 worldCamPosition = LimitCameraPosByRadius(planetCenter, atmosphereRadius, true);

	float viewDistance = length(worldCamPosition - vertexWorldPos);
	float fogFactor = fogParameter * viewDistance;
	fogFactor = exp2(-fogFactor * fogFactor);

	return 1.0 - saturate(fogFactor);
}

/// <summary>
/// Calculates planetary fog attenuation based on the specified planetary normal.
/// </summary>
float CalculatePlanetaryFogAttenuation(float3 planetaryNormal)
{
	return (dot(planetaryNormal, -_WorldSpaceLightPos0.xyz) + 1.0) * 0.5;
}


