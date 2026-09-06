using UnityEngine;

[DisallowMultipleComponent]
public sealed class TurretModule : MonoBehaviour, IModuleToggleable, IInstalledModuleLifecycle, IPowerConsumerModule
{
    [Header("State")]
    [SerializeField] private bool isOn = true;

    [Header("References")]
    [SerializeField] private Transform barrelPivot;
    [SerializeField] private Transform muzzlePoint;

    [Header("Pitch")]
    [SerializeField] private float minPitchDegrees = -20f;
    [SerializeField] private float maxPitchDegrees = 45f;
    [SerializeField] private float pitchDegrees;
    [SerializeField] private float pitchSmooth = 18f;

    [Header("Firing")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float projectileSpeed = 18f;
    [SerializeField] private float fireCooldown = 0.35f;
    [SerializeField] private float firePowerCost = 2f;

    [Header("Power")]
    [SerializeField] private float idlePowerDemandPerSecond = 0.05f;

    private InstalledModule installedModule;
    private Hardpoint ownerHardpoint;
    private Boat ownerBoat;
    private BoatPowerState powerState;

    private float targetPitchDegrees;
    private float cooldownRemaining;

    public bool IsOn => isOn;
    public bool IsConsumingPower => isOn && idlePowerDemandPerSecond > 0f;
    public float PowerDemandPerSecond => Mathf.Max(0f, idlePowerDemandPerSecond);

    public float PitchDegrees => pitchDegrees;
    public float FirePowerCost => firePowerCost;
    public float FireCooldown => fireCooldown;

    private void Awake()
    {
        installedModule = GetComponent<InstalledModule>();

        targetPitchDegrees = Mathf.Clamp(pitchDegrees, minPitchDegrees, maxPitchDegrees);
        pitchDegrees = targetPitchDegrees;

        ApplyPitchImmediate();
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        if (cooldownRemaining > 0f)
            cooldownRemaining -= dt;

        if (isOn)
            TickIdlePower(dt);

        TickPitch(dt);
    }

    public void OnInstalled(Hardpoint owner)
    {
        ownerHardpoint = owner;
        installedModule = GetComponent<InstalledModule>();
        ResolveOwnership();
        ApplyPitchImmediate();
    }

    public void OnRemoved()
    {
    }

    public bool CanRun()
    {
        ResolveOwnership();
        return powerState != null && powerState.CurrentPower > 0f;
    }

    public bool Toggle()
    {
        return SetOn(!isOn);
    }

    public bool SetOn(bool value)
    {
        if (value && !CanRun())
        {
            isOn = false;
            return false;
        }

        isOn = value;
        return true;
    }

    public void AimAtWorldPoint(Vector2 worldPoint)
    {
        if (barrelPivot == null)
            return;

        Vector2 toTarget = worldPoint - (Vector2)barrelPivot.position;

        if (toTarget.sqrMagnitude < 0.0001f)
            return;

        float worldAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;

        float parentWorldAngle = barrelPivot.parent != null
            ? barrelPivot.parent.eulerAngles.z
            : 0f;

        float localAngle = Mathf.DeltaAngle(parentWorldAngle, worldAngle);

        SetPitchDegrees(localAngle);
    }

    public void SetPitchDegrees(float degrees)
    {
        targetPitchDegrees = Mathf.Clamp(degrees, minPitchDegrees, maxPitchDegrees);
    }

    public void SetPitchNormalized(float normalized)
    {
        float t = Mathf.InverseLerp(-1f, 1f, Mathf.Clamp(normalized, -1f, 1f));
        SetPitchDegrees(Mathf.Lerp(minPitchDegrees, maxPitchDegrees, t));
    }

    public bool CanFire()
    {
        if (!isOn)
            return false;

        if (cooldownRemaining > 0f)
            return false;

        if (powerState == null)
            ResolveOwnership();

        return powerState != null && powerState.CurrentPower >= firePowerCost;
    }

    public bool TryFire()
    {
        if (!CanFire())
            return false;

        if (!powerState.TryConsume(firePowerCost))
            return false;

        cooldownRemaining = fireCooldown;

        Vector2 origin = muzzlePoint != null ? muzzlePoint.position : transform.position;
        Vector2 dir = muzzlePoint != null ? muzzlePoint.right : transform.right;

        if (projectilePrefab != null)
        {
            GameObject projectile = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(Vector3.forward, Vector3.up));
            projectile.transform.right = dir;

            Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = dir.normalized * projectileSpeed;
        }
        else
        {
            Debug.DrawRay(origin, dir.normalized * 6f, Color.red, 0.25f);
        }

        return true;
    }

    private void TickIdlePower(float dt)
    {
        if (idlePowerDemandPerSecond <= 0f)
            return;

        if (powerState == null)
            ResolveOwnership();

        if (powerState == null || !powerState.TryConsume(idlePowerDemandPerSecond * dt))
            isOn = false;
    }

    private void TickPitch(float dt)
    {
        pitchDegrees = Mathf.Lerp(
            pitchDegrees,
            targetPitchDegrees,
            1f - Mathf.Exp(-pitchSmooth * dt));

        ApplyPitchImmediate();
    }

    private void ApplyPitchImmediate()
    {
        if (barrelPivot == null)
            return;

        barrelPivot.localRotation = Quaternion.Euler(0f, 0f, pitchDegrees);
    }

    private void ResolveOwnership()
    {
        if (installedModule == null)
            installedModule = GetComponent<InstalledModule>();

        if (ownerHardpoint == null && installedModule != null)
            ownerHardpoint = installedModule.OwnerHardpoint;

        if (ownerHardpoint != null)
            ownerBoat = ownerHardpoint.GetComponentInParent<Boat>();

        if (ownerBoat == null)
            ownerBoat = GetComponentInParent<Boat>();

        powerState = ownerBoat != null
            ? ownerBoat.GetComponent<BoatPowerState>()
            : null;
    }

    public void RestorePersistentState(bool restoredIsOn)
    {
        SetOn(restoredIsOn);
    }
}