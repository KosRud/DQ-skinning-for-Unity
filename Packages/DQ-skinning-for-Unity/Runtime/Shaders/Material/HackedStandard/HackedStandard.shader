// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)

/*
    modified to use MadCake's dual quaternion skinning script

    modifications marked by	comments:
        // ----- DQ modification start -----
        ---inserted code---
        // ----- DQ modification end -----

    modifications added to following passes:
        - Forward
        - ShadowCaster
        - Deferred
*/

Shader "MadCake/Material/Standard hacked for DQ skinning"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _MainTex("Albedo", 2D) = "white" {}

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _Glossiness("Smoothness", Range(0.0, 1.0)) = 0.5
        _GlossMapScale("Smoothness Scale", Range(0.0, 1.0)) = 1.0
        [Enum(Metallic Alpha,0,Albedo Alpha,1)] _SmoothnessTextureChannel ("Smoothness texture channel", Float) = 0

        [Gamma] _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}

        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _GlossyReflections("Glossy Reflections", Float) = 1.0

        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}

        _Parallax ("Height Scale", Range (0.005, 0.08)) = 0.02
        _ParallaxMap ("Height Map", 2D) = "black" {}

        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}

        _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}

        _DetailMask("Detail Mask", 2D) = "white" {}

        _DetailAlbedoMap("Detail Albedo x2", 2D) = "grey" {}
        _DetailNormalMapScale("Scale", Float) = 1.0
        _DetailNormalMap("Normal Map", 2D) = "bump" {}

        [Enum(UV0,0,UV1,1)] _UVSec ("UV Set for secondary textures", Float) = 0


        // Blending state
        [HideInInspector] _Mode ("__mode", Float) = 0.0
        [HideInInspector] _SrcBlend ("__src", Float) = 1.0
        [HideInInspector] _DstBlend ("__dst", Float) = 0.0
        [HideInInspector] _ZWrite ("__zw", Float) = 1.0
    }

    CGINCLUDE
        #define UNITY_SETUP_BRDF_INPUT MetallicSetup
    ENDCG

    SubShader
    {
        Tags { "RenderType"="Opaque" "PerformanceChecks"="False" }
        LOD 300


        // ------------------------------------------------------------------
        //  Base forward pass (directional light, emission, lightmaps, ...)
        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            CGPROGRAM
            #pragma target 3.0

            // -------------------------------------

            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature _EMISSION
            #pragma shader_feature _METALLICGLOSSMAP
            #pragma shader_feature ___ _DETAIL_MULX2
            #pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature _ _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature _ _GLOSSYREFLECTIONS_OFF
            #pragma shader_feature _PARALLAXMAP

            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            // Uncomment the following line to enable dithering LOD crossfade. Note: there are more in the file to uncomment for other passes.
            //#pragma multi_compile _ LOD_FADE_CROSSFADE

            //#pragma vertex vertForwardBase
            #pragma fragment fragBase
            #include "UnityStandardCoreForward.cginc"

            // ----- DQ modification start -----

            #pragma vertex vertSkinnedForward // original #pragma vertex (above) was commented

            // expanded version of original vertex input structure
            struct VertexInputSkinningForward
            {
                float4 vertex   : POSITION;
                half3 normal    : NORMAL;
                float2 uv0      : TEXCOORD0;
                float2 uv1      : TEXCOORD1;

                // this was added, everything else remains unchanged
                uint id : SV_VertexID;

                #if defined(DYNAMICLIGHTMAP_ON) || defined(UNITY_PASS_META)
                    float2 uv2      : TEXCOORD2;
                #endif

                #ifdef _TANGENT_TO_WORLD
                    half4 tangent   : TANGENT;
                #endif

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // variables used for skining, always the same for every pass
            sampler2D skinned_data_1;
            sampler2D skinned_data_2;
            sampler2D skinned_data_3;
            uint skinned_tex_height;
            uint skinned_tex_width;
            bool _DoSkinning;

            // the actual skinning function
            // don't change the code but change the argument type to the name of vertex input structure used in current pass
            // for this pass it is VertexInputSkinningForward
            void vert(inout VertexInputSkinningForward v) {
                if (_DoSkinning) {
                    float2 skinned_tex_uv;

                    skinned_tex_uv.x = (float(v.id % skinned_tex_width)) / skinned_tex_width;
                    skinned_tex_uv.y = (float(v.id / skinned_tex_width)) / skinned_tex_height;

                    float4 data_1 = tex2Dlod(skinned_data_1, float4(skinned_tex_uv, 0, 0));
                    float4 data_2 = tex2Dlod(skinned_data_2, float4(skinned_tex_uv, 0, 0));

                    #ifdef _TANGENT_TO_WORLD
                        float2 data_3 = tex2Dlod(skinned_data_3, float4(skinned_tex_uv, 0, 0)).xy;
                    #endif

                    v.vertex.xyz = data_1.xyz;
                    v.vertex.w = 1;

                    v.normal.x = data_1.w;
                    v.normal.yz = data_2.xy;

                    #ifdef _TANGENT_TO_WORLD
                        v.tangent.xy = data_2.zw;
                        v.tangent.zw = data_3.xy;
                    #endif
                }
            }

            // this function will replace the original vertex function (vertForwardBase)
            // the return type is the same as in the original function
            // the argument type is our expanded structure
            VertexOutputForwardBase vertSkinnedForward(VertexInputSkinningForward vs)
            {
                // first we apply skinning
                vert(vs);

                // then we create the original vertex structure (VertexInput for this pass)
                // and fill it with the data from our expanded version
                VertexInput v;
                v.vertex = vs.vertex;
                v.normal = vs.normal;
                v.uv0 = vs.uv0;
                v.uv1 = vs.uv1;

                // this variable is inside an "if defined" block in original structure
                // so accessing it should be enclosed in identical "if defined" block
                #if defined(DYNAMICLIGHTMAP_ON) || defined(UNITY_PASS_META)
                    v.uv2 = vs.uv2;
                #endif

                // same here
                #ifdef _TANGENT_TO_WORLD
                    v.tangent = vs.tangent;
                #endif

                // finally we pass the original vertex structure to the original vertex function
                // and return the result
                return vertForwardBase(v);
            }

            // ----- DQ modification end -----

            ENDCG
        }
        // ------------------------------------------------------------------
        //  Additive forward pass (one light per pass)
        Pass
        {
            Name "FORWARD_DELTA"
            Tags { "LightMode" = "ForwardAdd" }
            Blend [_SrcBlend] One
            Fog { Color (0,0,0,0) } // in additive pass fog should be black
            ZWrite Off
            ZTest LEqual

            CGPROGRAM
            #pragma target 3.0

            // -------------------------------------


            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature _METALLICGLOSSMAP
            #pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature _ _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature ___ _DETAIL_MULX2
            #pragma shader_feature _PARALLAXMAP

            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog
            // Uncomment the following line to enable dithering LOD crossfade. Note: there are more in the file to uncomment for other passes.
            //#pragma multi_compile _ LOD_FADE_CROSSFADE

            //#pragma vertex vertAdd
            #pragma fragment fragAdd
            #include "UnityStandardCoreForward.cginc"

            // ----- DQ modification start -----

            #pragma vertex vertSkinnedForwardAdd // original #pragma vertex (above) was commented

            struct VertexInputSkinningForwardAdd
            {
                float4 vertex   : POSITION;
                half3 normal    : NORMAL;
                float2 uv0      : TEXCOORD0;
                float2 uv1      : TEXCOORD1;
                uint id : SV_VertexID;

                #if defined(DYNAMICLIGHTMAP_ON) || defined(UNITY_PASS_META)
                    float2 uv2      : TEXCOORD2;
                #endif

                #ifdef _TANGENT_TO_WORLD
                    half4 tangent   : TANGENT;
                #endif

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D skinned_data_1;
            sampler2D skinned_data_2;
            sampler2D skinned_data_3;
            uint skinned_tex_height;
            uint skinned_tex_width;
            bool _DoSkinning;

            void vert(inout VertexInputSkinningForwardAdd v) {
                if (_DoSkinning) {
                    float2 skinned_tex_uv;

                    skinned_tex_uv.x = (float(v.id % skinned_tex_width)) / skinned_tex_width;
                    skinned_tex_uv.y = (float(v.id / skinned_tex_width)) / skinned_tex_height;

                    float4 data_1 = tex2Dlod(skinned_data_1, float4(skinned_tex_uv, 0, 0));
                    float4 data_2 = tex2Dlod(skinned_data_2, float4(skinned_tex_uv, 0, 0));

                    #ifdef _TANGENT_TO_WORLD
                        float2 data_3 = tex2Dlod(skinned_data_3, float4(skinned_tex_uv, 0, 0)).xy;
                    #endif

                    v.vertex.xyz = data_1.xyz;
                    v.vertex.w = 1;

                    v.normal.x = data_1.w;
                    v.normal.yz = data_2.xy;

                    #ifdef _TANGENT_TO_WORLD
                        v.tangent.xy = data_2.zw;
                        v.tangent.zw = data_3.xy;
                    #endif
                }
            }

            VertexOutputForwardAdd vertSkinnedForwardAdd(VertexInputSkinningForwardAdd vs)
            {
                vert(vs);

                VertexInput v;
                v.vertex = vs.vertex;
                v.normal = vs.normal;
                v.uv0 = vs.uv0;
                v.uv1 = vs.uv1;

                #if defined(DYNAMICLIGHTMAP_ON) || defined(UNITY_PASS_META)
                    v.uv2 = vs.uv2;
                #endif

                #ifdef _TANGENT_TO_WORLD
                    v.tangent = vs.tangent;
                #endif

                return vertAdd(v);
            }

            // ----- DQ modification end -----

            ENDCG
        }
        // ------------------------------------------------------------------
        //  Shadow rendering pass
        Pass {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On ZTest LEqual

            CGPROGRAM
            #pragma target 3.0

            // -------------------------------------


            #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature _METALLICGLOSSMAP
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature _PARALLAXMAP
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            // Uncomment the following line to enable dithering LOD crossfade. Note: there are more in the file to uncomment for other passes.
            //#pragma multi_compile _ LOD_FADE_CROSSFADE

            //#pragma vertex verthadowCaster
            #pragma fragment fragShadowCaster

            #include "UnityStandardShadow.cginc"

            // ----- DQ modification start -----

            #pragma vertex vertSkinnedShadowCaster // original #pragma vertex (above) was commented

            struct VertexInputSkinningShadowCaster
            {
                float4 vertex   : POSITION;
                half3 normal    : NORMAL;
                float2 uv0      : TEXCOORD0;
                uint id : SV_VertexID;

                #if defined(UNITY_STANDARD_USE_SHADOW_UVS) && defined(_PARALLAXMAP)
                    half4 tangent   : TANGENT;
                #endif

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D skinned_data_1;
            sampler2D skinned_data_2;
            sampler2D skinned_data_3;
            uint skinned_tex_height;
            uint skinned_tex_width;
            bool _DoSkinning;

            void vert(inout VertexInputSkinningShadowCaster v) {
                if (_DoSkinning) {
                    float2 skinned_tex_uv;

                    skinned_tex_uv.x = (float(v.id % skinned_tex_width)) / skinned_tex_width;
                    skinned_tex_uv.y = (float(v.id / skinned_tex_width)) / skinned_tex_height;

                    float4 data_1 = tex2Dlod(skinned_data_1, float4(skinned_tex_uv, 0, 0));
                    float4 data_2 = tex2Dlod(skinned_data_2, float4(skinned_tex_uv, 0, 0));

                    #if defined(UNITY_STANDARD_USE_SHADOW_UVS) && defined(_PARALLAXMAP)
                        float2 data_3 = tex2Dlod(skinned_data_3, float4(skinned_tex_uv, 0, 0)).xy;
                    #endif

                    v.vertex.xyz = data_1.xyz;
                    v.vertex.w = 1;

                    v.normal.x = data_1.w;
                    v.normal.yz = data_2.xy;

                    #if defined(UNITY_STANDARD_USE_SHADOW_UVS) && defined(_PARALLAXMAP)
                        v.tangent.xy = data_2.zw;
                        v.tangent.zw = data_3.xy;
                    #endif
                }
            }

            void vertSkinnedShadowCaster(
                VertexInputSkinningShadowCaster vs,
                out float4 opos : SV_POSITION

                #ifdef UNITY_STANDARD_USE_SHADOW_OUTPUT_STRUCT
                    , out VertexOutputShadowCaster o
                #endif

                #ifdef UNITY_STANDARD_USE_STEREO_SHADOW_OUTPUT_STRUCT
                    , out VertexOutputStereoShadowCaster os
                #endif
            )
            {
                vert(vs);

                VertexInput v;
                v.vertex = vs.vertex;
                v.normal = vs.normal;
                v.uv0 = vs.uv0;

                #if defined(UNITY_STANDARD_USE_SHADOW_UVS) && defined(_PARALLAXMAP)
                    v.tangent = vs.tangent;
                #endif

                vertShadowCaster(
                    v,
                    opos

                    #ifdef UNITY_STANDARD_USE_SHADOW_OUTPUT_STRUCT
                        , o
                    #endif

                    #ifdef UNITY_STANDARD_USE_STEREO_SHADOW_OUTPUT_STRUCT
                        , os
                    #endif
                );
            }

            // ----- DQ modification end -----

            ENDCG
        }
        // ------------------------------------------------------------------
        //  Deferred pass
        Pass
        {
            Name "DEFERRED"
            Tags { "LightMode" = "Deferred" }

            CGPROGRAM
            #pragma target 3.0
            #pragma exclude_renderers nomrt


            // -------------------------------------

            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature _EMISSION
            #pragma shader_feature _METALLICGLOSSMAP
            #pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature _ _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature ___ _DETAIL_MULX2
            #pragma shader_feature _PARALLAXMAP

            #pragma multi_compile_prepassfinal
            #pragma multi_compile_instancing
            // Uncomment the following line to enable dithering LOD crossfade. Note: there are more in the file to uncomment for other passes.
            //#pragma multi_compile _ LOD_FADE_CROSSFADE

            //#pragma vertex vertDeferred
            #pragma fragment fragDeferred

            #include "UnityStandardCore.cginc"

            // ----- DQ modification start -----

            #pragma vertex vertSkinnedDeferred // original #pragma vertex (above) was commented

            struct VertexInputSkinningDeferred
                {
                    float4 vertex   : POSITION;
                    half3 normal    : NORMAL;
                    float2 uv0      : TEXCOORD0;
                    float2 uv1      : TEXCOORD1;
                    uint id : SV_VertexID;

                    #if defined(DYNAMICLIGHTMAP_ON) || defined(UNITY_PASS_META)
                        float2 uv2      : TEXCOORD2;
                    #endif

                    #ifdef _TANGENT_TO_WORLD
                        half4 tangent   : TANGENT;
                    #endif
                    

                    UNITY_VERTEX_INPUT_INSTANCE_ID
                };

            sampler2D skinned_data_1;
            sampler2D skinned_data_2;
            sampler2D skinned_data_3;
            uint skinned_tex_height;
            uint skinned_tex_width;
            bool _DoSkinning;

            void vert(inout VertexInputSkinningDeferred v) {
                if (_DoSkinning) {
                    float2 skinned_tex_uv;

                    skinned_tex_uv.x = (float(v.id % skinned_tex_width)) / skinned_tex_width;
                    skinned_tex_uv.y = (float(v.id / skinned_tex_width)) / skinned_tex_height;

                    float4 data_1 = tex2Dlod(skinned_data_1, float4(skinned_tex_uv, 0, 0));
                    float4 data_2 = tex2Dlod(skinned_data_2, float4(skinned_tex_uv, 0, 0));

                    #ifdef _TANGENT_TO_WORLD
                        float2 data_3 = tex2Dlod(skinned_data_3, float4(skinned_tex_uv, 0, 0)).xy;
                    #endif

                    v.vertex.xyz = data_1.xyz;
                    v.vertex.w = 1;

                    v.normal.x = data_1.w;
                    v.normal.yz = data_2.xy;

                    #ifdef _TANGENT_TO_WORLD
                        v.tangent.xy = data_2.zw;
                        v.tangent.zw = data_3.xy;
                    #endif
                }
            }

            VertexOutputDeferred vertSkinnedDeferred(VertexInputSkinningDeferred vs)
            {
                vert(vs);

                VertexInput v;
                v.vertex = vs.vertex;
                v.normal = vs.normal;
                v.uv0 = vs.uv0;
                v.uv1 = vs.uv1;

                #if defined(DYNAMICLIGHTMAP_ON) || defined(UNITY_PASS_META)
                    v.uv2 = vs.uv2;
                #endif

                #ifdef _TANGENT_TO_WORLD
                    v.tangent = vs.tangent;
                #endif

                return vertDeferred(v);
            }

            // ----- DQ modification end -----

            ENDCG
        }

        // ------------------------------------------------------------------
        // Extracts information for lightmapping, GI (emission, albedo, ...)
        // This pass it not used during regular rendering.
        Pass
        {
            Name "META"
            Tags { "LightMode"="Meta" }

            Cull Off

            CGPROGRAM
            //#pragma vertex vert_meta
            #pragma fragment frag_meta

            #pragma shader_feature _EMISSION
            #pragma shader_feature _METALLICGLOSSMAP
            #pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature ___ _DETAIL_MULX2
            #pragma shader_feature EDITOR_VISUALIZATION

            #include "UnityStandardMeta.cginc"

            // ----- DQ modification start -----

            #pragma vertex vertSkinnedMeta // original #pragma vertex (above) was commented

            struct VertexInputSkinningMeta
                {
                    float4 vertex   : POSITION;
                    half3 normal    : NORMAL;
                    float2 uv0      : TEXCOORD0;
                    float2 uv1      : TEXCOORD1;
                    uint id : SV_VertexID;

                    #if defined(DYNAMICLIGHTMAP_ON) || defined(UNITY_PASS_META)
                        float2 uv2      : TEXCOORD2;
                    #endif

                    #ifdef _TANGENT_TO_WORLD
                        half4 tangent   : TANGENT;
                    #endif


                    UNITY_VERTEX_INPUT_INSTANCE_ID
                };

            sampler2D skinned_data_1;
            sampler2D skinned_data_2;
            sampler2D skinned_data_3;
            uint skinned_tex_height;
            uint skinned_tex_width;
            bool _DoSkinning;

            void vert(inout VertexInputSkinningMeta v) {
                if (_DoSkinning) {
                    float2 skinned_tex_uv;

                    skinned_tex_uv.x = (float(v.id % skinned_tex_width)) / skinned_tex_width;
                    skinned_tex_uv.y = (float(v.id / skinned_tex_width)) / skinned_tex_height;

                    float4 data_1 = tex2Dlod(skinned_data_1, float4(skinned_tex_uv, 0, 0));
                    float4 data_2 = tex2Dlod(skinned_data_2, float4(skinned_tex_uv, 0, 0));

                    #ifdef _TANGENT_TO_WORLD
                        float2 data_3 = tex2Dlod(skinned_data_3, float4(skinned_tex_uv, 0, 0)).xy;
                    #endif

                    v.vertex.xyz = data_1.xyz;
                    v.vertex.w = 1;

                    v.normal.x = data_1.w;
                    v.normal.yz = data_2.xy;

                    #ifdef _TANGENT_TO_WORLD
                        v.tangent.xy = data_2.zw;
                        v.tangent.zw = data_3.xy;
                    #endif
                }
            }

            v2f_meta vertSkinnedMeta(VertexInputSkinningMeta vs)
            {
                vert(vs);

                VertexInput v;
                v.vertex = vs.vertex;
                v.normal = vs.normal;
                v.uv0 = vs.uv0;
                v.uv1 = vs.uv1;

                #if defined(DYNAMICLIGHTMAP_ON) || defined(UNITY_PASS_META)
                    v.uv2 = vs.uv2;
                #endif

                #ifdef _TANGENT_TO_WORLD
                    v.tangent = vs.tangent;
                #endif

                return vert_meta(v);
            }

            // ----- DQ modification end -----


            ENDCG
        }
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "PerformanceChecks"="False" }
        LOD 150

        // ------------------------------------------------------------------
        //  Base forward pass (directional light, emission, lightmaps, ...)
        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            CGPROGRAM
            #pragma target 2.0

            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature _EMISSION
            #pragma shader_feature _METALLICGLOSSMAP
            #pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature _ _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature _ _GLOSSYREFLECTIONS_OFF
            // SM2.0: NOT SUPPORTED shader_feature ___ _DETAIL_MULX2
            // SM2.0: NOT SUPPORTED shader_feature _PARALLAXMAP

            #pragma skip_variants SHADOWS_SOFT DIRLIGHTMAP_COMBINED

            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog

            //#pragma vertex vertBase
            #pragma fragment fragBase
            #include "UnityStandardCoreForward.cginc"

            // ----- DQ modification start -----

            #pragma vertex vertSkinnedForward // original #pragma vertex (above) was commented

            struct VertexInputSkinningForward
            {
                float4 vertex   : POSITION;
                half3 normal    : NORMAL;
                float2 uv0      : TEXCOORD0;
                float2 uv1      : TEXCOORD1;
                uint id : SV_VertexID;

                #if defined(DYNAMICLIGHTMAP_ON) || defined(UNITY_PASS_META)
                    float2 uv2      : TEXCOORD2;
                #endif

                #ifdef _TANGENT_TO_WORLD
                    half4 tangent   : TANGENT;
                #endif
                

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D skinned_data_1;
            sampler2D skinned_data_2;
            sampler2D skinned_data_3;
            uint skinned_tex_height;
            uint skinned_tex_width;
            bool _DoSkinning;

            void vert(inout VertexInputSkinningForward v) {
                if (_DoSkinning) {
                    float2 skinned_tex_uv;

                    skinned_tex_uv.x = (float(v.id % skinned_tex_width)) / skinned_tex_width;
                    skinned_tex_uv.y = (float(v.id / skinned_tex_width)) / skinned_tex_height;

                    float4 data_1 = tex2Dlod(skinned_data_1, float4(skinned_tex_uv, 0, 0));
                    float4 data_2 = tex2Dlod(skinned_data_2, float4(skinned_tex_uv, 0, 0));

                    #ifdef _TANGENT_TO_WORLD
                        float2 data_3 = tex2Dlod(skinned_data_3, float4(skinned_tex_uv, 0, 0)).xy;
                    #endif

                    v.vertex.xyz = data_1.xyz;
                    v.vertex.w = 1;

                    v.normal.x = data_1.w;
                    v.normal.yz = data_2.xy;

                    #ifdef _TANGENT_TO_WORLD
                        v.tangent.xy = data_2.zw;
                        v.tangent.zw = data_3.xy;
                    #endif
                }
            }

            VertexOutputForwardBase vertSkinnedForward(VertexInputSkinningForward vs)
            {
                vert(vs);

                VertexInput v;
                v.vertex = vs.vertex;
                v.normal = vs.normal;
                v.uv0 = vs.uv0;
                v.uv1 = vs.uv1;
            #if defined(DYNAMICLIGHTMAP_ON) || defined(UNITY_PASS_META)
                v.uv2 = vs.uv2;
            #endif

            #ifdef _TANGENT_TO_WORLD
                v.tangent = vs.tangent;
            #endif
                return vertForwardBase(v);
            }

            // ----- DQ modification end -----

            ENDCG
        }
        // ------------------------------------------------------------------
        //  Additive forward pass (one light per pass)
        Pass
        {
            Name "FORWARD_DELTA"
            Tags { "LightMode" = "ForwardAdd" }
            Blend [_SrcBlend] One
            Fog { Color (0,0,0,0) } // in additive pass fog should be black
            ZWrite Off
            ZTest LEqual

            CGPROGRAM
            #pragma target 2.0

            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature _METALLICGLOSSMAP
            #pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature _ _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature ___ _DETAIL_MULX2
            // SM2.0: NOT SUPPORTED shader_feature _PARALLAXMAP
            #pragma skip_variants SHADOWS_SOFT

            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog

            #pragma vertex vertAdd
            #pragma fragment fragAdd
            #include "UnityStandardCoreForward.cginc"

            ENDCG
        }
        // ------------------------------------------------------------------
        //  Shadow rendering pass
        Pass {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On ZTest LEqual

            CGPROGRAM
            #pragma target 2.0

            #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma shader_feature _METALLICGLOSSMAP
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma skip_variants SHADOWS_SOFT
            #pragma multi_compile_shadowcaster

            //#pragma vertex vertShadowCaster
            #pragma fragment fragShadowCaster

            #include "UnityStandardShadow.cginc"

            // ----- DQ modification start -----

            #pragma vertex vertSkinnedShadowCaster // original #pragma vertex (above) was commented

            struct VertexInputSkinningShadowCaster
            {
                float4 vertex   : POSITION;
                half3 normal    : NORMAL;
                float2 uv0      : TEXCOORD0;
                uint id : SV_VertexID;

                #if defined(UNITY_STANDARD_USE_SHADOW_UVS) && defined(_PARALLAXMAP)
                    half4 tangent   : TANGENT;
                #endif

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D skinned_data_1;
            sampler2D skinned_data_2;
            sampler2D skinned_data_3;
            uint skinned_tex_height;
            uint skinned_tex_width;
            bool _DoSkinning;

            void vert(inout VertexInputSkinningShadowCaster v) {
                if (_DoSkinning) {
                    float2 skinned_tex_uv;

                    skinned_tex_uv.x = (float(v.id % skinned_tex_width)) / skinned_tex_width;
                    skinned_tex_uv.y = (float(v.id / skinned_tex_width)) / skinned_tex_height;

                    float4 data_1 = tex2Dlod(skinned_data_1, float4(skinned_tex_uv, 0, 0));
                    float4 data_2 = tex2Dlod(skinned_data_2, float4(skinned_tex_uv, 0, 0));

                    #if defined(UNITY_STANDARD_USE_SHADOW_UVS) && defined(_PARALLAXMAP)
                        float2 data_3 = tex2Dlod(skinned_data_3, float4(skinned_tex_uv, 0, 0)).xy;
                    #endif

                    v.vertex.xyz = data_1.xyz;
                    v.vertex.w = 1;

                    v.normal.x = data_1.w;
                    v.normal.yz = data_2.xy;

                    #if defined(UNITY_STANDARD_USE_SHADOW_UVS) && defined(_PARALLAXMAP)
                        v.tangent.xy = data_2.zw;
                        v.tangent.zw = data_3.xy;
                    #endif
                }
            }

            void vertSkinnedShadowCaster(
                VertexInputSkinningShadowCaster vs,
                out float4 opos : SV_POSITION

                #ifdef UNITY_STANDARD_USE_SHADOW_OUTPUT_STRUCT
                    , out VertexOutputShadowCaster o
                #endif

                #ifdef UNITY_STANDARD_USE_STEREO_SHADOW_OUTPUT_STRUCT
                    , out VertexOutputStereoShadowCaster os
                #endif
            )
            {
                vert(vs);

                VertexInput v;
                v.vertex = vs.vertex;
                v.normal = vs.normal;
                v.uv0 = vs.uv0;

                #if defined(UNITY_STANDARD_USE_SHADOW_UVS) && defined(_PARALLAXMAP)
                    v.tangent = vs.tangent;
                #endif

                vertShadowCaster(
                    v,
                    opos

                    #ifdef UNITY_STANDARD_USE_SHADOW_OUTPUT_STRUCT
                        , o
                    #endif

                    #ifdef UNITY_STANDARD_USE_STEREO_SHADOW_OUTPUT_STRUCT
                        , os
                    #endif
                );
            }

            // ----- DQ modification end -----

            ENDCG
        }

        // ------------------------------------------------------------------
        // Extracts information for lightmapping, GI (emission, albedo, ...)
        // This pass it not used during regular rendering.
        Pass
        {
            Name "META"
            Tags { "LightMode"="Meta" }

            Cull Off

            CGPROGRAM
            #pragma vertex vert_meta
            #pragma fragment frag_meta

            #pragma shader_feature _EMISSION
            #pragma shader_feature _METALLICGLOSSMAP
            #pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature ___ _DETAIL_MULX2
            #pragma shader_feature EDITOR_VISUALIZATION

            #include "UnityStandardMeta.cginc"
            ENDCG
        }
    }


    FallBack "VertexLit"
    CustomEditor "StandardShaderGUI"
}