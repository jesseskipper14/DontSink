using System;
using UnityEngine;

[Obsolete("Old Cargo is deprecated. Use ItemInstance/WorldItem/BoatOwnedItem and modern CargoManifest paths instead.")]
public sealed class Cargo : MonoBehaviour
{
    [Header("Deprecated Cargo Properties")]
    public Vector2 size = Vector2.one;
    public float mass = 2.0f;

    [Header("Deprecated Placement")]
    public Compartment compartment;

    [Header("Deprecated Runtime")]
    public bool isAttachedToBoat = false;
    public Boat attachedBoat;
    public Vector2 localPositionOnBoat;

    public float WorldX => transform.position.x;
    public float WorldY => transform.position.y;

#if UNITY_EDITOR
    private void OnValidate()
    {
        Debug.LogWarning(
            "[Cargo] This component is deprecated. Remove it from prefabs and use modern item/cargo persistence instead.",
            this);
    }
#endif

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, size);
    }
}