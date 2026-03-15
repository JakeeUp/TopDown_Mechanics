using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FogOfWarFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material fogMaterial;
        public RenderPassEvent renderEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public Settings settings = new Settings();
    private FogOfWarPass fogPass;

    public override void Create()
    {
        fogPass = new FogOfWarPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.fogMaterial == null) return;
        renderer.EnqueuePass(fogPass);
    }

    // -------------------------------------------------------------------------
    // Render Pass
    // -------------------------------------------------------------------------

    private class FogOfWarPass : ScriptableRenderPass
    {
        private readonly Settings settings;
        private RTHandle tempRT;

        public FogOfWarPass(Settings settings)
        {
            this.settings = settings;
            renderPassEvent = settings.renderEvent;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref tempRT, descriptor, name: "_FogOfWarTemp");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (settings.fogMaterial == null) return;

            CommandBuffer cmd = CommandBufferPool.Get("FogOfWar");

            Camera cam = renderingData.cameraData.camera;
            Matrix4x4 vp = cam.projectionMatrix * cam.worldToCameraMatrix;
            cmd.SetGlobalMatrix("_InvViewProjMatrix", vp.inverse);

            var source = renderingData.cameraData.renderer.cameraColorTargetHandle;

            // Blit through temp RT to avoid same-source issues
            Blit(cmd, source, tempRT, settings.fogMaterial);
            Blit(cmd, tempRT, source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // RTHandle cleanup handled by ReAllocateIfNeeded
        }

        public void Dispose()
        {
            tempRT?.Release();
        }
    }

    protected override void Dispose(bool disposing)
    {
        fogPass?.Dispose();
    }
}
