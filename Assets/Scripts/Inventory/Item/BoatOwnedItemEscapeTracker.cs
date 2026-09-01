using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatOwnedItemEscapeTracker : MonoBehaviour
{
    [SerializeField] private BoatOwnedItem ownedItem;
    [SerializeField] private float secondsOutsideBeforeClearing = 5f;

    private float _outsideTimer;

    private Boat _cachedBoat;
    private BoatItemContainmentZone _cachedZone;

    // One loud failure per Boat, not one per loose item. A cargo spill should not
    // produce 300 modal screams because the same boat is misconfigured once.
    private static readonly System.Collections.Generic.HashSet<int>
        _reportedInvalidContainmentBoatIds =
            new System.Collections.Generic.HashSet<int>();

    private void Awake()
    {
        if (ownedItem == null)
            ownedItem = GetComponent<BoatOwnedItem>();
    }

    private void Update()
    {
        if (ownedItem == null || !ownedItem.IsOwnedByBoat)
        {
            _outsideTimer = 0f;
            return;
        }

        if (!TryGetOwningBoat(out Boat boat))
            return;

        if (!TryResolveValidContainmentZone(
                boat,
                out BoatItemContainmentZone zone))
        {
            // Fail safe. Without containment authority, a remote/falling item must
            // never be allowed to drag the Boat COM into the abyss.
            ownedItem.SetPhysicallyContainedByOwningBoat(false);
            return;
        }

        if (zone.Contains(ownedItem))
        {
            // Re-entering the owning boat restores physical mass contribution
            // immediately, even if ownership never expired during the grace period.
            ownedItem.SetPhysicallyContainedByOwningBoat(true);
            _outsideTimer = 0f;
            return;
        }

        // Physical support ends immediately on escape. Ownership intentionally
        // remains for the existing grace period below so this pass changes only
        // Boat mass/COM behavior, not persistence/layers/recovery semantics.
        ownedItem.SetPhysicallyContainedByOwningBoat(false);

        _outsideTimer += Time.deltaTime;

        if (_outsideTimer >= secondsOutsideBeforeClearing)
        {
            ownedItem.ClearOwnership();
            _outsideTimer = 0f;
        }
    }

    private bool TryResolveValidContainmentZone(
        Boat boat,
        out BoatItemContainmentZone zone)
    {
        zone = null;

        if (boat == null)
            return false;

        if (_cachedBoat == boat &&
            _cachedZone != null &&
            IsContainmentZoneRuntimeValid(_cachedZone))
        {
            zone = _cachedZone;
            return true;
        }

        _cachedBoat = boat;
        _cachedZone = null;

        BoatItemContainmentZone[] zones =
            boat.GetComponentsInChildren<BoatItemContainmentZone>(
                true);

        if (zones == null || zones.Length != 1)
        {
            ReportContainmentConfigurationFailure(
                boat,
                zones == null
                    ? 0
                    : zones.Length,
                "Owning boat must contain exactly ONE BoatItemContainmentZone.");
            return false;
        }

        BoatItemContainmentZone candidate =
            zones[0];

        if (!IsContainmentZoneRuntimeValid(candidate))
        {
            ReportContainmentConfigurationFailure(
                boat,
                1,
                "BoatItemContainmentZone exists but is disabled, inactive, missing its Collider2D, or has a disabled collider.");
            return false;
        }

        _cachedZone = candidate;
        zone = candidate;
        return true;
    }

    private static bool IsContainmentZoneRuntimeValid(
        BoatItemContainmentZone zone)
    {
        if (zone == null ||
            !zone.isActiveAndEnabled ||
            !zone.gameObject.activeInHierarchy)
        {
            return false;
        }

        Collider2D collider =
            zone.GetComponent<Collider2D>();

        return collider != null &&
               collider.enabled;
    }

    private void ReportContainmentConfigurationFailure(
        Boat boat,
        int foundCount,
        string detail)
    {
        if (boat == null)
            return;

        int boatInstanceId =
            boat.GetInstanceID();

        if (!_reportedInvalidContainmentBoatIds.Add(
                boatInstanceId))
        {
            return;
        }

        Debug.LogError(
            $"[BoatOwnedItemEscapeTracker] FATAL BOAT CONFIGURATION: " +
            $"Boat '{boat.name}' has invalid loose-item containment. " +
            $"Found zones={foundCount}. {detail} " +
            $"Loose BoatOwnedItem mass has been disabled as a safety fallback. " +
            $"Open Boat Builder and add/fix the required BoatItemContainmentZone.",
            boat);

#if UNITY_EDITOR
        // This is deliberately a hard development failure. Continuing silently
        // can create absurd COM values and destabilize the entire boat simulation.
        Debug.Break();
#endif
    }

    private bool TryGetOwningBoat(out Boat boat)
    {
        boat = null;

        if (GameState.I == null || GameState.I.boatRegistry == null)
            return false;

        return GameState.I.boatRegistry.TryGetById(ownedItem.OwningBoatInstanceId, out boat);
    }
}