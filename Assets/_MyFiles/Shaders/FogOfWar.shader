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
                float heightFactor = 1.0 - saturate((pos.y - _VFogBaseY) / max(_VFogHeight, 0.01));
                heightFactor = heightFactor * heightFactor;

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
                float3 toPos = pos - _FlashlightPos;
                float dist = length(toPos);
                float3 toPosDir = toPos / max(dist, 0.001);

                // Cone check
                float cosAngle = dot(toPosDir, _FlashlightDir);
                float innerCos = _FlashlightParams.x;
                float outerCos = innerCos - _FlashlightParams.w;
                float angleFactor = smoothstep(outerCos, innerCos, cosAngle);

                // Distance falloff
                float distFactor = 1.0 - saturate(dist / _FlashlightParams.y);
                distFactor *= distFactor;

                // Ambient clearance around player (can see your feet)
                float ambientDist = length(pos - _FlashlightPos);
                float ambientClear = saturate(1.0 - ambientDist / _FlashlightParams.z);
                ambientClear *= ambientClear;
                ambientClear *= _AmbientIntensity;

                return saturate(max(angleFactor * distFactor, ambientClear));
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
