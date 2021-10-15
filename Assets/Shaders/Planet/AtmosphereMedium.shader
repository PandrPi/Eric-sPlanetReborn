Shader "Hidden/Custom/AtmosphereMedium"
{
	HLSLINCLUDE

#include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"

		static const float maxFloat = 3.402823466e+38;

	TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);
	TEXTURE2D_SAMPLER2D(_CameraDepthTexture, sampler_CameraDepthTexture);
	TEXTURE2D_SAMPLER2D(_BakedOpticalDepth, sampler_BakedOpticalDepth);
	TEXTURE2D_SAMPLER2D(_BlueNoise, sampler_BlueNoise);

	uniform float3 planetCentre;
	uniform float3 dirToSun;


	uniform float test1;
	uniform float test2;
	uniform float test3;


	uniform float atmosphereRadius;
	uniform float planetRadius;
	uniform float densityFalloff;
	uniform float intensity;
	uniform float4 scatteringCoefficients; // only xyz are used
	uniform float ditherStrength;
	uniform float ditherScale;
	uniform int numInScatteringPoints;
	uniform int numOpticalDepthPoints;

	uniform float4 _CamWorldPos;
	uniform float4x4 _CamFrustum;

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
		o.rayDir = _CamFrustum[index].xyz;
		return o;
	}

	// Returns vector (dstToSphere, dstThroughSphere)
	// If ray origin is inside sphere, dstToSphere = 0
	// If ray misses sphere, dstToSphere = maxValue; dstThroughSphere = 0
	float2 raySphere(float3 sphereCentre, float sphereRadius, float3 rayOrigin, float3 rayDir)
	{
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

	float2 squareUV(float2 uv)
	{
		float width = _ScreenParams.x;
		float height = _ScreenParams.y;
		//float minDim = min(width, height);
		const float scale = 1000;
		float x = uv.x * width;
		float y = uv.y * height;
		return float2 (x / scale, y / scale);
	}

	float densityAtPoint(float3 densitySamplePoint) {
		float heightAboveSurface = length(densitySamplePoint - planetCentre) - planetRadius;
		float height01 = heightAboveSurface / (atmosphereRadius - planetRadius);
		float localDensity = exp(-height01 * densityFalloff) * (1 - height01);
		return localDensity;
	}

	float opticalDepth(float3 rayOrigin, float3 rayDir, float rayLength) {
		float3 densitySamplePoint = rayOrigin;
		float stepSize = rayLength / (numOpticalDepthPoints - 1);
		float opticalDepth = 0;

		for (int i = 0; i < numOpticalDepthPoints; i++) {
			float localDensity = densityAtPoint(densitySamplePoint);
			opticalDepth += localDensity * stepSize;
			densitySamplePoint += rayDir * stepSize;
		}
		return opticalDepth;
	}

	float opticalDepthBaked(float3 rayOrigin, float3 rayDir)
	{
		float height = length(rayOrigin - planetCentre) - planetRadius;
		float height01 = saturate(height / (atmosphereRadius - planetRadius));

		float uvX = 1 - (dot(normalize(rayOrigin - planetCentre), rayDir) * .5 + .5);
		return SAMPLE_TEXTURE2D_LOD(_BakedOpticalDepth, sampler_BakedOpticalDepth, float2(uvX, height01), 0);
	}

	float opticalDepthBaked2(float3 rayOrigin, float3 rayDir, float rayLength)
	{
		float3 endPoint = rayOrigin + rayDir * rayLength;
		float d = dot(rayDir, normalize(rayOrigin - planetCentre));
		float opticalDepth = 0;

		const float blendStrength = 1.5;
		float w = saturate(d * blendStrength + .5);

		float d1 = opticalDepthBaked(rayOrigin, rayDir) - opticalDepthBaked(endPoint, rayDir);
		float d2 = opticalDepthBaked(endPoint, -rayDir) - opticalDepthBaked(rayOrigin, -rayDir);

		opticalDepth = lerp(d2, d1, w);
		return opticalDepth;
	}

	float3 calculateLight(float3 rayOrigin, float3 rayDir, float rayLength, float3 originalCol, float2 uv)
	{
		float blueNoise = SAMPLE_TEXTURE2D_LOD(_BlueNoise, sampler_BlueNoise, squareUV(uv) * ditherScale, 0);
		blueNoise = (blueNoise - 0.5) * ditherStrength;
		float3 inScatterPoint = rayOrigin;
		float stepSize = rayLength / (numInScatteringPoints - 1);
		float3 inScatteredLight = 0;
		float viewRayOpticalDepth = 0;

		for (int i = 0; i < numInScatteringPoints; i++)
		{
			float sunRayLength = raySphere(planetCentre, atmosphereRadius, inScatterPoint, dirToSun).y;
			float sunRayOpticalDepth = opticalDepth(inScatterPoint, dirToSun, sunRayLength);
			viewRayOpticalDepth = opticalDepth(inScatterPoint, -rayDir, stepSize * i);
			float3 transmittance = exp(-(sunRayOpticalDepth + viewRayOpticalDepth) * scatteringCoefficients.xyz);
			float localDensity = densityAtPoint(inScatterPoint);

			inScatteredLight += localDensity * transmittance * scatteringCoefficients.xyz * stepSize;
			inScatterPoint += rayDir * stepSize;
		}

		// Attenuate brightness of original col (i.e light reflected from planet surfaces)
		// This is a hacky mess, TODO: figure out a proper way to do this
		const float brightnessAdaptionStrength = 0.5;
		const float reflectedLightOutScatterStrength = 3;
		float brightnessAdaption = dot(inScatteredLight, 1) * brightnessAdaptionStrength;
		float brightnessSum = viewRayOpticalDepth * intensity * reflectedLightOutScatterStrength + brightnessAdaption;
		float reflectedLightStrength = exp(-brightnessSum);
		float hdrStrength = saturate(dot(originalCol, 1) / 3 - 1);
		reflectedLightStrength = lerp(reflectedLightStrength, 1, hdrStrength);
		float3 reflectedLight = originalCol * reflectedLightStrength;

		float3 finalCol = reflectedLight + inScatteredLight;

		return finalCol;
	}

	float3 calculateLight3(float3 rayOrigin, float3 rayDir, float rayLength, float3 originalCol, float2 uv) {
		float blueNoise = SAMPLE_TEXTURE2D_LOD(_BlueNoise, sampler_BlueNoise, squareUV(uv) * ditherScale, 0);
		blueNoise = (blueNoise - 0.5) * ditherStrength;

		float3 inScatterPoint = rayOrigin;
		float stepSize = rayLength / (numInScatteringPoints - 1);
		float3 inScatteredLight = 0;
		float viewRayOpticalDepth = 0;

		/*for (int i = 0; i < numInScatteringPoints; i++) {
			float sunRayLength = raySphere(planetCentre, atmosphereRadius, inScatterPoint, dirToSun).y;
			float sunRayOpticalDepth = opticalDepthBaked(inScatterPoint + dirToSun * ditherStrength, dirToSun);
			float localDensity = densityAtPoint(inScatterPoint);
			viewRayOpticalDepth = opticalDepthBaked2(rayOrigin, rayDir, stepSize * i);
			float3 transmittance = exp(-(sunRayOpticalDepth + viewRayOpticalDepth) * scatteringCoefficients.xyz);

			inScatteredLight += localDensity * transmittance;
			inScatterPoint += rayDir * stepSize;
		}
		inScatteredLight *= scatteringCoefficients * intensity * stepSize / planetRadius;
		inScatteredLight += blueNoise * 0.01;*/

		for (int i = 0; i < numInScatteringPoints; i++) {
			float sunRayLength = raySphere(planetCentre, atmosphereRadius, inScatterPoint, dirToSun).y;
			float sunRayOpticalDepth = opticalDepthBaked(inScatterPoint + dirToSun * ditherStrength, dirToSun);
			float localDensity = densityAtPoint(inScatterPoint);
			viewRayOpticalDepth = opticalDepthBaked2(rayOrigin, rayDir, stepSize * i);
			float3 transmittance = exp(-(sunRayOpticalDepth + viewRayOpticalDepth) * scatteringCoefficients.xyz);

			inScatteredLight += localDensity * transmittance;
			inScatterPoint += rayDir * stepSize;
		}
		inScatteredLight *= scatteringCoefficients * intensity * stepSize;
		inScatteredLight += blueNoise * 0.01;


		// Attenuate brightness of original col (i.e light reflected from planet surfaces)
		// This is a hacky mess, TODO: figure out a proper way to do this
		const float brightnessAdaptionStrength = 0.5;
		const float reflectedLightOutScatterStrength = 3;
		float brightnessAdaption = dot(inScatteredLight, 1) * brightnessAdaptionStrength;
		float brightnessSum = viewRayOpticalDepth * intensity * reflectedLightOutScatterStrength + brightnessAdaption;
		float reflectedLightStrength = exp(-brightnessSum);
		float hdrStrength = saturate(dot(originalCol, 1) / 3 - 1);
		reflectedLightStrength = lerp(reflectedLightStrength, 1, hdrStrength);
		float3 reflectedLight = originalCol * reflectedLightStrength;

		float3 finalCol = reflectedLight + inScatteredLight;

		return finalCol;
	}

	float3 calculateLight2(float3 rayOrigin, float3 rayDir, float rayLength)
	{
		float3 inScatterPoint = rayOrigin;
		float stepSize = rayLength / (numInScatteringPoints - 1);
		float3 inScatteredLight = 0;

		for (int i = 0; i < numInScatteringPoints; i++)
		{
			float sunRayLength = raySphere(planetCentre, atmosphereRadius, inScatterPoint, dirToSun).y;
			//float sunRayOpticalDepth = opticalDepth(inScatterPoint, dirToSun, sunRayLength);
			//float viewRayOpticalDepth = opticalDepth(inScatterPoint, -rayDir, stepSize * i);
			//float3 transmittance = exp(-(viewRayOpticalDepth));
			//float3 transmittance = exp(-(sunRayOpticalDepth + viewRayOpticalDepth));
			float localDensity = densityAtPoint(inScatterPoint);

			/*float3 planetNormal = normalize(inScatterPoint - planetCentre);
			float nDotL = 1.0 - pow(saturate(dot(planetNormal, -dirToSun) + test1), test2);*/

			//inScatteredLight += localDensity * stepSize * nDotL;
			inScatteredLight += localDensity * stepSize;
			//inScatteredLight += localDensity * transmittance * stepSize;
			inScatterPoint += rayDir * stepSize;
		}

		return saturate(inScatteredLight * float3(1, 1, 0) * intensity);
	}

	float4 Frag(v2f i) : SV_Target
	{
		float4 originalColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
		float sceneDepthNonLinear = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, i.texcoordStereo);
		float sceneDepth = Linear01Depth(sceneDepthNonLinear) * length(i.rayDir);

		float3 rayOrigin = _WorldSpaceCameraPos;
		float3 rayDir = normalize(i.rayDir);

		float2 hitInfo = raySphere(planetCentre, atmosphereRadius, rayOrigin, rayDir);
		float dstToAtmosphere = hitInfo.x;
		float dstThroughAtmosphere = min(hitInfo.y, sceneDepth - dstToAtmosphere);

		if (dstThroughAtmosphere > 0) {
			const float epsilon = 0.0001;
			float3 pointInAtmosphere = rayOrigin + rayDir * (dstToAtmosphere + epsilon);
			float rayLength = dstThroughAtmosphere - epsilon * 2;
			float3 light = calculateLight(pointInAtmosphere, rayDir, rayLength, originalColor, i.uv);
			//return originalColor * float4(1.0 - light, 1.0) + float4(light, 1.0);
			return float4(light, 1.0);

			/*const float epsilon = 0.0001;
			float3 pointInAtmosphere = rayOrigin + rayDir * (dstToAtmosphere + epsilon);
			float3 light = calculateLight2(pointInAtmosphere, rayDir, dstThroughAtmosphere - epsilon * 2);
			return originalColor * float4(1.0 - light, 1.0) + float4(light, 1.0);*/

			//const float epsilon = 0.0001;
			//float3 pointInAtmosphere = rayOrigin + rayDir * (dstToAtmosphere + epsilon);
			//float rayLength = dstThroughAtmosphere - epsilon * 2;
			////float3 light = calculateLight(pointInAtmosphere, rayDir, rayLength, originalColor, i.uv);
			//float3 light = calculateLight3(pointInAtmosphere, rayDir, rayLength, originalColor, i.uv);
			////return originalColor * float4(1.0 - light, 1.0) + float4(light, 1.0);
			//return float4(light, 1);
		}

		return originalColor;
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
