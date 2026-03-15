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
        if (settings.fogMaterial == null)
        {
            Debug.LogWarning("[FOG] Material is null, skipping pass");
            return;
        }
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

        public void Setup(Material mat)
        {
            fogMaterial = mat;
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();

            if (resourceData.isActiveTargetBackBuffer)
            {
                Debug.LogWarning("[FOG] Active target is back buffer, skipping");
                return;
            }

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
