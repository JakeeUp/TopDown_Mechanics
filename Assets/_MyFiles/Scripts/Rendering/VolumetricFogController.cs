using UnityEngine;

/// <summary>
/// Pushes volumetric fog parameters to global shader properties.
/// Independent of FlashlightController — fog stays active regardless of flashlight state.
/// </summary>
public class VolumetricFogController : MonoBehaviour
{
    [Header("Fog Volume")]
    [SerializeField] private float densityScale = 0.6f;
    [SerializeField] private float fogBaseY = -1f;
    [SerializeField] private float fogHeight = 8f;

    [Header("Noise & Animation")]
    [SerializeField] private float noiseScale = 0.06f;
    [SerializeField] private float windSpeed = 0.25f;

    [Header("Absorption")]
    [SerializeField] private float absorption = 0.8f;

    [Header("Performance")]
    [SerializeField] private float volumetricSteps = 32f;
    [SerializeField] private float maxMarchDistance = 50f;

    // -------------------------------------------------------------------------
    // Shader Property IDs
    // -------------------------------------------------------------------------

    private static readonly int VFogStepsID = Shader.PropertyToID("_VFogSteps");
    private static readonly int VFogDensityScaleID = Shader.PropertyToID("_VFogDensityScale");
    private static readonly int VFogBaseYID = Shader.PropertyToID("_VFogBaseY");
    private static readonly int VFogHeightID = Shader.PropertyToID("_VFogHeight");
    private static readonly int VFogNoiseScaleID = Shader.PropertyToID("_VFogNoiseScale");
    private static readonly int VFogWindSpeedID = Shader.PropertyToID("_VFogWindSpeed");
    private static readonly int VFogAbsorptionID = Shader.PropertyToID("_VFogAbsorption");
    private static readonly int VFogMaxMarchDistID = Shader.PropertyToID("_VFogMaxMarchDist");

    private void LateUpdate()
    {
        Shader.SetGlobalFloat(VFogStepsID, volumetricSteps);
        Shader.SetGlobalFloat(VFogDensityScaleID, densityScale);
        Shader.SetGlobalFloat(VFogBaseYID, fogBaseY);
        Shader.SetGlobalFloat(VFogHeightID, fogHeight);
        Shader.SetGlobalFloat(VFogNoiseScaleID, noiseScale);
        Shader.SetGlobalFloat(VFogWindSpeedID, windSpeed);
        Shader.SetGlobalFloat(VFogAbsorptionID, absorption);
        Shader.SetGlobalFloat(VFogMaxMarchDistID, maxMarchDistance);
    }
}
