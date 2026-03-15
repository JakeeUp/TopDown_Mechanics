using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

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
        fogPass = new FogOfWarPass();
        fogPass.renderPassEvent = settings.renderEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.fogMaterial == null) return;
        fogPass.Setup(settings.fogMaterial);
        renderer.EnqueuePass(fogPass);
    }

    // -------------------------------------------------------------------------
    // Render Pass
    // -------------------------------------------------------------------------

    private class FogOfWarPass : ScriptableRenderPass
    {
        const string PassName = "FogOfWar";
        private Material fogMaterial;

        private static readonly int InvVPMatrixID = Shader.PropertyToID("_FogOfWar_InvVPMatrix");

        public void Setup(Material mat)
        {
            fogMaterial = mat;
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();

            if (resourceData.isActiveTargetBackBuffer)
                return;

            // Set the inverse VP matrix for world position reconstruction
            var cameraData = frameData.Get<UniversalCameraData>();
            Camera cam = cameraData.camera;
            Matrix4x4 vp = cam.projectionMatrix * cam.worldToCameraMatrix;
            Shader.SetGlobalMatrix(InvVPMatrixID, vp.inverse);

            var source = resourceData.activeColorTexture;

            var destinationDesc = renderGraph.GetTextureDesc(source);
            destinationDesc.name = $"CameraColor-{PassName}";
            destinationDesc.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

            RenderGraphUtils.BlitMaterialParameters blitParams =
                new(source, destination, fogMaterial, 0);
            renderGraph.AddBlitPass(blitParams, passName: PassName);

            resourceData.cameraColor = destination;
        }
    }
}
