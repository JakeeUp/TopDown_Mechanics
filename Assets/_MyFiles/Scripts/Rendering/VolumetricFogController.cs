using UnityEngine;

/// <summary>
/// Pushes volumetric fog parameters to global shader properties.
/// Independent of FlashlightController — fog stays active regardless of flashlight state.
/// Attach to any always-active GameObject (e.g. a manager or the Player root).
/// </summary>
public class VolumetricFogController : MonoBehaviour
{
    [Header("Fog Volume")]
    [SerializeField] private float fogDensity = 1.0f;
    [SerializeField] private float densityScale = 2.5f;
    [SerializeField] private float fogBaseY = -1f;
    [SerializeField] private float fogHeight = 15f;

    [Header("Noise & Animation")]
    [SerializeField] private float noiseScale = 0.15f;
    [SerializeField] private float windSpeed = 0.4f;

    [Header("Light Interaction")]
    [SerializeField] private float scatterIntensity = 5.0f;
    [SerializeField] private float lightAbsorption = 2.0f;
    [Range(-0.9f, 0.9f)]
    [SerializeField] private float phaseAsymmetry = 0.6f;

    [Header("Performance")]
    [SerializeField] private float volumetricSteps = 48f;
    [SerializeField] private float maxMarchDistance = 40f;

    // -------------------------------------------------------------------------
    // Shader Property IDs (cached)
    // -------------------------------------------------------------------------

    private static readonly int FogDensityID = Shader.PropertyToID("_FogDensity");
    private static readonly int VFogStepsID = Shader.PropertyToID("_VFogSteps");
    private static readonly int VFogDensityScaleID = Shader.PropertyToID("_VFogDensityScale");
    private static readonly int VFogScatterIntensityID = Shader.PropertyToID("_VFogScatterIntensity");
    private static readonly int VFogBaseYID = Shader.PropertyToID("_VFogBaseY");
    private static readonly int VFogHeightID = Shader.PropertyToID("_VFogHeight");
    private static readonly int VFogNoiseScaleID = Shader.PropertyToID("_VFogNoiseScale");
    private static readonly int VFogWindSpeedID = Shader.PropertyToID("_VFogWindSpeed");
    private static readonly int VFogLightAbsorptionID = Shader.PropertyToID("_VFogLightAbsorption");
    private static readonly int VFogPhaseGID = Shader.PropertyToID("_VFogPhaseG");
    private static readonly int VFogMaxMarchDistID = Shader.PropertyToID("_VFogMaxMarchDist");

    private void LateUpdate()
    {
        Shader.SetGlobalFloat(FogDensityID, fogDensity);
        Shader.SetGlobalFloat(VFogStepsID, volumetricSteps);
        Shader.SetGlobalFloat(VFogDensityScaleID, densityScale);
        Shader.SetGlobalFloat(VFogScatterIntensityID, scatterIntensity);
        Shader.SetGlobalFloat(VFogBaseYID, fogBaseY);
        Shader.SetGlobalFloat(VFogHeightID, fogHeight);
        Shader.SetGlobalFloat(VFogNoiseScaleID, noiseScale);
        Shader.SetGlobalFloat(VFogWindSpeedID, windSpeed);
        Shader.SetGlobalFloat(VFogLightAbsorptionID, lightAbsorption);
        Shader.SetGlobalFloat(VFogPhaseGID, phaseAsymmetry);
        Shader.SetGlobalFloat(VFogMaxMarchDistID, maxMarchDistance);
    }
}
