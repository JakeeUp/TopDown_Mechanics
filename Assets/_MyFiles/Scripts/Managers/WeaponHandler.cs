using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles raycast-based shooting in first-person mode.
/// Supports semi-auto and full-auto fire mode toggle.
/// Includes procedural ADS, muzzle-origin line trace, and console hit logging.
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

    [Header("Line Trace Visual")]
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private float lineVisibleDuration = 0.05f;

    [Header("Effects")]
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private GameObject bulletImpactPrefab;
    [SerializeField] private float impactDecalLifetime = 2f;

    [Header("Recoil")]
    [SerializeField] private Transform weaponMesh;
    [SerializeField] private float recoilAmount = 0.05f;
    [SerializeField] private float recoilSpeed = 10f;
    [SerializeField] private float recoilRecoverySpeed = 6f;

    // [Header("Aim Down Sights")]
    // [SerializeField] private Vector3 hipPosition = new Vector3(0.3f, -0.3f, 0.5f);
    // [SerializeField] private Vector3 adsPosition = new Vector3(0f, -0.2f, 0.4f);
    // [SerializeField] private float adsSpeed = 10f;

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
    private float lineTimer = 0f;

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
            // weaponMesh.localPosition = hipPosition;
            // currentWeaponPosition = hipPosition;
        }

        if (lineRenderer != null)
            lineRenderer.enabled = false;
    }

    private void Update()
    {
        HandleFullAuto();
        HandleRecoilRecovery();
       // HandleADS();
        HandleLineTimer();
    }

    // -------------------------------------------------------------------------
    // Shooting
    // -------------------------------------------------------------------------

    private void HandleFullAuto()
    {
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
        GetComponent<PlayerAnimator>()?.TriggerShoot();

        Ray ray = fpsCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 endPoint;

        if (Physics.Raycast(ray, out RaycastHit hit, range, hitLayers))
        {
            endPoint = hit.point;

            // Log hit info to console
            Debug.Log($"[HIT] {hit.collider.gameObject.name} | DMG: {damage} | Distance: {hit.distance:F1}m | Point: {hit.point}");

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
        else
        {
            endPoint = ray.origin + ray.direction * range;
            Debug.Log($"[MISS] No target in range ({range}m)");
        }

        DrawLineTrace(endPoint);
    }

    // -------------------------------------------------------------------------
    // Line Trace Visual
    // -------------------------------------------------------------------------

    private void DrawLineTrace(Vector3 endPoint)
    {
        if (lineRenderer == null) return;

        Vector3 startPoint = muzzlePoint != null
            ? muzzlePoint.position
            : fpsCamera.transform.position;

        lineRenderer.enabled = true;
        lineRenderer.SetPosition(0, startPoint);
        lineRenderer.SetPosition(1, endPoint);
        lineTimer = lineVisibleDuration;
    }

    private void HandleLineTimer()
    {
        if (lineTimer > 0f)
        {
            lineTimer -= Time.deltaTime;
            if (lineTimer <= 0f && lineRenderer != null)
                lineRenderer.enabled = false;
        }
    }

    // -------------------------------------------------------------------------
    // ADS
    // -------------------------------------------------------------------------

    // private void HandleADS()
    // {
    //     if (weaponMesh == null) return;

    //     Vector3 targetPosition = isAiming ? adsPosition : hipPosition;

    //     currentWeaponPosition = Vector3.Lerp(
    //         currentWeaponPosition,
    //         targetPosition,
    //         adsSpeed * Time.deltaTime
    //     );

    //     weaponMesh.localPosition = currentWeaponPosition + recoilOffset;
    // }

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

        if (currentFireMode == FireMode.SemiAuto && value.isPressed && isAiming)
            Shoot();
    }

    private void OnAim(InputValue value)
    {
        isAiming = value.isPressed;
        if (!isAiming) isTriggerHeld = false;
    }

    private void OnSwitchFireMode(InputValue value)
    {
        if (!value.isPressed) return;

        currentFireMode = currentFireMode == FireMode.SemiAuto
            ? FireMode.FullAuto
            : FireMode.SemiAuto;

        Debug.Log($"[FIRE MODE] Switched to: {currentFireMode}");
    }

    // -------------------------------------------------------------------------
    // Public Accessors
    // -------------------------------------------------------------------------

    public FireMode CurrentFireMode => currentFireMode;
    public bool IsFullAuto => currentFireMode == FireMode.FullAuto;
}