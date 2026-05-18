using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(WorldItem))]
public sealed class BoatSecuredItem :
    MonoBehaviour,
    IInteractable,
    IUnsecureInteractable,
    IInteractPromptProvider,
    IInteractPromptActionProvider
{
    [Header("Interaction")]
    [SerializeField] private int interactionPriority = 25;
    [SerializeField] private float maxInteractDistance = 1.6f;

    [Header("Placeholder Quality")]
    [Range(0f, 1f)]
    [SerializeField] private float placeholderSecureQuality01 = 0.75f;

    [Header("Fasten / Maintenance")]
    [Tooltip("Default amount restored by the temporary Fasten API/debug action.")]
    [Range(0f, 1f)]
    [SerializeField] private float fastenRestoreAmount01 = 0.25f;

    [Header("Passive Decay")]
    [Tooltip("Temporary toggle. Later this should probably be controlled by BoatScene/travel state.")]
    [SerializeField] private bool enablePassiveDecay = false;

    [Tooltip("Base secure-quality decay per second before zone multiplier. Placeholder only.")]
    [Min(0f)]
    [SerializeField] private float passiveDecayPerSecond = 0.0025f;

    [Header("Impact Degradation")]
    [Tooltip("Quality loss when impact severity is 1.0 before zone impact protection. Placeholder only.")]
    [Min(0f)]
    [SerializeField] private float impactQualityLossAtSeverityOne = 0.5f;

    [Header("Failure")]
    [SerializeField] private bool breakLooseWhenQualityDepleted = true;

    [Range(0f, 1f)]
    [SerializeField] private float breakLooseQualityThreshold01 = 0.001f;

    [Header("Runtime Secured State")]
    [SerializeField] private bool isSecured;
    [SerializeField] private string secureZoneStableId;
    [SerializeField] private int secureSlotIndex = -1;

    [Range(0f, 1f)]
    [SerializeField] private float secureQualityMax01;

    [Range(0f, 1f)]
    [SerializeField] private float secureQualityCurrent01;

    [SerializeField] private Vector2 securedLocalPosition;
    [SerializeField] private float securedLocalRotationZ;

    [SerializeField] private bool usedRope;
    [SerializeField] private float ropeBonus01;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    private WorldItem _worldItem;
    private BoatOwnedItem _ownedItem;
    private Rigidbody2D _rb;
    private BoatSecureZone _zone;
    private Boat _boat;

    private RigidbodyType2D _bodyTypeBeforeSecure = RigidbodyType2D.Dynamic;
    private bool _hadRigidbodyBeforeSecure;

    public int InteractionPriority => interactionPriority;

    public bool IsSecured => isSecured;
    public string SecureZoneStableId => secureZoneStableId;
    public int SecureSlotIndex => secureSlotIndex;
    public float SecureQualityMax01 => secureQualityMax01;
    public float SecureQualityCurrent01 => secureQualityCurrent01;
    public Vector2 SecuredLocalPosition => securedLocalPosition;
    public float SecuredLocalRotationZ => securedLocalRotationZ;
    public bool UsedRope => usedRope;
    public float RopeBonus01 => ropeBonus01;

    public float SecureQualityNormalized =>
        secureQualityMax01 <= 0.0001f
            ? 0f
            : Mathf.Clamp01(secureQualityCurrent01 / secureQualityMax01);

    private void Awake()
    {
        CacheRefs();
    }

    private void OnValidate()
    {
        placeholderSecureQuality01 = Mathf.Clamp01(placeholderSecureQuality01);
        fastenRestoreAmount01 = Mathf.Clamp01(fastenRestoreAmount01);
        passiveDecayPerSecond = Mathf.Max(0f, passiveDecayPerSecond);
        impactQualityLossAtSeverityOne = Mathf.Max(0f, impactQualityLossAtSeverityOne);
        breakLooseQualityThreshold01 = Mathf.Clamp01(breakLooseQualityThreshold01);

        secureQualityMax01 = Mathf.Clamp01(secureQualityMax01);
        secureQualityCurrent01 = Mathf.Clamp01(secureQualityCurrent01);
        ropeBonus01 = Mathf.Max(0f, ropeBonus01);
    }

    private void Update()
    {
        if (!isSecured)
            return;

        if (enablePassiveDecay)
            TickPassiveDecay(Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (!isSecured)
            return;

        ApplySecuredTransform();
    }

    public bool CanInteract(in InteractContext context)
    {
        CacheRefs();

        if (_worldItem == null || _worldItem.Instance == null || _worldItem.Instance.Definition == null)
            return false;

        float dist = Vector2.Distance(context.Origin, transform.position);
        if (dist > maxInteractDistance)
            return false;

        if (isSecured)
            return IsInteractorOnSameBoat(context);

        return CanSecure(context, out _);
    }

    public void Interact(in InteractContext context)
    {
        if (isSecured)
        {
            TryFastenDefault();
            return;
        }

        TrySecurePlaceholder(context);
    }

    public string GetPromptVerb(in InteractContext context)
    {
        if (isSecured)
            return $"Fasten Cargo ({Mathf.RoundToInt(SecureQualityNormalized * 100f)}%)";

        return "Secure Cargo";
    }

    public void GetPromptActions(in InteractContext context, List<PromptAction> actions)
    {
        if (actions == null)
            return;

        CacheRefs();

        if (_worldItem == null || _worldItem.Instance == null || _worldItem.Instance.Definition == null)
            return;

        float dist = Vector2.Distance(context.Origin, transform.position);
        if (dist > maxInteractDistance)
            return;

        if (!isSecured)
        {
            if (CanSecure(context, out _))
                actions.Add(new PromptAction("Press E to Secure Cargo", priority: 100));

            return;
        }

        if (!IsInteractorOnSameBoat(context))
            return;

        int pct = Mathf.RoundToInt(SecureQualityNormalized * 100f);

        actions.Add(new PromptAction(
            $"Press E to Fasten Cargo ({pct}%)",
            priority: 100,
            showProgress: true,
            progress01: SecureQualityNormalized,
            pulse: SecureQualityNormalized <= 0.25f));

        actions.Add(new PromptAction(
            "Press X to Unsecure Cargo",
            priority: 90));
    }

    public Transform GetPromptAnchor()
    {
        return transform;
    }

    public bool CanPickupWhileSecured()
    {
        return !isSecured;
    }

    public bool TrySecurePlaceholder(in InteractContext context)
    {
        if (!CanSecure(context, out BoatSecureZone zone))
            return false;

        return SecureInZone(
            zone,
            placeholderSecureQuality01,
            placeholderSecureQuality01,
            usedRope: false,
            ropeBonus01: 0f);
    }

    public bool SecureInZone(
        BoatSecureZone zone,
        float qualityMax01,
        float qualityCurrent01,
        bool usedRope,
        float ropeBonus01)
    {
        CacheRefs();

        if (zone == null || _worldItem == null || _worldItem.Instance == null)
            return false;

        if (isSecured)
            return false;

        if (!zone.ContainsWorldPoint(transform.position))
        {
            Log($"Cannot secure item='{DescribeItem()}': not inside zone '{zone.name}'.");
            return false;
        }

        if (!zone.CanAccept(_worldItem.Instance))
        {
            Log($"Cannot secure item='{DescribeItem()}': zone '{zone.name}' cannot accept it.");
            return false;
        }

        if (!ZoneBelongsToSameBoat(zone))
        {
            Log($"Cannot secure item='{DescribeItem()}': zone '{zone.name}' belongs to a different boat.");
            return false;
        }

        if (!zone.HasPhysicalSupport(_worldItem))
        {
            Log($"Cannot secure item='{DescribeItem()}': no valid physical support under cargo.");
            return false;
        }

        if (!zone.TryReserveSlot(
                _worldItem.Instance,
                transform,
                out int reservedSlot,
                out Vector2 localPos,
                out float localRotZ))
        {
            return false;
        }

        _zone = zone;
        _boat = zone.GetBoatRoot() != null
            ? zone.GetBoatRoot().GetComponent<Boat>()
            : null;

        isSecured = true;
        secureZoneStableId = zone.StableId;
        secureSlotIndex = reservedSlot;

        securedLocalPosition = localPos;
        securedLocalRotationZ = localRotZ;

        float adjustedMax = ApplyZoneAndRopeQuality(zone, qualityMax01, ropeBonus01);
        float adjustedCurrent = ApplyZoneAndRopeQuality(zone, qualityCurrent01, ropeBonus01);

        secureQualityMax01 = Mathf.Clamp01(adjustedMax);
        secureQualityCurrent01 = Mathf.Clamp01(Mathf.Min(adjustedCurrent, secureQualityMax01));

        this.usedRope = usedRope;
        this.ropeBonus01 = Mathf.Max(0f, ropeBonus01);

        LockPhysics();
        ApplySecuredTransform();

        Log(
            $"Secured item='{DescribeItem()}' zone='{secureZoneStableId}' " +
            $"slot={secureSlotIndex} quality={secureQualityCurrent01:0.00}/{secureQualityMax01:0.00}");

        return true;
    }

    public bool TryUnsecure(in InteractContext context)
    {
        if (!CanUnsecure(context))
            return false;

        ReleaseCurrentZoneSlot();
        ClearSecuredState();
        UnlockPhysics();

        Log($"Unsecured item='{DescribeItem()}'");

        return true;
    }

    public bool TryFastenDefault()
    {
        return TryFasten(fastenRestoreAmount01);
    }

    public bool TryFasten(float restoreAmount01)
    {
        if (!isSecured)
            return false;

        restoreAmount01 = Mathf.Max(0f, restoreAmount01);

        if (restoreAmount01 <= 0f)
            return false;

        if (secureQualityMax01 <= 0f)
            return false;

        float old = secureQualityCurrent01;
        secureQualityCurrent01 = Mathf.Min(
            secureQualityMax01,
            secureQualityCurrent01 + restoreAmount01);

        bool changed = !Mathf.Approximately(old, secureQualityCurrent01);

        if (changed)
        {
            Log(
                $"Fastened item='{DescribeItem()}' " +
                $"quality {old:0.00} -> {secureQualityCurrent01:0.00}/{secureQualityMax01:0.00}");
        }

        return changed;
    }

    public bool ApplySecuringImpact(float severity01)
    {
        return ApplySecuringImpact(severity01, Vector2.zero);
    }

    public bool ApplySecuringImpact(float severity01, Vector2 breakLooseImpulse)
    {
        if (!isSecured)
            return false;

        severity01 = Mathf.Clamp01(severity01);

        if (severity01 <= 0f)
            return false;

        BoatSecureZone zone = ResolveCurrentZone();

        float protection01 = zone != null
            ? zone.ImpactProtection01
            : 0f;

        float qualityLoss =
            severity01 *
            Mathf.Max(0f, impactQualityLossAtSeverityOne) *
            (1f - Mathf.Clamp01(protection01));

        if (qualityLoss <= 0f)
            return false;

        return ApplySecureQualityDelta(
            -qualityLoss,
            $"impact severity={severity01:0.00}",
            breakLooseWhenQualityDepleted,
            breakLooseImpulse);
    }

    public bool BreakLooseFromSecuring(string reason)
    {
        return BreakLooseFromSecuring(reason, Vector2.zero);
    }

    public bool BreakLooseFromSecuring(string reason, Vector2 impulse)
    {
        if (!isSecured)
            return false;

        ReleaseCurrentZoneSlot();
        ClearSecuredState();
        UnlockPhysics();

        if (_rb != null && impulse.sqrMagnitude > 0.0001f)
            _rb.AddForce(impulse, ForceMode2D.Impulse);

        Log($"Cargo broke loose item='{DescribeItem()}' reason='{reason}' impulse={impulse}");

        return true;
    }

    public void RestoreSecuredState(
        Boat boat,
        BoatSecureZone zone,
        int slotIndex,
        float qualityMax01,
        float qualityCurrent01,
        Vector2 localPosition,
        float localRotationZ,
        bool usedRope,
        float ropeBonus01)
    {
        CacheRefs();

        _boat = boat;
        _zone = zone;

        isSecured = true;
        secureZoneStableId = zone != null ? zone.StableId : null;
        secureSlotIndex = slotIndex;

        secureQualityMax01 = Mathf.Clamp01(qualityMax01);
        secureQualityCurrent01 = Mathf.Clamp01(Mathf.Min(qualityCurrent01, secureQualityMax01));

        securedLocalPosition = localPosition;
        securedLocalRotationZ = localRotationZ;

        this.usedRope = usedRope;
        this.ropeBonus01 = Mathf.Max(0f, ropeBonus01);

        if (zone != null && _worldItem != null && _worldItem.Instance != null)
        {
            // Re-reserve best-effort. If it fails, keep the item visually secured for now,
            // but the zone may show odd occupancy until we add stricter restore validation.
            zone.TryReserveSlot(
                _worldItem.Instance,
                transform,
                out int restoredSlot,
                out _,
                out _);

            if (restoredSlot >= 0)
                secureSlotIndex = restoredSlot;
        }

        LockPhysics();
        ApplySecuredTransform();

        Log(
            $"Restored secured item='{DescribeItem()}' zone='{secureZoneStableId}' " +
            $"slot={secureSlotIndex} quality={secureQualityCurrent01:0.00}/{secureQualityMax01:0.00}");
    }

    private void TickPassiveDecay(float deltaTime)
    {
        if (!isSecured)
            return;

        if (deltaTime <= 0f)
            return;

        BoatSecureZone zone = ResolveCurrentZone();

        float zoneDecayMultiplier = zone != null
            ? zone.PassiveDecayMultiplier
            : 1f;

        float loss =
            Mathf.Max(0f, passiveDecayPerSecond) *
            Mathf.Max(0f, zoneDecayMultiplier) *
            deltaTime;

        if (loss <= 0f)
            return;

        ApplySecureQualityDelta(
            -loss,
            "passive decay",
            breakLooseWhenQualityDepleted,
            Vector2.zero);
    }

    private bool ApplySecureQualityDelta(
        float delta,
        string reason,
        bool breakLooseIfDepleted,
        Vector2 breakLooseImpulse)
    {
        if (!isSecured)
            return false;

        if (Mathf.Approximately(delta, 0f))
            return false;

        float old = secureQualityCurrent01;
        secureQualityCurrent01 = Mathf.Clamp01(secureQualityCurrent01 + delta);

        bool changed = !Mathf.Approximately(old, secureQualityCurrent01);

        if (changed)
        {
            Log(
                $"Quality changed item='{DescribeItem()}' reason='{reason}' " +
                $"{old:0.00} -> {secureQualityCurrent01:0.00}/{secureQualityMax01:0.00}");
        }

        if (breakLooseIfDepleted &&
            secureQualityCurrent01 <= Mathf.Clamp01(breakLooseQualityThreshold01))
        {
            BreakLooseFromSecuring(reason, breakLooseImpulse);
        }

        return changed;
    }

    private static float ApplyZoneAndRopeQuality(
        BoatSecureZone zone,
        float rawQuality01,
        float ropeBonus01)
    {
        float zoneMultiplier = zone != null
            ? zone.ZoneQualityMultiplier
            : 1f;

        return Mathf.Clamp01(
            Mathf.Clamp01(rawQuality01) * Mathf.Max(0f, zoneMultiplier) +
            Mathf.Max(0f, ropeBonus01));
    }

    private bool CanSecure(in InteractContext context, out BoatSecureZone zone)
    {
        zone = null;

        CacheRefs();

        if (isSecured)
            return false;

        if (_worldItem == null || _worldItem.Instance == null || _worldItem.Instance.Definition == null)
            return false;

        if (!CargoLabelFormatter.IsCargo(_worldItem.Instance))
            return false;

        if (_ownedItem == null || !_ownedItem.IsOwnedByBoat)
            return false;

        if (!IsInteractorOnSameBoat(context))
            return false;

        zone = FindBestZoneForItem();
        return zone != null;
    }

    public bool CanUnsecure(in InteractContext context)
    {
        if (!isSecured)
            return false;

        float dist = Vector2.Distance(context.Origin, transform.position);
        if (dist > maxInteractDistance)
            return false;

        return IsInteractorOnSameBoat(context);
    }

    public void Unsecure(in InteractContext context)
    {
        TryUnsecure(context);
    }

    private BoatSecureZone FindBestZoneForItem()
    {
        BoatSecureZone[] zones = FindObjectsByType<BoatSecureZone>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        BoatSecureZone best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < zones.Length; i++)
        {
            BoatSecureZone zone = zones[i];
            if (zone == null)
                continue;

            if (!zone.ContainsWorldPoint(transform.position))
                continue;

            if (!zone.CanAccept(_worldItem.Instance))
                continue;

            if (!ZoneBelongsToSameBoat(zone))
                continue;

            if (!zone.HasPhysicalSupport(_worldItem))
            {
                Log($"Rejected zone '{zone.name}': item is not physically supported.");
                continue;
            }

            float score =
                (zone.ZoneQualityMultiplier * 10f) +
                zone.FreeCount -
                Vector2.Distance(transform.position, zone.transform.position);

            if (score > bestScore)
            {
                bestScore = score;
                best = zone;
            }
        }

        return best;
    }

    private bool ZoneBelongsToSameBoat(BoatSecureZone zone)
    {
        if (zone == null || _ownedItem == null)
            return false;

        Transform root = zone.GetBoatRoot();
        if (root == null)
            return false;

        Boat zoneBoat = root.GetComponent<Boat>();
        if (zoneBoat == null)
            return false;

        return zoneBoat.BoatInstanceId == _ownedItem.OwningBoatInstanceId;
    }

    private bool IsInteractorOnSameBoat(in InteractContext context)
    {
        if (_ownedItem == null || !_ownedItem.IsOwnedByBoat)
            return false;

        PlayerBoardingState boarding = null;

        if (context.InteractorGO != null)
        {
            boarding =
                context.InteractorGO.GetComponentInParent<PlayerBoardingState>() ??
                context.InteractorGO.GetComponentInChildren<PlayerBoardingState>(true);
        }

        if (boarding == null || !boarding.IsBoarded || boarding.CurrentBoatRoot == null)
            return false;

        Boat boat =
            boarding.CurrentBoatRoot.GetComponent<Boat>() ??
            boarding.CurrentBoatRoot.GetComponentInParent<Boat>();

        if (boat == null)
            return false;

        return boat.BoatInstanceId == _ownedItem.OwningBoatInstanceId;
    }

    private void ApplySecuredTransform()
    {
        Transform root = null;

        BoatSecureZone zone = ResolveCurrentZone();

        if (zone != null)
            root = zone.GetBoatRoot();

        if (root == null && _boat != null)
            root = _boat.transform;

        if (root == null)
            return;

        transform.position = root.TransformPoint(securedLocalPosition);
        transform.rotation = root.rotation * Quaternion.Euler(0f, 0f, securedLocalRotationZ);
    }

    private void LockPhysics()
    {
        CacheRefs();

        if (_rb == null)
            return;

        _hadRigidbodyBeforeSecure = true;
        _bodyTypeBeforeSecure = _rb.bodyType;

        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.simulated = true;
    }

    private void UnlockPhysics()
    {
        CacheRefs();

        if (_rb == null)
            return;

        _rb.bodyType = _hadRigidbodyBeforeSecure
            ? _bodyTypeBeforeSecure
            : RigidbodyType2D.Dynamic;

        _rb.simulated = true;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
    }

    private void ReleaseCurrentZoneSlot()
    {
        BoatSecureZone zone = ResolveCurrentZone();

        if (zone != null && _worldItem != null && _worldItem.Instance != null)
            zone.ReleaseSlot(_worldItem.Instance.InstanceId);
    }

    private void ClearSecuredState()
    {
        isSecured = false;
        secureZoneStableId = null;
        secureSlotIndex = -1;
        secureQualityMax01 = 0f;
        secureQualityCurrent01 = 0f;
        securedLocalPosition = Vector2.zero;
        securedLocalRotationZ = 0f;
        usedRope = false;
        ropeBonus01 = 0f;
        _zone = null;
    }

    private BoatSecureZone ResolveCurrentZone()
    {
        if (_zone != null)
            return _zone;

        if (!string.IsNullOrWhiteSpace(secureZoneStableId))
            _zone = FindZoneByStableId(secureZoneStableId);

        return _zone;
    }

    private BoatSecureZone FindZoneByStableId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        BoatSecureZone[] zones = FindObjectsByType<BoatSecureZone>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] != null && zones[i].StableId == id)
                return zones[i];
        }

        return null;
    }

    private void CacheRefs()
    {
        if (_worldItem == null)
            _worldItem = GetComponent<WorldItem>();

        if (_ownedItem == null)
            _ownedItem = GetComponent<BoatOwnedItem>();

        if (_rb == null)
            _rb = GetComponent<Rigidbody2D>();

        if (_boat == null && _ownedItem != null && _ownedItem.IsOwnedByBoat)
        {
            Boat[] boats = FindObjectsByType<Boat>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < boats.Length; i++)
            {
                if (boats[i] != null && boats[i].BoatInstanceId == _ownedItem.OwningBoatInstanceId)
                {
                    _boat = boats[i];
                    break;
                }
            }
        }
    }

    private string DescribeItem()
    {
        if (_worldItem == null || _worldItem.Instance == null || _worldItem.Instance.Definition == null)
            return "empty";

        return $"{_worldItem.Instance.Definition.ItemId} inst={_worldItem.Instance.InstanceId}";
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[BoatSecuredItem:{name}] {msg}", this);
    }

    [ContextMenu("Debug/Fasten Default")]
    private void DebugFastenDefault()
    {
        TryFastenDefault();
    }

    [ContextMenu("Debug/Apply Impact 25%")]
    private void DebugApplyImpact25()
    {
        ApplySecuringImpact(0.25f);
    }

    [ContextMenu("Debug/Apply Impact 100%")]
    private void DebugApplyImpact100()
    {
        ApplySecuringImpact(1f);
    }

    [ContextMenu("Debug/Break Loose")]
    private void DebugBreakLoose()
    {
        BreakLooseFromSecuring("debug context menu");
    }
}