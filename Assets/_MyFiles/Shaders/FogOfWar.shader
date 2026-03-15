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
                float4 clipPos = float4(uv * 2.0 - 1.0, depth, 1.0);

                #if UNITY_UV_STARTS_AT_TOP
                    clipPos.y = -clipPos.y;
                #endif

                // Transform to world space
                float4 worldPos = mul(_FogOfWar_InvVPMatrix, clipPos);
                return worldPos.xyz / worldPos.w;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 sceneColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

                float3 worldPos = ReconstructWorldPos(uv);

                // --- Flashlight cone visibility ---
                float3 toPixel = worldPos - _FlashlightPos;
                float dist = length(toPixel);
                float3 toPixelDir = toPixel / max(dist, 0.001);

                float cosAngle = dot(toPixelDir, _FlashlightDir);
                float innerCos = _FlashlightParams.x;
                float outerCos = innerCos - _FlashlightParams.w;
                float angleFactor = smoothstep(outerCos, innerCos, cosAngle);

                // Quadratic distance falloff
                float distFactor = 1.0 - saturate(dist / _FlashlightParams.y);
                distFactor *= distFactor;

                float coneVisibility = angleFactor * distFactor;

                // --- Ambient radius around player ---
                float ambientDist = dist;
                float ambientFactor = 1.0 - saturate(ambientDist / _FlashlightParams.z);
                ambientFactor *= ambientFactor;
                float ambientVisibility = ambientFactor * _AmbientIntensity;

                // --- Composite ---
                float visibility = max(coneVisibility, ambientVisibility);
                float darkness = 1.0 - visibility;

                // Blue-black fog for horror atmosphere
                half3 fogColor = half3(0.01, 0.01, 0.02);
                half3 finalColor = lerp(sceneColor.rgb, fogColor, darkness * _FogDensity);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}
