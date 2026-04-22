Shader "LoogaSoft/Glass"
{
    Properties
    {
        [MainTexture] _BaseMap ("Dirt Albedo (RGB) & Opacity (A)", 2D) = "black" {}
        [MainColor] _BaseColor ("Glass Tint Color", Color) = (0.9, 0.95, 1.0, 1.0)
        
        _NormalMap ("Normal Map", 2D) = "bump" {}
        
        [Toggle(_USE_MASK_MAP)] _UseMaskMap ("Use Mask Map", Float) = 0.0
        _MaskMap ("Mask Map (R:Metallic, G:AO, A:Smoothness)", 2D) = "white" {}
        
        _MetallicMap ("Metallic Map", 2D) = "white" {}
        _Metallic ("Metallic", Range(0, 1)) = 0.0
        _OcclusionMap ("Occlusion Map", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1.0
        
        [Enum(Metallic Alpha, 0, Albedo Alpha, 1)] _SmoothnessTextureChannel ("Smoothness Source", Float) = 0.0
        _Smoothness ("Master Smoothness", Range(0.0, 1.0)) = 0.95

        _Distortion ("Refraction Strength", Range(0.0, 0.5)) = 0.05

        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0
    }
    
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        
        Blend One Zero 
        ZWrite Off
        Cull Back

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            
            // Feature Toggles
            #pragma shader_feature_local _USE_MASK_MAP
            #pragma shader_feature_local _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local _ENVIRONMENTREFLECTIONS_OFF
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.loogasoft.loogalighting/Lighting Shaders/Includes/LoogaLightingHelpers.hlsl" 

            struct AttributesGlass
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
            };

            struct VaryingsGlass
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float4 tangentWS    : TEXCOORD2;
                float3 viewDirWS    : TEXCOORD3;
                float4 screenPos    : TEXCOORD4;
                float3 positionWS   : TEXCOORD5;
            };

            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);  SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MaskMap);    SAMPLER(sampler_MaskMap);
            TEXTURE2D(_MetallicMap);
            TEXTURE2D(_OcclusionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Distortion;
                float _Smoothness;
                float _Metallic;
                float _OcclusionStrength;
                float _SmoothnessTextureChannel;
            CBUFFER_END

            VaryingsGlass Vert(AttributesGlass input)
            {
                VaryingsGlass output;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = input.uv;
                
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
                
                output.screenPos = ComputeScreenPos(vertexInput.positionCS);
                return output;
            }

            half4 Frag(VaryingsGlass input) : SV_Target
            {
                // 1. Texture Sampling
                half4 dirtSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv);
                
                half metallic = 0.0;
                half occlusion = 1.0;
                half baseSmoothness = 0.5;

                // --- MASK VS SEPARATE TEXTURE LOGIC ---
                #if defined(_USE_MASK_MAP)
                    half4 maskSample = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv);
                    metallic = maskSample.r;
                    occlusion = maskSample.g;
                    baseSmoothness = maskSample.a * _Smoothness;
                #else
                    half4 metallicSample = SAMPLE_TEXTURE2D(_MetallicMap, sampler_BaseMap, input.uv);
                    metallic = metallicSample.r * _Metallic;
                    
                    half4 occlusionSample = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_BaseMap, input.uv);
                    occlusion = lerp(1.0, occlusionSample.g, _OcclusionStrength);
                    
                    if (_SmoothnessTextureChannel == 1.0)
                        baseSmoothness = dirtSample.a * _Smoothness; // Albedo Alpha
                    else
                        baseSmoothness = metallicSample.a * _Smoothness; // Metallic Alpha
                #endif

                half perceptualRoughness = 1.0 - baseSmoothness;
                half roughness = perceptualRoughness * perceptualRoughness;

                // F0 is 4% for clean glass, but lerps to the map color for metallic dirt/frames
                half3 f0 = lerp(half3(0.04, 0.04, 0.04), dirtSample.rgb, metallic);

                // 2. Normal Mapping
                half3 normalTS = UnpackNormal(normalSample);
                half sign = input.tangentWS.w * GetOddNegativeScale();
                half3 bitangentWS = cross(input.normalWS, input.tangentWS.xyz) * sign;
                half3 normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS));
                normalWS = NormalizeNormalPerPixel(normalWS);

                // 3. Physical Fresnel
                float NoV = saturate(dot(normalWS, input.viewDirWS));
                float3 F = Fresnel(f0, NoV, roughness); 

                // 4. Refraction (Distorting the Screen UVs)
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float2 refractionOffset = normalTS.xy * _Distortion;
                
                float edgeFade = smoothstep(0.0, 0.1, screenUV.x) * smoothstep(1.0, 0.9, screenUV.x) * smoothstep(0.0, 0.1, screenUV.y) * smoothstep(1.0, 0.9, screenUV.y);
                screenUV += refractionOffset * edgeFade;
                
                // 5. Calculate Background Transmission
                half3 background = SampleSceneColor(screenUV);
                half3 transmission = background * _BaseColor.rgb * (1.0 - F) * (1.0 - dirtSample.a);

                // 6. Dirt Diffuse & Specular Accumulation
                half3 dirtDiffuse = dirtSample.rgb * (1.0 - metallic) * dirtSample.a;
                half3 specularAccumulation = 0.0;
                half3 diffuseAccumulation = 0.0;

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, 1.0);
                
                half3 mainRadiance = mainLight.color * mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half mainNoL = saturate(dot(normalWS, mainLight.direction));
                
                diffuseAccumulation += dirtDiffuse * mainRadiance * mainNoL;
                
                #if !defined(_SPECULARHIGHLIGHTS_OFF)
                    specularAccumulation += EvaluateSecondaryGGXLobe(f0, perceptualRoughness, normalWS, mainLight.direction, input.viewDirWS, NoV, mainRadiance, 1.0);

                    uint pixelLightCount = GetAdditionalLightsCount();
                    for (uint lightIndex = 0; lightIndex < pixelLightCount; lightIndex++)
                    {
                        Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1,1,1,1));
                        half3 dynRadiance = light.color * light.shadowAttenuation * light.distanceAttenuation;
                        half NoL = saturate(dot(normalWS, light.direction));
                        
                        diffuseAccumulation += dirtDiffuse * dynRadiance * NoL;
                        specularAccumulation += EvaluateSecondaryGGXLobe(f0, perceptualRoughness, normalWS, light.direction, input.viewDirWS, NoV, dynRadiance, 1.0);
                    }
                #endif

                // 7. Environment Reflection (Cubemaps)
                half3 environmentReflection = 0.0;
                #if !defined(_ENVIRONMENTREFLECTIONS_OFF)
                    half3 reflectionDir = reflect(-input.viewDirWS, normalWS);
                    environmentReflection = GlossyEnvironmentReflection(reflectionDir, input.positionWS, perceptualRoughness, occlusion);
                #endif
                
                // 8. Final Composite
                half3 finalColor = transmission + diffuseAccumulation + environmentReflection + specularAccumulation;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
        
        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
    }
    
    CustomEditor "LoogaSoft.Lighting.Editor.LoogaGlassShaderGUI"
    Fallback "Universal Render Pipeline/Lit"
}