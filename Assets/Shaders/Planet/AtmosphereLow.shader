Shader "Custom/AtmosphereLow"
{
	Properties{
		_MainColor("Main color", Color) = (1, 1, 1, 1)
		_FogMultiplier("Fog Multiplier", Range(-1, 1)) = 0
		_FresnelExponent("Fresnel Exponent", Range(0.25, 4)) = 0
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
			#pragma multi_compile_fog
			#include "UnityCG.cginc"
			#include "UnityLightingCommon.cginc"

			static const float PI = 3.14159265f;

			float4 _MainColor;
			float _AtmosphereRadius;
			float _FresnelExponent;
			float _PlanetRadius;
			float _FogMultiplier;
			float3 _PlanetCenter;

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float4 color : COLOR;
				float3 worldNormal : TEXCOORD2;
				float3 worldVertex : TEXCOORD3;
				float3 cameraDir : TEXCOORD4; // Direction from a planet to the camera
				//UNITY_FOG_COORDS(1)
			};

			v2f vert(appdata_base v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);

				o.worldVertex = mul(unity_ObjectToWorld, v.vertex);
				o.worldNormal = normalize(_PlanetCenter - o.worldVertex);
				o.cameraDir = normalize(_PlanetCenter - _WorldSpaceCameraPos.xyz);

				//UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				float4 col = float4(1, 1, 1, 1) * _MainColor;

				float NdotL = dot(i.worldNormal, _WorldSpaceLightPos0.xyz);
				float normalAlpha = 1.0 - (NdotL + 1.0) * 0.5;

				float fresnel = dot(i.worldNormal, i.cameraDir);
				float cameraHeightAboveSurface = distance(_PlanetCenter, _WorldSpaceCameraPos.xyz) - _PlanetRadius;
				// Normalize cameraHeightAboveSurface variable to 0..1 range
				float cameraHeight01 = 1.0 - saturate(cameraHeightAboveSurface / (_AtmosphereRadius - _PlanetRadius));
				cameraHeight01 = smoothstep(0, 1, cameraHeight01);

				fresnel = smoothstep(1, 0, fresnel - cameraHeight01);
				fresnel = pow(fresnel, _FresnelExponent);
				col.a = smoothstep(0.0, 1.0, fresnel * normalAlpha);

				return col;
			}
			ENDCG
		}
	}
}
