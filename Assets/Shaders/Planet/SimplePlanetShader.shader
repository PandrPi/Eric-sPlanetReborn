Shader "Custom/SimplePlanetShader"
{
	Properties 
	{
		_Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
		_FogColor ("Fog Color (RGB)", Color) = (0.5, 0.5, 0.5, 1.0)

		_AtmospherePower("Atmophere power", Float) = 0
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
		#pragma debug

		sampler2D _MainTex;

		half _Glossiness;
        half _Metallic;
        fixed4 _Color;

		float _FogParam;
		float4 _FogColor;

		float _AtmospherePower;
		float _AtmosphereRadius;
		float _PlanetRadius;
		float3 _PlanetCenter;

		struct Input {
			float2 uv_MainTex;
			float3 worldPos;
			float fogFactor;
		};

		void vert(inout appdata_full v, out Input o)
		{
			o.uv_MainTex = v.texcoord.xy;
			
			o.worldPos = mul(unity_ObjectToWorld, v.vertex);
			
			float vertexHeightAboveSurface = distance(_PlanetCenter, o.worldPos) - _PlanetRadius;
			float vertexHeight01 = 1.0 - saturate(vertexHeightAboveSurface / (_AtmosphereRadius - _PlanetRadius));
			//vertexHeight01 = smoothstep(0, 1, vertexHeight01);
			vertexHeight01 = pow(vertexHeight01, _AtmospherePower);
			o.fogFactor = CalculatePlanetaryFogFactor(o.worldPos, _PlanetCenter, _AtmosphereRadius, _FogParam) * vertexHeight01;
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