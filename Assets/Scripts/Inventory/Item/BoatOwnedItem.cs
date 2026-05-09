using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatOwnedItem : MonoBehaviour
{
    public event Action<BoatOwnedItem> OwnershipChanged;

    [SerializeField] private string owningBoatInstanceId;
    [SerializeField] private bool registered;

    private BoatItemRegistry _registry;

    public string OwningBoatInstanceId => owningBoatInstanceId;
    public bool IsOwnedByBoat => !string.IsNullOrWhiteSpace(owningBoatInstanceId);

    public void AssignToBoat(Boat boat)
    {
        if (boat == null || string.IsNullOrWhiteSpace(boat.BoatInstanceId))
        {
            ClearOwnership();
            return;
        }

        if (_registry != null)
            _registry.Unregister(this);

        owningBoatInstanceId = boat.BoatInstanceId;

        _registry = boat.GetComponent<BoatItemRegistry>();
        if (_registry != null)
        {
            _registry.Register(this);
            registered = true;
        }
        else
        {
            registered = false;
            Debug.LogWarning($"[BoatOwnedItem:{name}] Assigned to boat '{boat.name}', but boat has no BoatItemRegistry.", this);
        }

        NotifyOwnershipChanged();
    }

    public void RestoreOwnership(Boat boat, string restoredBoatInstanceId)
    {
        if (boat == null)
        {
            owningBoatInstanceId = restoredBoatInstanceId;
            registered = false;
            NotifyOwnershipChanged();
            return;
        }

        AssignToBoat(boat);
    }

    public void ClearOwnership()
    {
        if (_registry != null)
            _registry.Unregister(this);

        _registry = null;
        owningBoatInstanceId = null;
        registered = false;

        NotifyOwnershipChanged();
    }

    private void NotifyOwnershipChanged()
    {
        OwnershipChanged?.Invoke(this);

        // Direct apply as a safety net, because events and Unity lifecycle
        // enjoy turning deterministic code into folk horror.
        BoatOwnedItemLayerPolicy layerPolicy = GetComponent<BoatOwnedItemLayerPolicy>();
        if (layerPolicy != null)
            layerPolicy.ApplyNow();

        BoatOwnedItemVisualPolicy visualPolicy = GetComponent<BoatOwnedItemVisualPolicy>();
        if (visualPolicy != null)
            visualPolicy.ApplyNow();
    }

    private void OnDestroy()
    {
        if (_registry != null)
            _registry.Unregister(this);

        OwnershipChanged = null;
    }
}