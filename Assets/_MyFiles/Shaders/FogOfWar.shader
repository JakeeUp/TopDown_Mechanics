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

            // Volumetric ray march params
            float _VFogSteps;
            float _VFogDensityScale;
            float _VFogScatterIntensity;
            float _VFogBaseY;
            float _VFogHeight;
            float _VFogNoiseScale;
            float _VFogWindSpeed;
            float _VFogLightAbsorption;
            float _VFogPhaseG;
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
            // Fog density at a world position
            // ---------------------------------------------------------------

            float sampleFogDensity(float3 pos)
            {
                // Height falloff: densest at base, fades upward
                float heightFactor = 1.0 - saturate((pos.y - _VFogBaseY) / max(_VFogHeight, 0.01));
                heightFactor = heightFactor * heightFactor;

                // Animated 3D noise for rolling, wispy fog
                float3 noisePos = pos * _VFogNoiseScale
                    + float3(_Time.y * _VFogWindSpeed, 0, _Time.y * _VFogWindSpeed * 0.7);
                float noise = fbm3D(noisePos);

                return max(noise * heightFactor * _VFogDensityScale, 0.0);
            }

            // ---------------------------------------------------------------
            // Henyey-Greenstein phase function (light scattering direction)
            // ---------------------------------------------------------------

            float henyeyGreenstein(float cosTheta, float g)
            {
                float g2 = g * g;
                float denom = 1.0 + g2 - 2.0 * g * cosTheta;
                return (1.0 - g2) / (4.0 * PI * pow(max(denom, 0.0001), 1.5));
            }

            // ---------------------------------------------------------------
            // Flashlight illumination at a point in the volume
            // ---------------------------------------------------------------

            float flashlightIllumination(float3 pos)
            {
                float3 toPos = pos - _FlashlightPos;
                float dist = length(toPos);
                float3 toPosDir = toPos / max(dist, 0.001);

                float cosAngle = dot(toPosDir, _FlashlightDir);
                float innerCos = _FlashlightParams.x;
                float outerCos = innerCos - _FlashlightParams.w;
                float angleFactor = smoothstep(outerCos, innerCos, cosAngle);

                float distFactor = 1.0 - saturate(dist / _FlashlightParams.y);
                distFactor *= distFactor;

                return angleFactor * distFactor;
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
            // Fragment: ray march through volumetric fog
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

                // Don't march further than geometry or max distance
                float marchDist = min(totalDist, _VFogMaxMarchDist);

                int steps = (int)_VFogSteps;
                float stepSize = marchDist / (float)steps;

                // Jitter ray start to reduce banding artifacts
                float jitter = frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
                float3 pos = camPos + rayDir * (stepSize * jitter);

                // Accumulate along the ray
                float transmittance = 1.0;
                float3 scatteredLight = float3(0, 0, 0);

                for (int i = 0; i < steps; i++)
                {
                    float density = sampleFogDensity(pos);

                    if (density > 0.001)
                    {
                        // Beer-Lambert extinction
                        float extinction = density * _VFogLightAbsorption * stepSize;
                        float stepTransmittance = exp(-extinction);

                        // Flashlight in-scattering at this sample
                        float lightAmount = flashlightIllumination(pos);

                        // Per-sample phase: use direction from light to this sample vs view ray
                        float3 lightToSample = normalize(pos - _FlashlightPos);
                        float cosTheta = dot(rayDir, lightToSample);
                        float phase = henyeyGreenstein(cosTheta, _VFogPhaseG);
                        // Clamp phase to prevent blowout when camera is inside the cone
                        phase = min(phase, 4.0);

                        float3 inScatter = lightAmount * phase * _VFogScatterIntensity * density;

                        // Warm flashlight tint
                        inScatter *= float3(1.0, 0.9, 0.75);

                        // Small ambient glow near the player
                        float ambientDist = length(pos - _FlashlightPos);
                        float ambient = saturate(1.0 - ambientDist / _FlashlightParams.z)
                                       * _AmbientIntensity * 0.3;
                        inScatter += float3(0.02, 0.02, 0.04) * ambient * density;

                        // Energy-conserving integration
                        scatteredLight += transmittance * (1.0 - stepTransmittance)
                            * inScatter / max(density * _VFogLightAbsorption, 0.001);

                        transmittance *= stepTransmittance;
                    }

                    pos += rayDir * stepSize;

                    if (transmittance < 0.01)
                        break;
                }

                // Final composite
                float fogOpacity = (1.0 - transmittance) * _FogDensity;
                half3 fogColor = half3(0.01, 0.01, 0.02);
                half3 finalColor = lerp(sceneColor.rgb, fogColor, fogOpacity) + scatteredLight;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}
