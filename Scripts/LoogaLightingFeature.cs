using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace LoogaSoft.Lighting
{
    [DisallowMultipleRendererFeature("Looga Lighting")]
    public class LoogaLightingFeature : ScriptableRendererFeature
    {
        public enum LightingModel
        {
            DisneyBurley,
            Source2,
            TF2,
            Minnaert,
            Overwatch,
            OrenNayar,
            Arkane
        }
        
        [Header("Base Lighting")]
        public LightingModel activeLightingModel = LightingModel.DisneyBurley;
        
        [Header("Subsurface Scattering")]
        public bool enableSSSS = true;

        private Material _customLightingMaterial;
        private Material _ssssMaterial;
        private CustomLightingPass _customLightingPass;
        
        public override void Create()
        {
            UpdateLightingMaterial();
        }

        private void UpdateLightingMaterial()
        {
            string shaderName = activeLightingModel switch
            {
                LightingModel.DisneyBurley => "Hidden/LoogaSoft/Lighting/DisneyBurley",
                LightingModel.Source2 => "Hidden/LoogaSoft/Lighting/Source2",
                LightingModel.TF2 => "Hidden/LoogaSoft/Lighting/TF2",
                LightingModel.Minnaert => "Hidden/LoogaSoft/Lighting/Minnaert",
                LightingModel.Overwatch => "Hidden/LoogaSoft/Lighting/Overwatch",
                LightingModel.OrenNayar => "Hidden/LoogaSoft/Lighting/OrenNayar",
                LightingModel.Arkane => "Hidden/LoogaSoft/Lighting/Arkane",
                _ => "Hidden/LoogaSoft/Lighting/DisneyBurley"
            };

            if (_customLightingMaterial == null || _customLightingMaterial.shader.name != shaderName)
            {
                if (_customLightingMaterial != null)
                    CoreUtils.Destroy(_customLightingMaterial);

                Shader shader = Shader.Find(shaderName);
                if (shader != null)
                {
                    _customLightingMaterial = CoreUtils.CreateEngineMaterial(shader);
                }
                else
                {
                    Debug.LogError($"[LoogaLighting] Could not find shader: {shaderName}");
                }
            }

            if (enableSSSS && (_ssssMaterial == null || _ssssMaterial.shader.name != "Hidden/LoogaSoft/SSSS"))
            {
                Shader ssssShader = Shader.Find("Hidden/LoogaSoft/SSSS");
                if (ssssShader != null)
                    _ssssMaterial = CoreUtils.CreateEngineMaterial(ssssShader);
            }

            if (_customLightingMaterial != null)
            {
                if (_customLightingPass == null)
                    _customLightingPass = new CustomLightingPass(this);
                else
                    _customLightingPass.UpdateMaterials(this);
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            UpdateLightingMaterial();

            if (_customLightingMaterial == null) return;
            
            if (renderingData.cameraData.cameraType == CameraType.Game || renderingData.cameraData.cameraType == CameraType.SceneView)
            {
                renderer.EnqueuePass(_customLightingPass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (_customLightingMaterial != null)
            {
                CoreUtils.Destroy(_customLightingMaterial);
                _customLightingMaterial = null;
            }
            if (_ssssMaterial != null)
            {
                CoreUtils.Destroy(_ssssMaterial);
                _ssssMaterial = null;
            }
            
            _customLightingPass = null;
            base.Dispose(disposing);
        }

        private class CustomLightingPass : ScriptableRenderPass
        {
            private LoogaLightingFeature _feature;

            private static readonly int[] ShaderGBufferIDs = {
                Shader.PropertyToID("_GBuffer0"),
                Shader.PropertyToID("_GBuffer1"),
                Shader.PropertyToID("_GBuffer2"),
                Shader.PropertyToID("_GBuffer3"),
            };
            
            private static readonly int CameraDepthTextureID = Shader.PropertyToID("_CameraDepthTexture");
            private static readonly int SSSSProfileTextureID = Shader.PropertyToID("_SSSSProfileTexture");
            private static readonly ShaderTagId SSSSProfileTagId = new ShaderTagId("SSSSProfile");

            public CustomLightingPass(LoogaLightingFeature feature)
            {
                _feature = feature;
                renderPassEvent = RenderPassEvent.BeforeRenderingDeferredLights;
            }

            public void UpdateMaterials(LoogaLightingFeature feature)
            {
                _feature = feature;
            }
            
            private class LightingPassData
            {
                public Material material;
                public TextureHandle[] gBuffers;
                public TextureHandle depthTexture;
                public TextureHandle ssssProfileTexture;
            }
            
            private class SSSSPassData
            {
                public TextureHandle source;
                public Material material;
                public int passIndex;
            }
            
            private class DrawProfileData
            {
                public RendererListHandle rendererList;
            }

            private class BlitPassData
            {
                public TextureHandle source;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
  
                if (_feature._customLightingMaterial == null) return;
                
                TextureHandle activeColor = resourceData.activeColorTexture;
                TextureHandle hardwareDepth = resourceData.activeDepthTexture;
                TextureHandle stencilTexture = resourceData.activeDepthTexture;
                
                RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                
                TextureHandle tempLightingTarget = renderGraph.CreateTexture(new TextureDesc(desc)
                {
                    name = "Looga Lighting Target",
                    enableRandomWrite = true,
                    clearBuffer = true,
                    clearColor = Color.clear
                });

                TextureHandle ssssProfileTarget = TextureHandle.nullHandle;

                // 1. SSSS Profile Draw Pass (Must happen BEFORE lighting evaluation)
                if (_feature.enableSSSS && _feature._ssssMaterial != null && hardwareDepth.IsValid())
                {
                    ssssProfileTarget = renderGraph.CreateTexture(new TextureDesc(desc)
                    {
                        name = "SSSS Profile Target",
                        colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm,
                        clearBuffer = true,
                        clearColor = Color.clear // Blank areas will have 0 width/color
                    });

                    using (var builder = renderGraph.AddRasterRenderPass<DrawProfileData>("Looga SSSS Profile Draw", out var passData))
                    {
                        builder.SetRenderAttachment(ssssProfileTarget, 0, AccessFlags.Write);
                        builder.SetRenderAttachmentDepth(hardwareDepth, AccessFlags.Read); // For ZTest LEqual execution

                        UniversalRenderingData urpRenderingData = frameData.Get<UniversalRenderingData>();
                        DrawingSettings drawingSettings = new DrawingSettings(SSSSProfileTagId, new SortingSettings(cameraData.camera));
                        FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

                        passData.rendererList = renderGraph.CreateRendererList(new RendererListParams(urpRenderingData.cullResults, drawingSettings, filteringSettings));
                        builder.UseRendererList(passData.rendererList);

                        builder.SetRenderFunc((DrawProfileData data, RasterGraphContext context) =>
                        {
                            context.cmd.DrawRendererList(data.rendererList);
                        });
                    }
                }

                // 2. Lighting Evaluation Pass (Now reads the SSSS Profile for transmission)
                using (var builder = renderGraph.AddRasterRenderPass<LightingPassData>("Looga Lighting Evaluation", out var passData))
                {
                    passData.material = _feature._customLightingMaterial;
                    passData.depthTexture = hardwareDepth;
                    passData.ssssProfileTexture = ssssProfileTarget;

                    TextureHandle[] currentGBuffers = resourceData.gBuffer;
                    if (currentGBuffers != null)
                    {
                        passData.gBuffers = new TextureHandle[Mathf.Min(currentGBuffers.Length, 4)];
                        for (int i = 0; i < passData.gBuffers.Length; i++)
                        {
                            if (currentGBuffers[i].IsValid())
                            {
                                passData.gBuffers[i] = currentGBuffers[i];
                                builder.UseTexture(passData.gBuffers[i], AccessFlags.Read);
                            }
                        }
                    }
                    
                    if (passData.depthTexture.IsValid())
                        builder.UseTexture(passData.depthTexture, AccessFlags.Read);

                    // Allow lighting pass to read the profile we just drew
                    if (passData.ssssProfileTexture.IsValid())
                        builder.UseTexture(passData.ssssProfileTexture, AccessFlags.Read);

                    builder.SetRenderAttachment(tempLightingTarget, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    
                    builder.SetRenderFunc((LightingPassData data, RasterGraphContext context) =>
                    {
                        RasterCommandBuffer cmd = context.cmd;

                        if (data.gBuffers != null)
                        {
                            for (int i = 0; i < data.gBuffers.Length; i++)
                            {
                                if (data.gBuffers[i].IsValid())
                                    cmd.SetGlobalTexture(ShaderGBufferIDs[i], data.gBuffers[i]);
                            }
                        }
                        
                        if (data.depthTexture.IsValid())
                            cmd.SetGlobalTexture(CameraDepthTextureID, data.depthTexture);

                        // Bind the profile so the deferred lighting fragment shader can sample it
                        if (data.ssssProfileTexture.IsValid())
                            cmd.SetGlobalTexture(SSSSProfileTextureID, data.ssssProfileTexture);

                        Blitter.BlitTexture(cmd, new Vector4(1,1,0,0), data.material, 0);
                    });
                }

                // 3. Subsurface Scattering Blurs (Ping-Pong)
                if (ssssProfileTarget.IsValid())
                {
                    TextureHandle ssssPingPong = renderGraph.CreateTexture(new TextureDesc(desc) { name = "SSSS PingPong Target" });

                    // Horizontal Blur (Temp Target -> Ping Pong)
                    using (var builder = renderGraph.AddRasterRenderPass<SSSSPassData>("Looga SSSS Horizontal", out var passData))
                    {
                        passData.source = tempLightingTarget;
                        passData.material = _feature._ssssMaterial;
                        passData.passIndex = 0;

                        builder.UseTexture(passData.source, AccessFlags.Read);
                        builder.SetRenderAttachment(ssssPingPong, 0, AccessFlags.Write);
                        builder.SetRenderAttachmentDepth(hardwareDepth, AccessFlags.Read);
                        builder.UseTexture(ssssProfileTarget, AccessFlags.Read);
                        
                        builder.AllowGlobalStateModification(true);

                        builder.SetRenderFunc((SSSSPassData data, RasterGraphContext context) =>
                        {
                            context.cmd.SetGlobalTexture(SSSSProfileTextureID, ssssProfileTarget);
                            Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, data.passIndex);
                        });
                    }

                    // Vertical Blur (Ping Pong -> Temp Target)
                    using (var builder = renderGraph.AddRasterRenderPass<SSSSPassData>("Looga SSSS Vertical", out var passData))
                    {
                        passData.source = ssssPingPong;
                        passData.material = _feature._ssssMaterial;
                        passData.passIndex = 1;

                        builder.UseTexture(passData.source, AccessFlags.Read);
                        builder.SetRenderAttachment(tempLightingTarget, 0, AccessFlags.Write);
                        builder.SetRenderAttachmentDepth(hardwareDepth, AccessFlags.Read);
                        builder.UseTexture(ssssProfileTarget, AccessFlags.Read);
                        
                        builder.AllowGlobalStateModification(true);

                        builder.SetRenderFunc((SSSSPassData data, RasterGraphContext context) =>
                        {
                            context.cmd.SetGlobalTexture(SSSSProfileTextureID, ssssProfileTarget);
                            Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, data.passIndex);
                        });
                    }
                }

                // 4. Final Blit and Stencil Clear
                using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>("Looga Lighting Blit", out var passData))
                {
                    passData.source = tempLightingTarget;
                    
                    builder.UseTexture(passData.source, AccessFlags.Read);
                    builder.SetRenderAttachment(activeColor, 0, AccessFlags.Write);
                    
                    if (stencilTexture.IsValid())
                        builder.SetRenderAttachmentDepth(stencilTexture, AccessFlags.Write);
                    
                    builder.SetRenderFunc((BlitPassData data, RasterGraphContext context) =>
                    {
                        RasterCommandBuffer cmd = context.cmd;
                        Blitter.BlitTexture(cmd, data.source, new Vector4(1,1,0,0), 0.0f, false);
                        
                        // Clear the stencil so it doesn't interfere with URP's transparent/post-processing passes
                        cmd.ClearRenderTarget(RTClearFlags.Stencil, Color.clear, 1.0f, 0);
                    });
                }
            }
        }
    }
}