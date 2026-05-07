using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatOwnedItem : MonoBehaviour
{
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
    }

    public void RestoreOwnership(Boat boat, string restoredBoatInstanceId)
    {
        if (boat == null)
        {
            owningBoatInstanceId = restoredBoatInstanceId;
            registered = false;
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
    }

    private void OnDestroy()
    {
        if (_registry != null)
            _registry.Unregister(this);
    }
}