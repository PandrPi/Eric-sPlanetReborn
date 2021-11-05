Shader "Hidden/Custom/AtmosphereMedium"
{
	HLSLINCLUDE

	#include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"

	// Source code of the core of the shader is here: https://www.shadertoy.com/view/ldsBz2

	#define PI 3.14159265359

	TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);
	TEXTURE2D_SAMPLER2D(_CameraDepthTexture, sampler_CameraDepthTexture);
	TEXTURE2D_SAMPLER2D(_BakedOpticalDepth, sampler_BakedOpticalDepth);

	uniform float4x4 FrustumMatrix;
	uniform float3 LightDirection;
	uniform float3 ScatteringCoefficients;
	uniform float3 PlanetCenter;
	uniform float PlanetRadius;
	uniform float AtmosphereRadius;
	uniform float AtmosphereIntensity;
	uniform float DensityScale;
	uniform float OpticScale;
	uniform int OutScatteringPointsNum;
	uniform int InScatteringPointsNum;

	static const float K_R = 0.166;
	static const float K_M = 0.0025;
	static const float E = 12.0; 		// light intensity
	static const float G_M = -0.92;		// Mie g

	float2 GetRaySphereIntersection(float3 sphereCenter, float3 rayOrigin, float3 rayDir, float radius)
	{
		const float MAX = 1e9;

		float3 p = rayOrigin - sphereCenter;
		float b = dot(p, rayDir);
		float c = dot(p, p) - radius * radius;

		float d = b * b - c;
		if (d < 0.0)
		{
			return float2(MAX, -MAX);
		}

		d = sqrt(d);

		return float2(max(0, -b - d), -b + d);
	}

	float CalculateMiePhase(float g, float c, float cc)
	{
		float gg = g * g;
		float a = (1.0 - gg) * (1.0 + cc);
		float b = 1.0 + gg - 2.0 * g * c;
		b *= sqrt(b);
		b *= 2.0 + gg;
		return 1.5 * a / b;
	}

	float CalculateDensity(float3 p)
	{
		float heightAboveSurface = length(p - PlanetCenter) - PlanetRadius;
		float height01 = heightAboveSurface / (AtmosphereRadius - PlanetRadius);
		return exp(-height01 * DensityScale) * (1 - height01);
		//return exp(-(length(p - PlanetCenter) - PlanetRadius) * DensityScale);
	}

	float CalculateOpticalDepth(float3 p, float3 q)
	{
		float3 step = (q - p) / float(OutScatteringPointsNum);
		float3 v = p + step * 0.5;

		float sum = 0.0;
		for (int i = 0; i < OutScatteringPointsNum; i++)
		{
			sum += CalculateDensity(v);
			v += step;
		}

		sum *= length(step) * OpticScale;
		return sum;
	}

	float3 CalculateAtmosphereScattering(float3 rayOrigin, float3 rayDir, float2 intersectionInfo, float3 lightDir)
	{
		float len = (intersectionInfo.y - intersectionInfo.x) / float(InScatteringPointsNum);
		float3 step = rayDir * len;
		float3 p = rayOrigin + rayDir * intersectionInfo.x;
		float3 v = p + rayDir * (len * 0.5);

		float3 sum = float3(0.0, 0.0, 0.0);
		for (int i = 0; i < InScatteringPointsNum; i++)
		{
			float2 f = GetRaySphereIntersection(PlanetCenter, v, lightDir, AtmosphereRadius);
			float3 u = v + lightDir * f.y;

			//float n = (1 + 1) * (PI * 4.0);
			float n = (CalculateOpticalDepth(p, v) + CalculateOpticalDepth(v, u)) * (PI * 4.0);
			sum += CalculateDensity(v) * exp(-n * (K_R * ScatteringCoefficients + K_M));
			v += step;
		}

		sum *= len * OpticScale;

		float c = dot(rayDir, -lightDir);
		float cc = c * c;
		float3 rayleighScattering = K_R * ScatteringCoefficients * 0.75 * (1.0 + cc);
		float3 result = sum * (rayleighScattering + K_M * CalculateMiePhase(G_M, c, cc)) * E;
		return saturate(result);
	}



	float2 raySphere(float3 sphereCentre, float sphereRadius, float3 rayOrigin, float3 rayDir)
	{
		const float maxFloat = 3.402823466e+38;

		float3 offset = rayOrigin - sphereCentre;
		float a = 1; // Set to dot(rayDir, rayDir) if rayDir might not be normalized
		float b = 2 * dot(offset, rayDir);
		float c = dot(offset, offset) - sphereRadius * sphereRadius;
		float d = b * b - 4 * a * c; // Discriminant from quadratic formula

		// Number of intersections: 0 when d < 0; 1 when d = 0; 2 when d > 0
		if (d > 0) {
			float s = sqrt(d);
			float dstToSphereNear = max(0, (-b - s) / (2 * a));
			float dstToSphereFar = (-b + s) / (2 * a);

			// Ignore intersections that occur behind the ray
			if (dstToSphereFar >= 0) {
				return float2(dstToSphereNear, dstToSphereFar - dstToSphereNear);
			}
		}
		// Ray did not intersect sphere
		return float2(maxFloat, 0);
	}

	float densityAtPoint(float3 densitySamplePoint) {
		float heightAboveSurface = length(densitySamplePoint - PlanetCenter) - PlanetRadius;
		float height01 = heightAboveSurface / (AtmosphereRadius - PlanetRadius);
		float localDensity = exp(-height01 * DensityScale) * (1 - height01);
		return localDensity;
	}

	float opticalDepth(float3 rayOrigin, float3 rayDir, float rayLength) {
		float3 densitySamplePoint = rayOrigin;
		float stepSize = rayLength / (OutScatteringPointsNum - 1);
		float opticalDepth = 0;

		for (int i = 0; i < OutScatteringPointsNum; i++) {
			float localDensity = densityAtPoint(densitySamplePoint);
			opticalDepth += localDensity * stepSize;
			densitySamplePoint += rayDir * stepSize;
		}
		return opticalDepth;
	}

	float opticalDepthBaked(float3 rayOrigin, float3 rayDir)
	{
		float height = length(rayOrigin - PlanetCenter) - PlanetRadius;
		float height01 = saturate(height / (AtmosphereRadius - PlanetRadius));

		float uvX = 1 - (dot(normalize(rayOrigin - PlanetCenter), rayDir) * .5 + .5);
		return SAMPLE_TEXTURE2D_LOD(_BakedOpticalDepth, sampler_BakedOpticalDepth, float2(uvX, height01), 0);
	}

	float opticalDepthBaked2(float3 rayOrigin, float3 rayDir, float rayLength)
	{
		float3 endPoint = rayOrigin + rayDir * rayLength;
		float d = dot(rayDir, normalize(rayOrigin - PlanetCenter));
		float opticalDepth = 0;

		const float blendStrength = 1.5;
		float w = saturate(d * blendStrength + .5);

		float d1 = opticalDepthBaked(rayOrigin, rayDir) - opticalDepthBaked(endPoint, rayDir);
		float d2 = opticalDepthBaked(endPoint, -rayDir) - opticalDepthBaked(rayOrigin, -rayDir);

		opticalDepth = lerp(d2, d1, w);
		return opticalDepth;
	}

	float3 calculateLight(float3 rayOrigin, float3 rayDir, float rayLength)
	{
		/*float3 inScatterPoint = rayOrigin;
		float stepSize = rayLength / float(InScatteringPointsNum - 1);
		float3 inScatteredLight = 0;
		float viewRayOpticalDepth = 0;

		for (int i = 0; i < InScatteringPointsNum; i++) {
			float sunRayLength = raySphere(PlanetCenter, AtmosphereRadius, inScatterPoint, LightDirection).y;
			float sunRayOpticalDepth = opticalDepthBaked(inScatterPoint, LightDirection);
			float localDensity = densityAtPoint(inScatterPoint);
			viewRayOpticalDepth = opticalDepthBaked2(rayOrigin, rayDir, stepSize * i);
			float3 transmittance = exp(-(sunRayOpticalDepth + viewRayOpticalDepth) * ScatteringCoefficients.xyz);

			inScatteredLight += localDensity * transmittance;
			inScatterPoint += rayDir * stepSize;
		}
		inScatteredLight *= ScatteringCoefficients * stepSize;*/

		float3 inScatterPoint = rayOrigin;
		float stepSize = rayLength / (InScatteringPointsNum - 1);
		float3 inScatteredLight = 0;
		float viewRayOpticalDepth = 0;

		for (int i = 0; i < InScatteringPointsNum; i++) {
			float sunRayLength = raySphere(PlanetCenter, AtmosphereRadius, inScatterPoint, -LightDirection).y;
			float sunRayOpticalDepth = opticalDepthBaked(inScatterPoint, -LightDirection);
			float localDensity = densityAtPoint(inScatterPoint);
			viewRayOpticalDepth = opticalDepthBaked2(rayOrigin, rayDir, stepSize * i);
			float3 transmittance = exp(-(sunRayOpticalDepth + viewRayOpticalDepth) * ScatteringCoefficients);

			inScatteredLight += localDensity * transmittance;
			inScatterPoint += rayDir * stepSize;
		}
		inScatteredLight *= ScatteringCoefficients * stepSize / PlanetRadius;

		return inScatteredLight;
	}

	struct appdata
	{
		float3 vertex : POSITION;
	};

	struct v2f
	{
		float4 vertex : SV_POSITION;
		float2 uv : TEXCOORD0;
		float2 texcoordStereo : TEXCOORD1;
		float3 rayDir : TEXCOORD2;
	};

	v2f Vert(appdata v)
	{
		v2f o;
		o.vertex = float4(v.vertex.xy, 0.0, 1.0);
		float2 uv = TransformTriangleVertexToUV(v.vertex.xy);
		o.uv = uv;

#if UNITY_UV_STARTS_AT_TOP
		o.uv = float2(uv.x, 1.0 - uv.y);
#endif
		o.texcoordStereo = TransformStereoScreenSpaceTex(o.uv, 1.0);

		int index = (uv.x / 2) + uv.y;
		o.rayDir = FrustumMatrix[index].xyz;
		return o;
	}

	float4 Frag(v2f i) : SV_Target
	{
		// Sample the screen pixel
		float4 originalColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

		// Set the camera world position as the origin of the ray
		float3 rayOrigin = _WorldSpaceCameraPos;
		// Normalize the ray direction get from the vertex shader
		float3 rayDir = normalize(i.rayDir);

		// Get the intersection info based on the ray origin and the ray direction
		float2 intersectionInfo = GetRaySphereIntersection(PlanetCenter, rayOrigin, rayDir, AtmosphereRadius);
		// if the ray didn't intersect the atmosphere than just return the original color because the current
		// pixel will not be affected by the atmosphere
		if (intersectionInfo.x > intersectionInfo.y) return originalColor;

		// Sample the non linear depth value from the depth buffer
		float sceneDepthNonLinear = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, i.texcoordStereo);
		// Calculate the scene depth value based on the sample from the depth buffer
		float sceneDepth = Linear01Depth(sceneDepthNonLinear) * length(i.rayDir);

		// We need to take into account the depth of the scene
		intersectionInfo.y = min(intersectionInfo.y, sceneDepth);

		// Calculate the atmosphere scattering color
		float3 scattering = CalculateAtmosphereScattering(rayOrigin, rayDir, intersectionInfo, -LightDirection);
		//float3 scattering = CalculateAtmosphereScattering(rayOrigin, rayDir, intersectionInfo, -LightDirection);
		// Combine the original color with the atmosphere scattering color
		float3 color = saturate(originalColor.rgb + scattering * AtmosphereIntensity);

		// Return the result
		return float4(color, 1.0);


		/*float4 originalColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
		float sceneDepthNonLinear = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, i.texcoordStereo);
		float sceneDepth = Linear01Depth(sceneDepthNonLinear) * length(i.rayDir);

		float3 rayOrigin = _WorldSpaceCameraPos;
		float3 rayDir = normalize(i.rayDir);

		float2 hitInfo = raySphere(PlanetCenter, AtmosphereRadius, rayOrigin, rayDir);
		float dstToAtmosphere = hitInfo.x;
		float dstThroughAtmosphere = min(hitInfo.y, sceneDepth - dstToAtmosphere);

		if (dstThroughAtmosphere > 0) {
			const float epsilon = 0.0001;
			float3 pointInAtmosphere = rayOrigin + rayDir * (dstToAtmosphere + epsilon);
			float3 light = calculateLight(pointInAtmosphere, rayDir, dstThroughAtmosphere - epsilon * 2);
			return float4(originalColor + light * AtmosphereIntensity, 1.0);
		}

		return originalColor;*/

	}

		ENDHLSL

		SubShader
	{
		Cull Off ZWrite Off ZTest Always

			Pass
		{
			HLSLPROGRAM

				#pragma vertex Vert
				#pragma fragment Frag

			ENDHLSL
		}
	}
}
