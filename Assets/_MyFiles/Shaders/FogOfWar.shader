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

            // Set by FlashlightController.cs
            float4x4 _FogOfWar_InvVPMatrix;
            float3 _FlashlightPos;
            float3 _FlashlightDir;
            float4 _FlashlightParams; // x: cos(halfAngle), y: range, z: ambientRadius, w: edgeSoftness
            float _AmbientIntensity;

            // Set by VolumetricFogController.cs
            float _VFogSteps;
            float _VFogDensityScale;
            float _VFogBaseY;
            float _VFogHeight;
            float _VFogNoiseScale;
            float _VFogWindSpeed;
            float _VFogAbsorption;
            float _VFogMaxMarchDist;

            // ---------------------------------------------------------------
            // 3D Noise — 3 octaves, large scale for smooth rolling fog
            // ---------------------------------------------------------------

            float hash3D(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float valueNoise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash3D(i);
                float b = hash3D(i + float3(1, 0, 0));
                float c = hash3D(i + float3(0, 1, 0));
                float d = hash3D(i + float3(1, 1, 0));
                float e = hash3D(i + float3(0, 0, 1));
                float g = hash3D(i + float3(1, 0, 1));
                float h = hash3D(i + float3(0, 1, 1));
                float k = hash3D(i + float3(1, 1, 1));

                return lerp(
                    lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y),
                    lerp(lerp(e, g, f.x), lerp(h, k, f.x), f.y),
                    f.z);
            }

            float fbm3D(float3 p)
            {
                float value = 0.0;
                float amplitude = 0.6;
                for (int i = 0; i < 3; i++)
                {
                    value += amplitude * valueNoise3D(p);
                    p *= 2.0;
                    amplitude *= 0.4;
                }
                return value;
            }

            // ---------------------------------------------------------------
            // Fog density — distance-based, thickens far from player
            // ---------------------------------------------------------------

            float sampleFogDensity(float3 pos)
            {
                // Height: ground fog, fades out above fogHeight
                float heightFactor = saturate(1.0 - (pos.y - _VFogBaseY) / max(_VFogHeight, 0.01));

                // Smooth, large-scale animated noise
                float3 noisePos = pos * _VFogNoiseScale
                    + float3(_Time.y * _VFogWindSpeed, _Time.y * _VFogWindSpeed * 0.3, _Time.y * _VFogWindSpeed * 0.7);
                float noise = fbm3D(noisePos);

                // Remap noise: push low values to zero for patches of clear air
                noise = saturate(noise * 1.4 - 0.15);

                return noise * heightFactor * _VFogDensityScale;
            }

            // ---------------------------------------------------------------
            // Flashlight fog clearance (0 = fog stays, 1 = fully clear)
            // ---------------------------------------------------------------

            float flashlightClearance(float3 pos)
            {
                float innerCos = _FlashlightParams.x;
                float softness = _FlashlightParams.w;
                float outerCos = innerCos - softness;
                float range = _FlashlightParams.y;

                // --- XZ cone (for top-down camera to see through) ---
                float2 toPosXZ = pos.xz - _FlashlightPos.xz;
                float distXZ = length(toPosXZ);
                float2 dirXZ = normalize(_FlashlightDir.xz);
                float cosXZ = dot(toPosXZ / max(distXZ, 0.001), dirXZ);
                float angleXZ = smoothstep(outerCos, innerCos, cosXZ);
                float distXZFade = saturate(1.0 - distXZ / range);
                float clearXZ = angleXZ * distXZFade;

                // --- 3D cone (for FPS looking up/down) ---
                float3 toPos3D = pos - _FlashlightPos;
                float dist3D = length(toPos3D);
                float cos3D = dot(toPos3D / max(dist3D, 0.001), _FlashlightDir);
                float angle3D = smoothstep(outerCos, innerCos, cos3D);
                float dist3DFade = saturate(1.0 - dist3D / range);
                float clear3D = angle3D * dist3DFade;

                float flashClear = max(clearXZ, clear3D);

                // Ambient clearance around player
                float ambDist = length(pos.xz - _FlashlightPos.xz);
                float ambClear = saturate(1.0 - ambDist / _FlashlightParams.z);
                ambClear *= ambClear * _AmbientIntensity;

                return saturate(max(flashClear, ambClear));
            }

            // ---------------------------------------------------------------
            // World position from depth
            // ---------------------------------------------------------------

            float3 ReconstructWorldPos(float2 uv)
            {
                float depth = SampleSceneDepth(uv);
                float4 clipPos = float4(uv * 2.0 - 1.0, depth, 1.0);
                float4 worldPos = mul(_FogOfWar_InvVPMatrix, clipPos);
                return worldPos.xyz / worldPos.w;
            }

            // ---------------------------------------------------------------
            // Fragment
            // ---------------------------------------------------------------

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 sceneColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

                float3 worldPos = ReconstructWorldPos(uv);
                float3 camPos = _WorldSpaceCameraPos;

                float3 rayVec = worldPos - camPos;
                float totalDist = length(rayVec);
                float3 rayDir = rayVec / max(totalDist, 0.001);

                float marchDist = min(totalDist, _VFogMaxMarchDist);
                int steps = (int)_VFogSteps;
                float stepSize = marchDist / (float)steps;

                // Jitter start position to reduce banding
                float jitter = frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
                float3 pos = camPos + rayDir * (stepSize * jitter);

                // Silent Hill style: light grey fog
                half3 fogColor = half3(0.02, 0.025, 0.04);

                float transmittance = 1.0;
                float3 fogAccum = float3(0, 0, 0);

                for (int i = 0; i < steps; i++)
                {
                    float baseDensity = sampleFogDensity(pos);

                    if (baseDensity > 0.001)
                    {
                        // Flashlight carves through the fog
                        float clearance = flashlightClearance(pos);
                        float density = baseDensity * (1.0 - clearance);

                        if (density > 0.001)
                        {
                            float extinction = density * _VFogAbsorption * stepSize;
                            float stepT = exp(-extinction);

                            fogAccum += transmittance * (1.0 - stepT) * fogColor;
                            transmittance *= stepT;
                        }

                        // Subtle edge brightening at fog/clear boundary
                        if (clearance > 0.1 && clearance < 0.9)
                        {
                            float edge = smoothstep(0.0, 0.5, clearance) * smoothstep(1.0, 0.5, clearance);
                            fogAccum += float3(0.08, 0.09, 0.14) * edge * baseDensity * 0.06 * transmittance * stepSize;
                        }
                    }

                    pos += rayDir * stepSize;

                    if (transmittance < 0.01)
                        break;
                }

                half3 finalColor = sceneColor.rgb * transmittance + fogAccum;
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}
