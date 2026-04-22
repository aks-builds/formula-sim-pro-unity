using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FormulaSim.FX
{
    // URP Renderer Feature: injects the rain overlay as a full-screen blit
    // after the main camera renders the scene.
    // Add via Project Settings → URP Renderer → Add Renderer Feature → RainRendererFeature.

    public class RainRendererFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public Material         rainMaterial;
            public RenderPassEvent  passEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public Settings settings = new();

        RainRenderPass pass;

        public override void Create()
        {
            pass = new RainRenderPass(settings.rainMaterial, settings.passEvent);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.rainMaterial == null) return;
            pass.Setup(renderer.cameraColorTargetHandle);
            renderer.EnqueuePass(pass);
        }

        class RainRenderPass : ScriptableRenderPass
        {
            Material         mat;
            RTHandle         src;
            RTHandle         temp;
            static readonly int TempRT = Shader.PropertyToID("_RainTemp");

            public RainRenderPass(Material m, RenderPassEvent ev) { mat = m; renderPassEvent = ev; }

            public void Setup(RTHandle source) => src = source;

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData _)
            {
                RenderTextureDescriptor desc = _.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                RenderingUtils.ReAllocateIfNeeded(ref temp, desc, name: "_RainTemp");
            }

            public override void Execute(ScriptableRenderContext ctx, ref RenderingData _)
            {
                if (mat == null) return;
                CommandBuffer cmd = CommandBufferPool.Get("RainOverlay");
                Blitter.BlitCameraTexture(cmd, src, temp, mat, 0);
                Blitter.BlitCameraTexture(cmd, temp, src);
                ctx.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                temp?.Release();
            }
        }
    }
}
