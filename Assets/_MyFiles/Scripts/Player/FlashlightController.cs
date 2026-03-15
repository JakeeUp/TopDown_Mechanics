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
    // Private State
    // -------------------------------------------------------------------------

    private bool isOn = true;

    // -------------------------------------------------------------------------
    // Shader Property IDs (cached)
    // -------------------------------------------------------------------------

    private static readonly int FlashlightPosID = Shader.PropertyToID("_FlashlightPos");
    private static readonly int FlashlightDirID = Shader.PropertyToID("_FlashlightDir");
    private static readonly int FlashlightParamsID = Shader.PropertyToID("_FlashlightParams");
    private static readonly int FogDensityID = Shader.PropertyToID("_FogDensity");
    private static readonly int AmbientIntensityID = Shader.PropertyToID("_AmbientIntensity");
    private static readonly int InvVPMatrixID = Shader.PropertyToID("_FogOfWar_InvVPMatrix");

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
            // FPS mode: flashlight follows where you're looking
            flashlightPos = firstPersonPivot.position;
            flashlightDir = firstPersonPivot.forward;
        }
        else
        {
            // Top-down mode: use the same direction PlayerController rotates toward
            flashlightPos = playerBody.position + Vector3.up * 1.0f;
            flashlightDir = -playerBody.forward;
        }

        Shader.SetGlobalVector(FlashlightPosID, flashlightPos);
        Shader.SetGlobalVector(FlashlightDirID, flashlightDir.normalized);
        Shader.SetGlobalVector(FlashlightParamsID, new Vector4(
            isOn ? Mathf.Cos(coneAngle * Mathf.Deg2Rad) : 1f, // cos(0) = 1 means zero-width cone when off
            isOn ? range : 0f,
            ambientRadius,
            edgeSoftness
        ));
        Shader.SetGlobalFloat(FogDensityID, fogDensity);
        Shader.SetGlobalFloat(AmbientIntensityID, ambientIntensity);

        // Set inverse VP matrix for world position reconstruction in the fog shader.
        // This must be done here (not in RecordRenderGraph) because RenderGraph is deferred.
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
