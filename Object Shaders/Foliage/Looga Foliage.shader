Shader "LoogaSoft/Foliage"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo & Alpha", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _Smoothness ("Smoothness", Range(0, 1)) = 0.1

        _SubsurfaceColor ("Subsurface Color", Color) = (0.6, 0.8, 0.2, 1.0)
        _ScatterWidth ("Scatter Width", Range(0.1, 5.0)) = 1.5

        _WindInfluence ("Wind Influence", Range(0.0, 1.0)) = 1.0

        _GlobalGridScale ("Global Grid Scale", Float) = 0.1
        _GlobalHueVar ("Global Hue Var (X: Min, Y: Max)", Vector) = (0, 0, 0, 0)
        _GlobalSatVar ("Global Sat Var (X: Min, Y: Max)", Vector) = (0, 0, 0, 0)
        _GlobalLumVar ("Global Lum Var (X: Min, Y: Max)", Vector) = (0, 0, 0, 0)

        _LocalNoiseScale ("Local Noise Scale", Float) = 1.0
        [Enum(Blocky, 0, Smooth, 1, Wavy, 2)] _LocalNoiseType ("Local Noise Type", Int) = 1
        _LocalHueVar ("Local Hue Var (X: Min, Y: Max)", Vector) = (0, 0, 0, 0)
        _LocalSatVar ("Local Sat Var (X: Min, Y: Max)", Vector) = (0, 0, 0, 0)
        _LocalLumVar ("Local Lum Var (X: Min, Y: Max)", Vector) = (0, 0, 0, 0)
        
        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0
    }
    
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
        Cull Off 

        // ----------------------------------------------------------------------
        // PASS 1: G-BUFFER
        // ----------------------------------------------------------------------
        Pass
        {
            Name "GBuffer"
            Tags { "LightMode" = "UniversalGBuffer" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            
            #pragma shader_feature_local _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local _ENVIRONMENTREFLECTIONS_OFF

            #include "LoogaFoliageCore.hlsl"

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                
                // Foliage flutter mask is 1.0 (leaves jitter rapidly)
                input.positionOS.xyz = ApplyProceduralWind(input.positionOS.xyz, positionWS, 1.0, _WindInfluence);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                
                // Dummy assignment since Varyings requires it for the grass shader
                output.windGust = 0.0;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.uv = input.uv;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                return output;
            }

            struct FragmentOutput { half4 GBuffer0 : SV_Target0; half4 GBuffer1 : SV_Target1; half4 GBuffer2 : SV_Target2; half4 GBuffer3 : SV_Target3; };

            FragmentOutput Frag(Varyings input, bool isFrontFace : SV_IsFrontFace)
            {
                FragmentOutput outGBuffer;
                
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(albedo.a - _Cutoff); 
                
                // Apply HSV noise variation from the shared core
                half3 finalAlbedo = GetVariedColor(albedo.rgb, input.positionWS);

                half4 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv);
                half3 normalTS = UnpackNormal(normalSample);
                
                half sign = input.tangentWS.w * GetOddNegativeScale();
                half3 bitangentWS = cross(input.normalWS, input.tangentWS.xyz) * sign;
                half3 normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS));
                normalWS = NormalizeNormalPerPixel(normalWS);
                normalWS = isFrontFace ? normalWS : -normalWS;

                outGBuffer.GBuffer0 = half4(finalAlbedo, 33.0 / 255.0); 
                outGBuffer.GBuffer1 = half4(0.0, 0.0, 0.0, 1.0); 
                outGBuffer.GBuffer2 = half4(normalWS, _Smoothness);
                outGBuffer.GBuffer3 = half4(0, 0, 0, 1); 
                
                return outGBuffer;
            }
            ENDHLSL
        }
        
        // ----------------------------------------------------------------------
        // PASS 2: SSSS PROFILE
        // ----------------------------------------------------------------------
        Pass
        {
            Name "SSSSProfile"
            Tags { "LightMode" = "SSSSProfile" }

            ZWrite Off
            ZTest LEqual 
            Cull Off

            HLSLPROGRAM
            #pragma vertex VertProfile
            #pragma fragment FragProfile
            
            #include "LoogaFoliageCore.hlsl"

            VaryingsProfile VertProfile(AttributesProfile input)
            {
                VaryingsProfile output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                input.positionOS.xyz = ApplyProceduralWind(input.positionOS.xyz, positionWS, 1.0, _WindInfluence);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 FragProfile(VaryingsProfile input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;
                clip(alpha - _Cutoff); 
                
                half3 finalSSSS = GetVariedColor(_SubsurfaceColor.rgb, input.positionWS);
                
                return half4(finalSSSS, _ScatterWidth / 5.0);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
    }
    
    CustomEditor "LoogaSoft.Lighting.Editor.LoogaFoliageShaderGUI"
    Fallback "Universal Render Pipeline/Lit"
}