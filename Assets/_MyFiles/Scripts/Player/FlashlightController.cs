using UnityEngine;

/// <summary>
/// Computes flashlight position and direction each frame
/// and pushes the values to global shader properties for the FogOfWar shader.
/// Attach to the same GameObject as CameraManager (the Player root).
/// </summary>
public class FlashlightController : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector Fields
    // -------------------------------------------------------------------------

    [Header("Flashlight Settings")]
    [SerializeField] private float coneAngle = 30f;
    [SerializeField] private float range = 20f;
    [SerializeField] private float ambientRadius = 3f;
    [SerializeField] private float ambientIntensity = 0.15f;
    [SerializeField] private float edgeSoftness = 0.08f;
    [SerializeField] private float fogDensity = 0.97f;

    [Header("References")]
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private Transform firstPersonPivot;
    [SerializeField] private Transform playerBody;

    // -------------------------------------------------------------------------
    // Shader Property IDs (cached)
    // -------------------------------------------------------------------------

    private static readonly int FlashlightPosID = Shader.PropertyToID("_FlashlightPos");
    private static readonly int FlashlightDirID = Shader.PropertyToID("_FlashlightDir");
    private static readonly int FlashlightParamsID = Shader.PropertyToID("_FlashlightParams");
    private static readonly int FogDensityID = Shader.PropertyToID("_FogDensity");
    private static readonly int AmbientIntensityID = Shader.PropertyToID("_AmbientIntensity");

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (cameraManager == null)
            cameraManager = GetComponent<CameraManager>();
    }

    private void LateUpdate()
    {
        Vector3 flashlightPos;
        Vector3 flashlightDir;

        if (cameraManager != null && cameraManager.IsAiming)
        {
            // FPS mode: flashlight follows camera look direction
            flashlightPos = firstPersonPivot.position;
            flashlightDir = firstPersonPivot.forward;
        }
        else
        {
            // Top-down mode: flashlight from player chest, pointing forward
            flashlightPos = playerBody.position + Vector3.up * 1.0f;
            flashlightDir = playerBody.forward;
        }

        Shader.SetGlobalVector(FlashlightPosID, flashlightPos);
        Shader.SetGlobalVector(FlashlightDirID, flashlightDir.normalized);
        Shader.SetGlobalVector(FlashlightParamsID, new Vector4(
            Mathf.Cos(coneAngle * Mathf.Deg2Rad),
            range,
            ambientRadius,
            edgeSoftness
        ));
        Shader.SetGlobalFloat(FogDensityID, fogDensity);
        Shader.SetGlobalFloat(AmbientIntensityID, ambientIntensity);
    }
}
