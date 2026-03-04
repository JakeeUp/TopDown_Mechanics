using UnityEngine;

/// <summary>
/// Drives the Animator Controller for the Action Heroine character.
/// Reads state from PlayerController and WeaponHandler and sets
/// the appropriate Animator parameters each frame.
/// Upper body layer handles aim/shoot/hurt.
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

    // -------------------------------------------------------------------------
    // Animator Parameter Hashes
    // (Using hashes instead of strings for better performance)
    // -------------------------------------------------------------------------

    private static readonly int IsWalking    = Animator.StringToHash("isWalking");
    private static readonly int IsAiming     = Animator.StringToHash("isAiming");
    private static readonly int IsShooting   = Animator.StringToHash("isShooting");
    private static readonly int HurtIndex    = Animator.StringToHash("hurtIndex");
    private static readonly int DeathIndex   = Animator.StringToHash("deathIndex");

    // -------------------------------------------------------------------------
    // Private State
    // -------------------------------------------------------------------------

    private bool isDead = false;

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Auto-find references on the same GameObject if not assigned
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
    }

    // -------------------------------------------------------------------------
    // Movement
    // -------------------------------------------------------------------------

    private void UpdateMovement()
    {
        bool isWalking = playerController != null && playerController.IsMoving;
        animator.SetBool(IsWalking, isWalking);
    }

    // -------------------------------------------------------------------------
    // Aim & Shoot
    // -------------------------------------------------------------------------

    private void UpdateAim()
    {
        bool isAiming = playerController != null && playerController.IsAiming;
        animator.SetBool(IsAiming, isAiming);
    }

    /// <summary>
    /// Call this from WeaponHandler when a shot is fired.
    /// </summary>
    public void TriggerShoot()
    {
        animator.SetTrigger(IsShooting);
    }

    // -------------------------------------------------------------------------
    // Hurt
    // -------------------------------------------------------------------------

    /// <summary>
    /// Call this when the player takes damage.
    /// Randomly picks one of the 4 hurt animations.
    /// </summary>
    public void TriggerHurt()
    {
        if (isDead) return;

        int randomHurt = Random.Range(1, 5); // 1 to 4 inclusive
        animator.SetInteger(HurtIndex, randomHurt);

        // Reset after a short delay so it can trigger again next hit
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
    /// Call this when the player dies.
    /// Randomly picks one of the 4 death animations.
    /// </summary>
    public void TriggerDeath()
    {
        if (isDead) return;
        isDead = true;

        int randomDeath = Random.Range(1, 5); // 1 to 4 inclusive
        animator.SetInteger(DeathIndex, randomDeath);
    }

    // -------------------------------------------------------------------------
    // Public Reset (for respawn)
    // -------------------------------------------------------------------------

    public void ResetAnimator()
    {
        isDead = false;
        animator.SetInteger(DeathIndex, 0);
        animator.SetInteger(HurtIndex, 0);
        animator.SetBool(IsWalking, false);
        animator.SetBool(IsAiming, false);
    }
}