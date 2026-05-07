using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatOwnedItemEscapeTracker : MonoBehaviour
{
    [SerializeField] private BoatOwnedItem ownedItem;
    [SerializeField] private float secondsOutsideBeforeClearing = 5f;

    private float _outsideTimer;

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

        BoatItemContainmentZone zone = boat.GetComponentInChildren<BoatItemContainmentZone>();
        if (zone == null)
            return;

        if (zone.Contains(ownedItem))
        {
            _outsideTimer = 0f;
            return;
        }

        _outsideTimer += Time.deltaTime;

        if (_outsideTimer >= secondsOutsideBeforeClearing)
        {
            ownedItem.ClearOwnership();
            _outsideTimer = 0f;
        }
    }

    private bool TryGetOwningBoat(out Boat boat)
    {
        boat = null;

        if (GameState.I == null || GameState.I.boatRegistry == null)
            return false;

        return GameState.I.boatRegistry.TryGetById(ownedItem.OwningBoatInstanceId, out boat);
    }
}