Shader "Custom/AtmosphereLow"
{
	Properties{
		_MainColor("Main color", Color) = (1, 1, 1, 1)
		//_FogMultiplier("Fog Multiplier", Range(0, 1)) = 0
		//_FresnelExponent("Fresnel Exponent", Range(0.25, 4)) = 0
		_AtmosphereRadius("Atmophere radius", Float) = 0
		_PlanetRadius("Planet radius", Float) = 0
		_PlanetCenter("Planet center", Vector) = (0, 0, 0, 0)
	}
		SubShader{
		Tags{ Queue = Transparent }
		Blend SrcAlpha OneMinusSrcAlpha
		Cull Front

		Pass
		{
			Tags{ "LightMode" = "ForwardBase" "Queue" = "Transparent" "RenderType" = "Transparent" }

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "UnityLightingCommon.cginc"
			#include "Assets/Shaders/Planet/PlanetaryFog.cginc"			

			static const float PI = 3.14159265f;
			static const float _FresnelExponent = 0.85;

			float4 _MainColor;

			float _FogParam;
			float4 _FogColor;

			//float _FogMultiplier;
			float _AtmosphereRadius;
			float _PlanetRadius;
			float3 _PlanetCenter;

			struct VertexOutput
			{
				float4 vertex : SV_POSITION;
				float4 color : COLOR;
				float3 worldNormal : TEXCOORD2;
				float3 worldPos : TEXCOORD3; // Vertex world position
				float3 cameraToWorldPosDir : TEXCOORD4; // Direction from a vertex world position to the camera
				float atmosphereFactor : TEXCOORD5; // Multiplication of fresnel, fog and planetaryFogAttenuation factors
			};

			float CalculateFresnelEffect(float3 Normal, float3 ViewDir)
			{
				float camDistanceToPlanet = distance(_WorldSpaceCameraPos.xyz, _PlanetCenter);

				float camHeightNormalizer = _AtmosphereRadius - _PlanetRadius;
				float camHeightAboveSurface = camDistanceToPlanet - _PlanetRadius - camHeightNormalizer * 0.5;
				float camHeight01 = saturate(camHeightAboveSurface / camHeightNormalizer * 2);

				float fresnel = pow(saturate(dot(normalize(Normal), normalize(ViewDir))), _FresnelExponent);
				fresnel = smoothstep(0.0, 1.0, fresnel);
				fresnel = lerp(1.0, fresnel, camHeight01);

				return fresnel;
			}

			VertexOutput vert(appdata_base v)
			{
				VertexOutput o;
				o.vertex = UnityObjectToClipPos(v.vertex);

				// Get world camera pos and limit it with atmosphere radius
				float3 worldCamPosition = LimitCameraPosByRadius(_PlanetCenter, _AtmosphereRadius, false);

				o.worldPos = mul(unity_ObjectToWorld, v.vertex);
				o.worldNormal = normalize(_PlanetCenter - o.worldPos);
				float3 cameraToWorldPosDir = normalize(worldCamPosition - o.worldPos);

				// Calculate all the needed variables for the atmosphereFactor calculation
				/*float planetaryFogAttenuation = 1.0 - CalculatePlanetaryFogAttenuationNew(o.worldPos, _PlanetCenter,
					_PlanetRadius, _AtmosphereRadius, false);*/
				float planetaryFogAttenuation = CalculatePlanetShadowValueForAtmosphere(o.worldPos, _PlanetCenter,
					_PlanetRadius, _AtmosphereRadius);
				float fogFactor = CalculatePlanetaryFogFactor(o.worldPos, _PlanetCenter, _AtmosphereRadius, _FogParam);
				float fresnel = CalculateFresnelEffect(o.worldNormal, cameraToWorldPosDir);

				// Calculate atmosphere factor
				//o.atmosphereFactor = fresnel * fogFactor;
				//o.atmosphereFactor = planetaryFogAttenuation;
				o.atmosphereFactor = fresnel * fogFactor * planetaryFogAttenuation;

				return o;
			}

			float4 frag(VertexOutput i) : SV_Target
			{
				float4 atmosphereColor = float4(1, 1, 1, 1); // We have to calculate it depending on the planetaryAttenuation
				float4 col = atmosphereColor * _MainColor;

				// Calculate smoother alpha value with smoothstep function
				col.a = smoothstep(0.0, 1.0, i.atmosphereFactor);

				return col;
			}
			ENDCG
		}
	}
}
