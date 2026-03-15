using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Core top-down player controller.
/// Handles Rigidbody-based movement with smooth acceleration/deceleration,
/// mouse cursor rotation, and sprint. Supports slower movement while aiming
/// in FPS mode. Works alongside CameraManager for perspective switching.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector Fields
    // -------------------------------------------------------------------------

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float sprintMultiplier = 1.6f;
    [SerializeField] private float aimMoveMultiplier = 0.4f;
    [SerializeField] private float acceleration = 80f;
    [SerializeField] private float deceleration = 100f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 20f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 0.2f;

    [Header("References")]
    [SerializeField] private Camera mainCamera;

    // -------------------------------------------------------------------------
    // Private State
    // -------------------------------------------------------------------------

    private Rigidbody rb;
    private Vector2 moveInput;
    private bool isSprinting;
    private bool isAiming;
    private bool isGrounded;
    private Vector3 currentVelocity;

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void Update()
    {
        CheckGrounded();

        if (!isAiming)
            RotateTowardCursor();
    }

    private void FixedUpdate()
    {
        HandleMovement();
    }

    // -------------------------------------------------------------------------
    // Movement
    // -------------------------------------------------------------------------

    private void HandleMovement()
    {
        Vector3 inputDirection;

        if (isAiming)
        {
            // In FPS mode move relative to where the player is facing
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            forward.y = 0f;
            right.y = 0f;
            inputDirection = (forward * moveInput.y + right * moveInput.x).normalized;
        }
        else
        {
            // Top-down: standard world space movement
            inputDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        }

        // Reduce speed while aiming, boost while sprinting
        float targetSpeed = isAiming   ? moveSpeed * aimMoveMultiplier :
                            isSprinting ? moveSpeed * sprintMultiplier : moveSpeed;

        Vector3 targetVelocity = inputDirection * targetSpeed;

        float rate = inputDirection.magnitude > 0.1f ? acceleration : deceleration;

        currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, rate * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(currentVelocity.x, rb.linearVelocity.y, currentVelocity.z);
    }

    // -------------------------------------------------------------------------
    // Rotation
    // -------------------------------------------------------------------------

    private void RotateTowardCursor()
    {
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
        {
            Vector3 lookTarget = hit.point;
            lookTarget.y = transform.position.y;

            Vector3 direction = (lookTarget - transform.position).normalized;

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }
        }
    }

    // -------------------------------------------------------------------------
    // Ground Check
    // -------------------------------------------------------------------------

    private void CheckGrounded()
    {
        isGrounded = Physics.Raycast(
            transform.position + Vector3.up * 0.1f,
            Vector3.down,
            groundCheckDistance + 0.1f,
            groundLayer
        );
    }

    // -------------------------------------------------------------------------
    // Input Callbacks (Send Messages)
    // -------------------------------------------------------------------------

    private void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    private void OnSprint(InputValue value)
    {
        isSprinting = value.isPressed;
    }

    // -------------------------------------------------------------------------
    // Called by CameraManager when switching perspectives
    // -------------------------------------------------------------------------

    public void SetAimingState(bool aiming)
    {
        isAiming = aiming;

        // Bleed off velocity slightly when entering FPS so it feels deliberate
        if (isAiming)
            currentVelocity *= 0.3f;
    }

    // -------------------------------------------------------------------------
    // Public Accessors
    // -------------------------------------------------------------------------

    public bool IsGrounded => isGrounded;
    public bool IsMoving => currentVelocity.sqrMagnitude > 0.01f;
    public bool IsSprinting => isSprinting;
    public bool IsAiming => isAiming;
    public Vector2 MoveInput => moveInput;
}