using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace LoogaSoft.Lighting
{
    public class LoogaSSSSPass : ScriptableRenderPass
    {
        private Material _material;
        private float _scatterWidth;
        private Color _subsurfaceColor;

        private static readonly int ScatterWidthID = Shader.PropertyToID("_ScatterWidth");
        private static readonly int SubsurfaceColorID = Shader.PropertyToID("_SubsurfaceColor");

        public LoogaSSSSPass()
        {
            // Execute immediately after deferred lighting is applied, but before transparents
            renderPassEvent = RenderPassEvent.AfterRenderingDeferredLights;
        }

        public void Setup(Material material, float scatterWidth, Color subsurfaceColor)
        {
            _material = material;
            _scatterWidth = scatterWidth;
            _subsurfaceColor = subsurfaceColor;
        }

        private class PassData
        {
            public Material material;
            public TextureHandle source;
            public int passIndex;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            TextureHandle activeColor = resourceData.activeColorTexture;
            TextureHandle activeDepth = resourceData.activeDepthTexture;

            if (!activeColor.IsValid() || !activeDepth.IsValid()) return;

            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0; // Color target only
            
            TextureHandle pingPong = renderGraph.CreateTexture(new TextureDesc(desc) { name = "SSSS PingPong" });

            // Pass 1: Horizontal Blur (Active Color -> Ping Pong)
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("SSSS Horizontal Blur", out var passData))
            {
                passData.material = _material;
                passData.source = activeColor;
                passData.passIndex = 0; // Horizontal pass in the shader

                builder.UseTexture(activeColor, AccessFlags.Read);
                builder.UseTexture(activeDepth, AccessFlags.Read); // For depth-aware sampling
                
                builder.SetRenderAttachment(pingPong, 0, AccessFlags.Write);
                
                // Bind the main depth buffer as Read-Only to enable hardware Stencil Testing
                builder.SetRenderAttachmentDepth(activeDepth, AccessFlags.Read);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    data.material.SetFloat(ScatterWidthID, _scatterWidth);
                    data.material.SetColor(SubsurfaceColorID, _subsurfaceColor);
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, data.passIndex);
                });
            }

            // Pass 2: Vertical Blur (Ping Pong -> Active Color)
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("SSSS Vertical Blur", out var passData))
            {
                passData.material = _material;
                passData.source = pingPong;
                passData.passIndex = 1; // Vertical pass in the shader

                builder.UseTexture(pingPong, AccessFlags.Read);
                builder.UseTexture(activeDepth, AccessFlags.Read); 
                
                builder.SetRenderAttachment(activeColor, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(activeDepth, AccessFlags.Read);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, data.passIndex);
                });
            }
        }
    }
}