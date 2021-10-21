Shader "Space/Skybox" {
	Properties{
	   _Exposure("Exposure", Range(0, 10)) = 0.0
	   _Rotation("Rotation", Range(0, 360)) = 0
	   _RotationAxis("Rotation axis", Vector) = (0, 1, 0)
	   _Tex("Cubemap   (HDR)", Cube) = "grey" {}
	}

		SubShader{
			Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
			Cull Off ZWrite Off

			Pass {

				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma target 2.0

				#include "UnityCG.cginc"

				samplerCUBE _Tex;
				half4 _Tex_HDR;
				half4 _Tint;
				half _Exposure;
				float _Rotation;
				float3 _RotationAxis;

				float3 RotateAroundYInDegrees(float3 vertex, float degrees)
				{
					float alpha = degrees * UNITY_PI / 180.0;
					float sina, cosa;
					sincos(alpha, sina, cosa);
					float2x2 m = float2x2(cosa, -sina, sina, cosa);
					return float3(mul(m, vertex.xz), vertex.y).xzy;
				}

				float4x4 rotationMatrix(float3 axis, float angle)
				 {
					 axis = normalize(axis);
					 float s = sin(angle);
					 float c = cos(angle);
					 float oc = 1.0 - c;

					 return float4x4(oc * axis.x * axis.x + c,           oc * axis.x * axis.y - axis.z * s,  oc * axis.z * axis.x + axis.y * s,  0.0,
								 oc * axis.x * axis.y + axis.z * s,  oc * axis.y * axis.y + c,           oc * axis.y * axis.z - axis.x * s,  0.0,
								 oc * axis.z * axis.x - axis.y * s,  oc * axis.y * axis.z + axis.x * s,  oc * axis.z * axis.z + c,           0.0,
								 0.0,                                0.0,                                0.0,                                1.0);
				 }

				struct appdata_t {
					float4 vertex : POSITION;
				};

				struct v2f {
					float4 vertex : SV_POSITION;
					float3 texcoord : TEXCOORD0;
				};

				v2f vert(appdata_t v)
				{
					v2f o;
					//float3 rotated = RotateAroundYInDegrees(v.vertex, _Rotation);
					float3 rotated = mul(rotationMatrix(normalize(_RotationAxis.xyz), _Rotation * UNITY_PI / 180.0), v.vertex).xyz;
					o.vertex = UnityObjectToClipPos(rotated);
					o.texcoord = v.vertex.xyz;
					return o;
				}

				fixed4 frag(v2f i) : SV_Target
				{
					half4 tex = texCUBE(_Tex, i.texcoord) * _Exposure;

					half3 c = DecodeHDR(tex, _Tex_HDR);

					return half4(c, 1);
				}
				ENDCG
			}
	}
		Fallback Off

}