/// <summary>
///	This cginc file contains some helper methods for the planetary fog (atmosphere of the planet)
/// <summary>

#include "UnityCG.cginc"
#include "UnityLightingCommon.cginc"

#define PLANETARY_FOG_FACTOR(idx) float fogFactor : TEXCOORD##idx;

// This value represents the falloff width of the shadow of the planet. 
// This width can be calculated as ATMOSPHERE_SHADOW_FALLOFF * planetRadius
static const float ATMOSPHERE_SHADOW_FALLOFF = 0.3;

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

/// <summary>
/// Normalizes the specified value
/// </summary>
float NormalizeWithinRange(float value, float minValue, float maxValue)
{
	return saturate((value - minValue) / (maxValue - minValue));
}

float CalculatePlanetShadowValueForAtmosphere(float3 worldPoint, float3 planetCenter, float planetRadius,
	float atmosphereRadius)
{
	// IMPORTANT NOTE!
	// The value of atmosphereRadius parameter must always be greater than the value of planetRadius parameter

	float3 lightDirection = _WorldSpaceLightPos0.xyz;
	float3 planetaryNormal = normalize(worldPoint - planetCenter);
	float normalDotLight = saturate(dot(planetaryNormal, lightDirection) * -1.0);

	float absoluteRadiusesDifference = atmosphereRadius - planetRadius;
	float relativeRadiusesDifference = absoluteRadiusesDifference / atmosphereRadius;
	float shadowRectangleHeight = 1.0 - relativeRadiusesDifference;

	// The height of the shadow rectangle is a value relative to the radius of the atmosphere, so inside the root
	// function we have to write 1.0 - shadowRectangleHeight ^ 2, because atmosphereRadius is an absolute value, not 
	// a relative one, and we can't use it.
	// Find the threshold of normalDotLight value above which the atmosphere is shadowed by the planet.
	float shadowThreshold = sqrt(1.0 - shadowRectangleHeight * shadowRectangleHeight);

	float maxThreshold = shadowThreshold + ATMOSPHERE_SHADOW_FALLOFF;
	float shadow = 1.0 - NormalizeWithinRange(normalDotLight, shadowThreshold, maxThreshold);

	return smoothstep(0.0, 1.0, shadow);
}

/// <summary>
/// Calculates planetary fog attenuation based on the specified planetary normal.
/// </summary>
float CalculatePlanetaryFogAttenuationNew(float3 worldPoint, float3 planetCenter, float planetRadius,
	float atmosphereRadius, bool invertLightDir)
{
	//if (distance(worldPoint, planetCenter) <= planetRadius) return 0.0;

	float cylinderLength = atmosphereRadius * 2.0;
	float lightDirInversionValue = ((int)invertLightDir * 2.0 - 1.0) * -1.0;
	float lengthsq = cylinderLength * cylinderLength;

	float3 lightDirection = _WorldSpaceLightPos0.xyz;
	float3 pt1 = planetCenter + lightDirection * cylinderLength * lightDirInversionValue;
	float3 pt2 = planetCenter;

	float3 d = pt2 - pt1;			// vector d from line segment point 1 to point 2
	float3 pd = worldPoint - pt2;	// vector from pt1 to test point.

	// Dot the d and pd vectors to see if point lies behind the 
	// cylinder cap at pt1.x, pt1.y, pt1.z
	float dAndPd_Dot = dot(pd, d);

	// If dot is less than zero the point is behind the pt1 cap.
	// If greater than the cylinder axis line segment length squared
	// then the point is outside the other end cap at pt2.
	if (dAndPd_Dot < 0.0f || dAndPd_Dot > lengthsq)
	{
		return 0.0f;
	}
	else
	{
		// distance squared to the cylinder axis:
		float dsq = dot(pd, pd) - dAndPd_Dot * dAndPd_Dot / lengthsq;
		float radius_sq = planetRadius * planetRadius;

		return dsq > radius_sq ? 0.0 : 1.0;
	}
}


