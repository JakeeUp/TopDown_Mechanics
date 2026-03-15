Shader "Custom/FogOfWar"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "FogOfWarPass"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // Set by FogOfWarPass.cs
            float4x4 _FogOfWar_InvVPMatrix;

            // Set by FlashlightController.cs
            float3 _FlashlightPos;
            float3 _FlashlightDir;
            float4 _FlashlightParams; // x: cos(halfAngle), y: range, z: ambientRadius, w: edgeSoftness
            float _FogDensity;
            float _AmbientIntensity;

            float3 ReconstructWorldPos(float2 uv)
            {
                float depth = SampleSceneDepth(uv);

                // Reconstruct clip-space position
                // No y-flip needed here — GL.GetGPUProjectionMatrix(proj, true) already
                // bakes in the platform y-flip for render textures.
                float4 clipPos = float4(uv * 2.0 - 1.0, depth, 1.0);

                // Transform to world space
                float4 worldPos = mul(_FogOfWar_InvVPMatrix, clipPos);
                return worldPos.xyz / worldPos.w;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // DEBUG: Solid bright green. If game view turns green, the pass runs.
                return half4(0, 1, 0, 1);
            }
            ENDHLSL
        }
    }
}
