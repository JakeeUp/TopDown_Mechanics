using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

/// <summary>
/// Manages the perspective switch between top-down third-person and first-person.
/// Drives Cinemachine camera priority to trigger a smooth blend when the player aims.
/// Notifies PlayerController to lock lateral movement while in FPS mode.
/// </summary>
public class CameraManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector Fields
    // -------------------------------------------------------------------------

    [Header("Cinemachine Cameras")]
    [SerializeField] private CinemachineCamera topDownCamera;
    [SerializeField] private CinemachineCamera firstPersonCamera;

    [Header("Priority Settings")]
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int inactivePriority = 5;

    [Header("First Person Look")]
    [SerializeField] private float mouseSensitivity = 150f;
    [SerializeField] private float verticalClampMin = -60f;
    [SerializeField] private float verticalClampMax = 60f;

    [Header("References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Transform playerBody;
    [SerializeField] private Transform firstPersonPivot;

    // -------------------------------------------------------------------------
    // Private State
    // -------------------------------------------------------------------------

    private enum PerspectiveState { TopDown, FirstPerson }
    private PerspectiveState currentState = PerspectiveState.TopDown;

    private bool isAiming = false;
    private float verticalRotation = 0f;

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();

        // Start in top-down mode
        SetTopDown();
    }

    private void Update()
    {
        if (currentState == PerspectiveState.FirstPerson)
            HandleFirstPersonLook();
    }

    // -------------------------------------------------------------------------
    // Input Callbacks (Send Messages)
    // -------------------------------------------------------------------------

    private void OnAim(InputValue value)
    {
        isAiming = value.isPressed;

        if (isAiming)
            SetFirstPerson();
        else
            SetTopDown();
    }
    // -------------------------------------------------------------------------
    // Perspective Switching
    // -------------------------------------------------------------------------

    private void SetFirstPerson()
    {
        currentState = PerspectiveState.FirstPerson;

        // Boost FPS camera priority so Cinemachine blends to it
        firstPersonCamera.Priority = activePriority;
        topDownCamera.Priority = inactivePriority;

        // Lock cursor for FPS look
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Tell PlayerController to stop lateral movement
        playerController?.SetAimingState(true);
    }

    private void SetTopDown()
    {
        currentState = PerspectiveState.TopDown;

        // Drop FPS camera priority so Cinemachine blends back to top-down
        topDownCamera.Priority = activePriority;
        firstPersonCamera.Priority = inactivePriority;

        // Restore cursor for top-down mouse aim
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Tell PlayerController to resume normal movement
        playerController?.SetAimingState(false);

        // Reset vertical rotation so re-entering FPS feels clean
        verticalRotation = 0f;
    }

    // -------------------------------------------------------------------------
    // First Person Mouse Look
    // -------------------------------------------------------------------------

    private void HandleFirstPersonLook()
    {
        if (Mouse.current == null) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        // Horizontal — rotate player body
        float horizontalRotation = mouseDelta.x * mouseSensitivity * Time.deltaTime;
        playerBody.Rotate(Vector3.up * horizontalRotation);

        // Vertical — tilt FP_Pivot up and down
        verticalRotation -= mouseDelta.y * mouseSensitivity * Time.deltaTime;
        verticalRotation = Mathf.Clamp(verticalRotation, verticalClampMin, verticalClampMax);

        if (firstPersonPivot != null)
            firstPersonPivot.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    public bool IsAiming => isAiming;
}