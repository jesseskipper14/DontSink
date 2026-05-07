using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BoatItemContainmentZone : MonoBehaviour
{
    [SerializeField] private Boat boat;
    [SerializeField] private Collider2D containmentCollider;

    private readonly HashSet<BoatOwnedItem> _inside = new();

    public Boat Boat => boat;

    private void Awake()
    {
        if (boat == null)
            boat = GetComponentInParent<Boat>();

        if (containmentCollider == null)
            containmentCollider = GetComponent<Collider2D>();

        if (containmentCollider != null)
            containmentCollider.isTrigger = true;
    }

    public bool Contains(BoatOwnedItem item)
    {
        return item != null && _inside.Contains(item);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        BoatOwnedItem item = other.GetComponentInParent<BoatOwnedItem>();
        if (item == null)
            return;

        if (boat == null)
            return;

        if (item.OwningBoatInstanceId == boat.BoatInstanceId)
            _inside.Add(item);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        BoatOwnedItem item = other.GetComponentInParent<BoatOwnedItem>();
        if (item == null)
            return;

        _inside.Remove(item);
    }
}