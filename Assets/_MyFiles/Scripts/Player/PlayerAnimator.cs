using UnityEngine;

/// <summary>
/// Drives the Animator Controller for the Action Heroine character.
/// Reads state from PlayerController and WeaponHandler and sets
/// the appropriate Animator parameters each frame.
/// Upper body layer handles aim/shoot/hurt via blend trees.
/// Base layer handles idle/walk/death.
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector Fields
    // -------------------------------------------------------------------------

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private WeaponHandler weaponHandler;
    [SerializeField] private Transform firstPersonPivot; // FP_Pivot transform

    // -------------------------------------------------------------------------
    // Animator Parameter Hashes
    // -------------------------------------------------------------------------

    private static readonly int IsWalking  = Animator.StringToHash("isWalking");
    private static readonly int IsAiming   = Animator.StringToHash("isAiming");
    private static readonly int IsShooting = Animator.StringToHash("isShooting");
    private static readonly int HurtIndex  = Animator.StringToHash("hurtIndex");
    private static readonly int DeathIndex = Animator.StringToHash("deathIndex");
    private static readonly int AimAngle   = Animator.StringToHash("aimAngle");

    // -------------------------------------------------------------------------
    // Private State
    // -------------------------------------------------------------------------

    private bool isDead = false;

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (weaponHandler == null)
            weaponHandler = GetComponent<WeaponHandler>();
    }

    private void Update()
    {
        if (isDead) return;

        UpdateMovement();
        UpdateAim();
        UpdateAimAngle();
    }

    // -------------------------------------------------------------------------
    // Movement
    // -------------------------------------------------------------------------

    private void UpdateMovement()
    {
        bool isWalking = playerController != null && playerController.IsMoving;
        animator.SetBool(IsWalking, isWalking);

        if (isWalking)
        {
            Vector2 input = playerController.MoveInput;
            Vector3 moveDir = new Vector3(input.x, 0f, input.y).normalized;
            
            // Get dot product between move direction and facing direction
            float dot = Vector3.Dot(transform.forward, moveDir);
            
            // dot > 0 = moving forward, dot < 0 = moving backward
            animator.SetFloat("walkSpeed", dot >= 0 ? 1f : -1f);
        }
        else
        {
            animator.SetFloat("walkSpeed", 1f);
        }
    }
    // -------------------------------------------------------------------------
    // Aim
    // -------------------------------------------------------------------------

    private void UpdateAim()
    {
        bool isAiming = playerController != null && playerController.IsAiming;
        animator.SetBool(IsAiming, isAiming);
    }

    // -------------------------------------------------------------------------
    // Aim Angle (drives blend tree)
    // -------------------------------------------------------------------------

    private void UpdateAimAngle()
    {
        if (firstPersonPivot == null) return;

        // Get vertical rotation of FP_Pivot (-180 to 180)
        float rawAngle = firstPersonPivot.localEulerAngles.x;

        // Convert from 0-360 to -180 to 180
        if (rawAngle > 180f)
            rawAngle -= 360f;

        // Normalize to -1 to 1 based on clamp range (60 degrees up/down)
        float normalized = Mathf.Clamp(rawAngle / 60f, -1f, 1f);

        // Invert so looking up = positive, looking down = negative
        normalized = -normalized;

        animator.SetFloat(AimAngle, normalized);
    }

    // -------------------------------------------------------------------------
    // Shoot
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called by WeaponHandler when a shot is fired.
    /// </summary>
    public void TriggerShoot()
    {
        animator.SetTrigger(IsShooting);
    }

    // -------------------------------------------------------------------------
    // Hurt
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called when the player takes damage.
    /// Randomly picks one of the 4 hurt animations.
    /// </summary>
    public void TriggerHurt()
    {
        if (isDead) return;

        int randomHurt = Random.Range(1, 5);
        animator.SetInteger(HurtIndex, randomHurt);
        Invoke(nameof(ResetHurtIndex), 0.1f);
    }

    private void ResetHurtIndex()
    {
        animator.SetInteger(HurtIndex, 0);
    }

    // -------------------------------------------------------------------------
    // Death
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called when the player dies.
    /// Randomly picks one of the 4 death animations.
    /// </summary>
    public void TriggerDeath()
    {
        if (isDead) return;
        isDead = true;

        int randomDeath = Random.Range(1, 5);
        animator.SetInteger(DeathIndex, randomDeath);
    }

    // -------------------------------------------------------------------------
    // Reset (for respawn)
    // -------------------------------------------------------------------------

    public void ResetAnimator()
    {
        isDead = false;
        animator.SetInteger(DeathIndex, 0);
        animator.SetInteger(HurtIndex, 0);
        animator.SetBool(IsWalking, false);
        animator.SetBool(IsAiming, false);
        animator.SetFloat(AimAngle, 0f);
    }
}