using UnityEngine;

/// <summary>An authored rope attachment point; its sprite and transform remain designer-owned.</summary>
[DisallowMultipleComponent]
public sealed class MooringPoint2D : MonoBehaviour
{
    public Boat Boat => GetComponentInParent<Boat>();

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, .15f);
    }
}
