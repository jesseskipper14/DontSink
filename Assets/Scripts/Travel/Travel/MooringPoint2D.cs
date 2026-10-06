using UnityEngine;

/// <summary>An authored attachment point; its sprite and transform remain designer-owned.</summary>
[DisallowMultipleComponent]
public sealed class MooringPoint2D : MonoBehaviour
{
    public enum Role { Aft, Forward }
    public Role role;
    [Tooltip("Dock points only: fixed rope length. Zero measures distance plus slack when tying.")]
    [Min(0f)] public float ropeLength;
    public Boat Boat => GetComponentInParent<Boat>();

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = role == Role.Aft ? Color.cyan : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, .15f);
    }
}
