using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class MarketSellZone : MonoBehaviour
{
    public Collider2D ZoneCollider { get; private set; }

    private void Awake()
    {
        CacheCollider();
    }

    private void OnValidate()
    {
        CacheCollider();
    }

    private void CacheCollider()
    {
        ZoneCollider = GetComponent<Collider2D>();

        if (ZoneCollider != null)
            ZoneCollider.isTrigger = true;
    }
}