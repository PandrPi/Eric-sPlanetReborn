Shader "Custom/SimplePlanetShader"
{
	Properties 
	{
		_Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
		_FogColor ("Fog Color (RGB)", Color) = (0.5, 0.5, 0.5, 1.0)

		_AtmospherePower("Atmophere power", Range(0.5, 10)) = 0
		_AtmosphereRadius("Atmophere radius", Float) = 0
		_PlanetRadius("Planet radius", Float) = 0
		_PlanetCenter("Planet center", Vector) = (0, 0, 0, 0)
	}
	SubShader 
	{
		Tags{ "RenderType" = "Opaque"}
		LOD 200
		
		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows vertex:vert finalcolor:fcolor

		#include "UnityCG.cginc"
		#include "UnityPBSLighting.cginc"
		#include "Assets/Shaders/Planet/PlanetaryFog.cginc"

		// Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0
		//#pragma debug

		sampler2D _MainTex;

		half _Glossiness;
        half _Metallic;
        fixed4 _Color;

		float _FogParam;			// Determines the density of the atmosphere based on the distance to the camera
		float4 _FogColor;			// The color of the atnosphere

		float _AtmospherePower;		// How much the atmosphere attenuates with increasing altitude of the fragments
		float _AtmosphereRadius;	// The radius of the atmosphere
		float _PlanetRadius;		// The radius of the planet
		float3 _PlanetCenter;		// The origin vector of the planet

		struct Input {
			float2 uv_MainTex;
			float3 worldPos;
			float fogFactor;
		};

		void vert(inout appdata_full v, out Input o)
		{
			o.uv_MainTex = v.texcoord.xy;
			
			o.worldPos = mul(unity_ObjectToWorld, v.vertex);
			float3 worldNormal = normalize(_PlanetCenter - o.worldPos);
			float3 worldPosOnSphere = _PlanetCenter + normalize(o.worldPos - _PlanetCenter) * _PlanetRadius;
			
			float vertexHeightAboveSurface = distance(_PlanetCenter, o.worldPos) - _PlanetRadius;
			float vertexHeight01 = 1.0 - saturate(vertexHeightAboveSurface / (_AtmosphereRadius - _PlanetRadius));
			vertexHeight01 = pow(smoothstep(0, 1, vertexHeight01), _AtmospherePower);
			o.fogFactor = CalculatePlanetaryFogFactor(o.worldPos, _PlanetCenter, _AtmosphereRadius, _FogParam);
			o.fogFactor *= vertexHeight01;
			////o.fogFactor = saturate(o.fogFactor - vertexHeight01 * saturate(1.0 - o.fogFactor));

			// Apply planetary attenuation to the fogFactor
			//o.fogFactor *= CalculatePlanetaryFogAttenuation(worldNormal);
			o.fogFactor *= CalculatePlanetShadowValueForAtmosphere(o.worldPos, _PlanetCenter, _PlanetRadius, _PlanetRadius);
			// TODO: Replace = with *= for the next line of code
			//o.fogFactor = CalculatePlanetaryFogAttenuationNew(worldPosOnSphere, _PlanetCenter, _PlanetRadius, _AtmosphereRadius, true);
		}

		void surf(Input IN, inout SurfaceOutputStandard o) 
		{
			// Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
		}

		void fcolor(Input IN, SurfaceOutputStandard o, inout fixed4 color) 
		{
			fixed3 fogColor = _FogColor.rgb;
     		color.rgb = lerp(color.rgb, fogColor, IN.fogFactor);
		}

		ENDCG
	} 
	FallBack "Diffuse"
}