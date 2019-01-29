// Upgrade NOTE: upgraded instancing buffer 'Props' to new syntax.

Shader "MadCake/Material/Minimal skinned shader" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200
		
		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows vertex:vert

		#pragma target 5.0

		struct Input {
        	float2 uv_MainTex;
        	float4 color : COLOR;
     	};

     	sampler2D skinned_data_1;
		sampler2D skinned_data_2;
		sampler2D skinned_data_3;
		uint skinned_tex_height;
		uint skinned_tex_width;
		bool _DoSkinning;

		//struct appdata_base {
		//	float4 vertex : POSITION;
		//	float3 normal : NORMAL;
		//	float4 texcoord : TEXCOORD0;
		//	UNITY_VERTEX_INPUT_INSTANCE_ID
		//};

		//struct appdata_tan {
		//	float4 vertex : POSITION;
		//	float4 tangent : TANGENT;
		//	float3 normal : NORMAL;
		//	float4 texcoord : TEXCOORD0;
		//	UNITY_VERTEX_INPUT_INSTANCE_ID
		//};

		//struct appdata_full {
		//	float4 vertex : POSITION;
		//	float4 tangent : TANGENT;
		//	float3 normal : NORMAL;
		//	float4 texcoord : TEXCOORD0;
		//	float4 texcoord1 : TEXCOORD1;
		//	float4 texcoord2 : TEXCOORD2;
		//	float4 texcoord3 : TEXCOORD3;
		//	fixed4 color : COLOR;
		//	UNITY_VERTEX_INPUT_INSTANCE_ID
		//};

		struct appdata_minimal_skinning {
			float4 vertex		: POSITION;
			float3 normal		: NORMAL;
			float4 tangent		: TANGENT;
			float4 texcoord		: TEXCOORD0;
			float4 texcoord1	: TEXCOORD1;
			float4 texcoord2	: TEXCOORD2;
			uint id				: SV_VertexID;
		};

		void vert (inout appdata_minimal_skinning v) {
			if(_DoSkinning){
				float2 skinned_tex_uv;

				skinned_tex_uv.x = (float(v.id % skinned_tex_width)) / skinned_tex_width;
				skinned_tex_uv.y = (float(v.id / skinned_tex_width)) / skinned_tex_height;

				float4 data_1 = tex2Dlod(skinned_data_1, float4(skinned_tex_uv, 0, 0));
				float4 data_2 = tex2Dlod(skinned_data_2, float4(skinned_tex_uv, 0, 0));
				float2 data_3 = tex2Dlod(skinned_data_3, float4(skinned_tex_uv, 0, 0)).xy;

				v.vertex.xyz = data_1.xyz;
				v.vertex.w = 1;

				v.normal.x = data_1.w;
				v.normal.yz = data_2.xy;

				v.tangent.xy = data_2.zw;
				v.tangent.z = data_3.x;
			}
		}

		sampler2D _MainTex;

		half _Glossiness;
		half _Metallic;
		fixed4 _Color;

		// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
		// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
		// #pragma instancing_options assumeuniformscaling
		UNITY_INSTANCING_BUFFER_START(Props)
			// put more per-instance properties here
		UNITY_INSTANCING_BUFFER_END(Props)

		void surf (Input IN, inout SurfaceOutputStandard o) {
			// Albedo comes from a texture tinted by color
			fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
			o.Albedo = c;
			// Metallic and smoothness come from slider variables
			o.Metallic = _Metallic;
			o.Smoothness = _Glossiness;
			o.Alpha = 1;
		}

		ENDCG
	}
	FallBack "Diffuse"
}
