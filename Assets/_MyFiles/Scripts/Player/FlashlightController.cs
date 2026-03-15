using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("Top-Down Flashlight")]
    [SerializeField] private float coneAngle = 30f;
    [SerializeField] private float range = 20f;
    [SerializeField] private float edgeSoftness = 0.08f;

    [Header("First-Person Flashlight")]
    [SerializeField] private float fpsConeAngle = 18f;
    [SerializeField] private float fpsRange = 25f;
    [SerializeField] private float fpsEdgeSoftness = 0.12f;

    [Header("Shared Settings")]
    [SerializeField] private float ambientRadius = 3f;
    [SerializeField] private float ambientIntensity = 0.15f;

    [Header("References")]
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private Transform firstPersonPivot;
    [SerializeField] private Transform flashlightHolder;

    // -------------------------------------------------------------------------
    // Private State
    // -------------------------------------------------------------------------

    private bool isOn = true;
    private Vector3 smoothedDir;

    // -------------------------------------------------------------------------
    // Shader Property IDs (cached)
    // -------------------------------------------------------------------------

    private static readonly int FlashlightPosID = Shader.PropertyToID("_FlashlightPos");
    private static readonly int FlashlightDirID = Shader.PropertyToID("_FlashlightDir");
    private static readonly int FlashlightParamsID = Shader.PropertyToID("_FlashlightParams");
    private static readonly int AmbientIntensityID = Shader.PropertyToID("_AmbientIntensity");
    private static readonly int InvVPMatrixID = Shader.PropertyToID("_FogOfWar_InvVPMatrix");
    private static readonly int NearFadeDistID = Shader.PropertyToID("_NearFadeDist");

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (cameraManager == null)
            cameraManager = GetComponent<CameraManager>();
        if (flashlightHolder != null)
            smoothedDir = flashlightHolder.forward;
        else if (firstPersonPivot != null)
            smoothedDir = firstPersonPivot.forward;
    }

    private void LateUpdate()
    {
        Vector3 flashlightPos;
        Vector3 flashlightDir;
        float activeAngle;
        float activeRange;
        float activeSoftness;
        float nearFade;

        bool isFPS = cameraManager != null && cameraManager.IsAiming;

        if (isFPS)
        {
            flashlightPos = firstPersonPivot.position;
            flashlightDir = firstPersonPivot.forward;
            activeAngle = fpsConeAngle;
            activeRange = fpsRange;
            activeSoftness = fpsEdgeSoftness;
            nearFade = 0.5f;
        }
        else
        {
            flashlightPos = flashlightHolder.position;
            Vector3 fwd = flashlightHolder.forward;
            flashlightDir = new Vector3(-fwd.x, fwd.y, fwd.z);
            activeAngle = coneAngle;
            activeRange = range;
            activeSoftness = edgeSoftness;
            nearFade = 3.0f;
        }

        // Smooth the direction to prevent jitter
        smoothedDir = Vector3.Lerp(smoothedDir, flashlightDir.normalized, 15f * Time.deltaTime);
        smoothedDir.Normalize();

        // Flashlight core params
        Shader.SetGlobalVector(FlashlightPosID, flashlightPos);
        Shader.SetGlobalVector(FlashlightDirID, smoothedDir);
        Shader.SetGlobalVector(FlashlightParamsID, new Vector4(
            isOn ? Mathf.Cos(activeAngle * Mathf.Deg2Rad) : 1f,
            isOn ? activeRange : 0f,
            ambientRadius,
            activeSoftness
        ));
        Shader.SetGlobalFloat(AmbientIntensityID, ambientIntensity);
        Shader.SetGlobalFloat(NearFadeDistID, nearFade);

        // Set inverse VP matrix for world position reconstruction in the fog shader.
        Camera cam = Camera.main;
        if (cam != null)
        {
            Matrix4x4 gpuProj = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);
            Matrix4x4 vp = gpuProj * cam.worldToCameraMatrix;
            Shader.SetGlobalMatrix(InvVPMatrixID, vp.inverse);
        }
    }

    // -------------------------------------------------------------------------
    // Input Callbacks (Send Messages)
    // -------------------------------------------------------------------------

    private void OnFlashlight(InputValue value)
    {
        if (!value.isPressed) return;
        isOn = !isOn;
        Debug.Log($"[FLASHLIGHT] {(isOn ? "ON" : "OFF")}");
    }

    // -------------------------------------------------------------------------
    // Public Accessors
    // -------------------------------------------------------------------------

    public bool IsOn => isOn;
}
