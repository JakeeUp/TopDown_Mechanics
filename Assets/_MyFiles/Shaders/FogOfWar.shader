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

            // Procedural noise for organic fog edges
            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f); // smoothstep interpolation

                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * valueNoise(p);
                    p *= 2.0;
                    amplitude *= 0.5;
                }
                return value;
            }

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
                float ambientFactor = 1.0 - saturate(dist / _FlashlightParams.z);
                ambientFactor *= ambientFactor;
                float ambientVisibility = ambientFactor * _AmbientIntensity;

                // --- Composite ---
                float visibility = max(coneVisibility, ambientVisibility);

                // --- Noise for organic fog edges ---
                float2 noiseCoord = worldPos.xz * 0.8 + _Time.y * 0.3;
                float noise = fbm(noiseCoord);
                // Push noise into the edge region — thickens/thins the fog boundary
                visibility = saturate(visibility + (noise - 0.5) * 0.3 * saturate(visibility * 3.0));

                float darkness = 1.0 - visibility;

                // Blue-black fog for horror atmosphere
                half3 fogColor = half3(0.01, 0.01, 0.02);
                // Slight color variation in the fog itself
                fogColor += half3(0.005, 0.0, 0.01) * noise;
                half3 finalColor = lerp(sceneColor.rgb, fogColor, darkness * _FogDensity);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}
