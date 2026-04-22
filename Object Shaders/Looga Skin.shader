Shader "LoogaSoft/Skin"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Float) = 1.0
        
        [Toggle(_USE_MASK_MAP)] _UseMaskMap ("Use Mask Map", Float) = 0.0
        _MaskMap ("Mask Map (R:Metallic, G:Occlusion, A:Smoothness)", 2D) = "white" {}
        
        _MetallicMap ("Metallic Map", 2D) = "white" {}
        _Metallic ("Metallic", Range(0, 1)) = 0.0
        _OcclusionMap ("Occlusion Map", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1.0
        
        [Enum(Metallic Alpha, 0, Albedo Alpha, 1)] _SmoothnessTextureChannel ("Smoothness Source", Float) = 0.0
        _BaseSmoothnessScale ("Smoothness", Range(0, 1)) = 0.5
        
        _EmissionMap("Emission Map", 2D) = "black" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
        
        _CavityMap ("Cavity/Lobe Mask (R)", 2D) = "white" {}
        _LobeMix ("Oily Layer Strength", Range(0, 1)) = 1.0
        _SecondarySmoothness ("Secondary Smoothness", Range(0, 1)) = 0.85
        
        _SubsurfaceColor ("Subsurface Color", Color) = (0.85, 0.4, 0.25, 1.0)
        _ScatterWidth ("Scatter Width", Range(0.1, 5.0)) = 2.0

        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "GBuffer"
            Tags { "LightMode" = "UniversalGBuffer" }
            
            // --- THE STENCIL FLAG ---
            // This writes Bit 7 (128) to the Stencil Buffer.
            // Your future Screen-Space SSS Render Feature will check for this exact Reference.
            Stencil
            {
                Ref 128
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _USE_MASK_MAP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float4 tangentWS    : TEXCOORD3;
            };

            // Properties
            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);  SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MetallicMap);
            TEXTURE2D(_OcclusionMap);
            TEXTURE2D(_MaskMap);    SAMPLER(sampler_MaskMap);
            TEXTURE2D(_EmissionMap);
            TEXTURE2D(_CavityMap);  SAMPLER(sampler_CavityMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _NormalScale;
                float _Metallic;
                float _OcclusionStrength;
                float _SmoothnessTextureChannel;
                float _BaseSmoothnessScale;
                float4 _EmissionColor;
                float _SecondarySmoothness;
                float _LobeMix;
                float4 _SubsurfaceColor;
                float _ScatterWidth;
            CBUFFER_END

            struct FragmentOutput
            {
                half4 GBuffer0 : SV_Target0;
                half4 GBuffer1 : SV_Target1;
                half4 GBuffer2 : SV_Target2;
                half4 GBuffer3 : SV_Target3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.uv = input.uv;
                output.normalWS = normalInput.normalWS;
                
                // Pack the float3 tangent and the original tangent sign (.w) into the float4 output
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);

                return output;
            }

            FragmentOutput Frag(Varyings input)
            {
                FragmentOutput outGBuffer;

                // 1. Sample Textures
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half4 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv);
                half cavitySample = SAMPLE_TEXTURE2D(_CavityMap, sampler_CavityMap, input.uv).r;

                // 2. Base Properties
                // Skin is dielectric, so metallic is forced to 0 regardless of map, but you could use maskSample.r if desired.
                half metallic = 0.0; 
                half occlusion = 1.0;
                half baseSmoothness = 0.5;
                
                #if defined(_USE_MASK_MAP)
                    half4 maskSample = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv);
                    metallic = maskSample.r;
                    occlusion = maskSample.g;
                    baseSmoothness = maskSample.a * _BaseSmoothnessScale;
                #else
                    // Use base map sampler to save slots
                    half4 metallicSample = SAMPLE_TEXTURE2D(_MetallicMap, sampler_BaseMap, input.uv);
                    metallic = metallicSample.r * _Metallic;
                    half4 occlusionSample = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_BaseMap, input.uv);
                    occlusion = lerp(1.0, occlusionSample.g, _OcclusionStrength);
                
                    if (_SmoothnessTextureChannel == 1.0)
                        baseSmoothness = albedo.a * _BaseSmoothnessScale;
                    else
                        baseSmoothness = metallicSample.a * _BaseSmoothnessScale;
                #endif

                // 3. Normal Map Decoding
                half3 normalTS = UnpackNormalScale(normalSample, _NormalScale);
                half3 viewDirWS = GetCameraPositionWS() - input.positionCS.xyz; // Needed for bitangent
                half sign = input.tangentWS.w * GetOddNegativeScale();
                half3 bitangentWS = cross(input.normalWS, input.tangentWS.xyz) * sign;
                half3 normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS));
                normalWS = NormalizeNormalPerPixel(normalWS);

                // 4. Dual Lobe Properties
                // Convert secondary smoothness to perceptual roughness for packing
                half secondaryRoughness = 1.0 - _SecondarySmoothness;
                
                // Mask the secondary oily layer using cavity map (less oil in deep pores) and the master slider
                half finalLobeMix = cavitySample * _LobeMix;
                
                half4 emissionSample = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, input.uv);
                half3 emission = emissionSample.rgb * _EmissionColor.rgb;

                // 5. PACKING TO GBUFFER
                // Flag 1 (Lit) + 16 (Dual Lobe) + 32 (SSSS) = 49
                outGBuffer.GBuffer0 = half4(albedo.rgb, 49.0 / 255.0);
                
                // GBuffer 1: Metallic (R), Secondary Roughness (G), Lobe Mix (B), Occlusion (A)
                outGBuffer.GBuffer1 = half4(metallic, secondaryRoughness, finalLobeMix, occlusion);
                
                // GBuffer 2: Normal (RGB) and Base Smoothness (A)
                outGBuffer.GBuffer2 = half4(normalWS, baseSmoothness);
                
                // GBuffer 3: Emission (RGB)
                outGBuffer.GBuffer3 = half4(emission, 1.0);

                return outGBuffer;
            }
            ENDHLSL
        }

        // --- SSSS PROFILE PASS ---
        // A lightweight pass executed via custom RendererList to output SSSS parameters
        Pass
        {
            Name "SSSSProfile"
            Tags { "LightMode" = "SSSSProfile" }

            ZWrite Off
            ZTest Equal
            Cull Back

            HLSLPROGRAM
            #pragma vertex VertProfile
            #pragma fragment FragProfile
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _SubsurfaceColor;
                float _ScatterWidth;
            CBUFFER_END

            Varyings VertProfile(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 FragProfile(Varyings input) : SV_Target
            {
                // Pack RGB Color and normalize Width (0-5 range mapped to 0-1)
                return half4(_SubsurfaceColor.rgb, _ScatterWidth / 5.0);
            }
            ENDHLSL
        }
        
        // Include Standard ShadowCaster and DepthOnly passes here so the skin casts shadows properly
        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        
    }

    CustomEditor "LoogaSoft.Lighting.Editor.LoogaSkinShaderGUI"
    Fallback "Universal Render Pipeline/Lit"
}