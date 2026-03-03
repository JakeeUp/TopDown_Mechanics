using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles raycast-based shooting in first-person mode.
/// Supports semi-auto and full-auto fire mode toggle.
/// Includes procedural Aim Down Sights (ADS) by lerping the weapon mesh
/// to a defined ADS position when aiming.
/// Only fires when player is in FPS/aiming mode.
/// </summary>
public class WeaponHandler : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector Fields
    // -------------------------------------------------------------------------

    [Header("Weapon Stats")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private float range = 100f;
    [SerializeField] private float fireRate = 0.1f;

    [Header("Raycast")]
    [SerializeField] private Camera fpsCamera;
    [SerializeField] private LayerMask hitLayers;

    [Header("Effects")]
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private GameObject bulletImpactPrefab;
    [SerializeField] private float impactDecalLifetime = 2f;

    [Header("Recoil")]
    [SerializeField] private Transform weaponMesh;
    [SerializeField] private float recoilAmount = 0.05f;
    [SerializeField] private float recoilSpeed = 10f;
    [SerializeField] private float recoilRecoverySpeed = 6f;

    [Header("Aim Down Sights")]
    [SerializeField] private Vector3 hipPosition = new Vector3(0.3f, -0.3f, 0.5f);
    [SerializeField] private Vector3 adsPosition = new Vector3(0f, -0.2f, 0.4f);
    [SerializeField] private float adsSpeed = 10f;

    // -------------------------------------------------------------------------
    // Private State
    // -------------------------------------------------------------------------

    public enum FireMode { SemiAuto, FullAuto }
    private FireMode currentFireMode = FireMode.SemiAuto;

    private bool isTriggerHeld = false;
    private bool isAiming = false;
    private float nextFireTime = 0f;

    private Vector3 recoilOffset;
    private Vector3 currentWeaponPosition;

    private CameraManager cameraManager;

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        cameraManager = GetComponent<CameraManager>();

        if (fpsCamera == null)
            fpsCamera = Camera.main;

        if (weaponMesh != null)
        {
            weaponMesh.localPosition = hipPosition;
            currentWeaponPosition = hipPosition;
        }
    }

    private void Update()
    {
        HandleFullAuto();
        HandleRecoilRecovery();
        HandleADS();
    }

    // -------------------------------------------------------------------------
    // Shooting
    // -------------------------------------------------------------------------

    private void HandleFullAuto()
    {
        // Must be holding trigger AND in FPS mode AND full auto
        if (currentFireMode == FireMode.FullAuto && isTriggerHeld && isAiming)
        {
            if (Time.time >= nextFireTime)
                Shoot();
        }
    }

    private void Shoot()
    {
        if (!isAiming) return;
        if (fpsCamera == null) return;

        nextFireTime = Time.time + fireRate;

        if (muzzleFlash != null)
            muzzleFlash.Play();

        ApplyRecoil();

        Ray ray = fpsCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, range, hitLayers))
        {
            Debug.Log($"Hit: {hit.collider.gameObject.name} | Damage: {damage}");

            if (bulletImpactPrefab != null)
            {
                GameObject impact = Instantiate(
                    bulletImpactPrefab,
                    hit.point + hit.normal * 0.01f,
                    Quaternion.LookRotation(hit.normal)
                );
                Destroy(impact, impactDecalLifetime);
            }

            // Uncomment when HealthComponent exists:
            // hit.collider.GetComponent<HealthComponent>()?.TakeDamage(damage);
        }
    }

    // -------------------------------------------------------------------------
    // Aim Down Sights
    // -------------------------------------------------------------------------

    private void HandleADS()
    {
        if (weaponMesh == null) return;

        Vector3 targetPosition = isAiming ? adsPosition : hipPosition;

        currentWeaponPosition = Vector3.Lerp(
            currentWeaponPosition,
            targetPosition,
            adsSpeed * Time.deltaTime
        );

        weaponMesh.localPosition = currentWeaponPosition + recoilOffset;
    }

    // -------------------------------------------------------------------------
    // Recoil
    // -------------------------------------------------------------------------

    private void ApplyRecoil()
    {
        if (weaponMesh == null) return;
        recoilOffset -= new Vector3(0f, 0f, recoilAmount);
    }

    private void HandleRecoilRecovery()
    {
        recoilOffset = Vector3.Lerp(recoilOffset, Vector3.zero, recoilRecoverySpeed * Time.deltaTime);
    }

    // -------------------------------------------------------------------------
    // Input Callbacks (Send Messages)
    // -------------------------------------------------------------------------

    private void OnFire(InputValue value)
    {
        isTriggerHeld = value.isPressed;

        // Semi-auto: only fire once on press, and only if in FPS mode
        if (currentFireMode == FireMode.SemiAuto && value.isPressed && isAiming)
            Shoot();
    }

    private void OnAim(InputValue value)
{
    isAiming = value.isPressed;
    if (!isAiming) isTriggerHeld = false; // force reset trigger on aim release
}

    private void OnSwitchFireMode(InputValue value)
    {
        if (!value.isPressed) return;

        currentFireMode = currentFireMode == FireMode.SemiAuto
            ? FireMode.FullAuto
            : FireMode.SemiAuto;

        Debug.Log($"Fire Mode: {currentFireMode}");
    }

    // -------------------------------------------------------------------------
    // Public Accessors
    // -------------------------------------------------------------------------

    public FireMode CurrentFireMode => currentFireMode;
    public bool IsFullAuto => currentFireMode == FireMode.FullAuto;
}