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
            float _FogDensity;
            float _AmbientIntensity;

            // Volumetric ray march params (set by VolumetricFogController.cs)
            float _VFogSteps;
            float _VFogDensityScale;
            float _VFogBaseY;
            float _VFogHeight;
            float _VFogNoiseScale;
            float _VFogWindSpeed;
            float _VFogLightAbsorption;
            float _VFogMaxMarchDist;

            // ---------------------------------------------------------------
            // 3D Noise
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

                float x1 = lerp(a, b, f.x);
                float x2 = lerp(c, d, f.x);
                float x3 = lerp(e, g, f.x);
                float x4 = lerp(h, k, f.x);

                float y1 = lerp(x1, x2, f.y);
                float y2 = lerp(x3, x4, f.y);

                return lerp(y1, y2, f.z);
            }

            float fbm3D(float3 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * valueNoise3D(p * frequency);
                    frequency *= 2.0;
                    amplitude *= 0.5;
                }
                return value;
            }

            // ---------------------------------------------------------------
            // Base fog density at a world position (before flashlight)
            // ---------------------------------------------------------------

            float sampleFogDensity(float3 pos)
            {
                // Gentle height falloff — fog is present at all heights, just slightly
                // thinner up high. Linear falloff instead of quadratic keeps fog visible
                // at eye level for FPS.
                float heightFactor = saturate(1.0 - (pos.y - _VFogBaseY) / max(_VFogHeight, 0.01));
                // Slight bias so fog is still ~40% dense even at the top
                heightFactor = lerp(0.4, 1.0, heightFactor);

                float3 noisePos = pos * _VFogNoiseScale
                    + float3(_Time.y * _VFogWindSpeed, 0, _Time.y * _VFogWindSpeed * 0.7);
                float noise = fbm3D(noisePos);

                return max(noise * heightFactor * _VFogDensityScale, 0.0);
            }

            // ---------------------------------------------------------------
            // How much the flashlight clears fog at a point (0 = no clearing, 1 = fully clear)
            // ---------------------------------------------------------------

            float flashlightClearance(float3 pos)
            {
                // Project to XZ plane for cone check — this makes the clearance
                // work as a vertical column above the cone footprint, so the
                // top-down camera can see through the fog to the cleared ground.
                float2 toPosXZ = pos.xz - _FlashlightPos.xz;
                float2 dirXZ = _FlashlightDir.xz;
                float dirLenXZ = length(dirXZ);

                float distXZ = length(toPosXZ);
                float2 toPosNormXZ = toPosXZ / max(distXZ, 0.001);
                float2 dirNormXZ = dirXZ / max(dirLenXZ, 0.001);

                // Cone angle check on XZ plane
                float cosAngle = dot(toPosNormXZ, dirNormXZ);
                float innerCos = _FlashlightParams.x;
                float outerCos = innerCos - _FlashlightParams.w;
                float angleFactor = smoothstep(outerCos, innerCos, cosAngle);

                // Distance falloff on XZ plane
                float distFactor = 1.0 - saturate(distXZ / _FlashlightParams.y);
                distFactor *= distFactor;

                float coneClear = angleFactor * distFactor;

                // Also do a 3D cone check for FPS (looking up/down matters)
                float3 toPos3D = pos - _FlashlightPos;
                float dist3D = length(toPos3D);
                float3 toPos3DDir = toPos3D / max(dist3D, 0.001);
                float cosAngle3D = dot(toPos3DDir, _FlashlightDir);
                float angleFactor3D = smoothstep(outerCos, innerCos, cosAngle3D);
                float distFactor3D = 1.0 - saturate(dist3D / _FlashlightParams.y);
                distFactor3D *= distFactor3D;
                float cone3DClear = angleFactor3D * distFactor3D;

                // Use whichever clearance is stronger (XZ for top-down, 3D for FPS)
                float flashClear = max(coneClear, cone3DClear);

                // Ambient clearance around player (can see your feet)
                float ambientDist = length(pos.xz - _FlashlightPos.xz);
                float ambientClear = saturate(1.0 - ambientDist / _FlashlightParams.z);
                ambientClear *= ambientClear;
                ambientClear *= _AmbientIntensity;

                return saturate(max(flashClear, ambientClear));
            }

            // ---------------------------------------------------------------
            // World position reconstruction from depth
            // ---------------------------------------------------------------

            float3 ReconstructWorldPos(float2 uv)
            {
                float depth = SampleSceneDepth(uv);
                float4 clipPos = float4(uv * 2.0 - 1.0, depth, 1.0);
                float4 worldPos = mul(_FogOfWar_InvVPMatrix, clipPos);
                return worldPos.xyz / worldPos.w;
            }

            // ---------------------------------------------------------------
            // Fragment: ray march — flashlight clears fog in its cone
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

                // Jitter to reduce banding
                float jitter = frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
                float3 pos = camPos + rayDir * (stepSize * jitter);

                // Dark blue-black fog color
                half3 fogColor = half3(0.01, 0.01, 0.02);

                float transmittance = 1.0;
                float3 fogAccum = float3(0, 0, 0);

                for (int i = 0; i < steps; i++)
                {
                    float baseDensity = sampleFogDensity(pos);

                    if (baseDensity > 0.001)
                    {
                        // Flashlight clears fog in its cone
                        float clearance = flashlightClearance(pos);
                        float density = baseDensity * (1.0 - clearance);

                        if (density > 0.001)
                        {
                            float extinction = density * _VFogLightAbsorption * stepSize;
                            float stepTransmittance = exp(-extinction);

                            // Fog contributes its own dark color
                            float3 contribution = transmittance * (1.0 - stepTransmittance) * fogColor;
                            fogAccum += contribution;

                            transmittance *= stepTransmittance;
                        }

                        // Subtle edge glow where fog meets the cleared area
                        // (light catching the fog boundary)
                        if (clearance > 0.05 && clearance < 0.95)
                        {
                            float edgeGlow = smoothstep(0.0, 0.5, clearance) * smoothstep(1.0, 0.5, clearance);
                            float3 glow = float3(0.8, 0.7, 0.5) * edgeGlow * baseDensity * 0.15 * transmittance * stepSize;
                            fogAccum += glow;
                        }
                    }

                    pos += rayDir * stepSize;

                    if (transmittance < 0.01)
                        break;
                }

                // Scene visible through remaining transmittance, fog fills the rest
                half3 finalColor = sceneColor.rgb * transmittance + fogAccum;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}
