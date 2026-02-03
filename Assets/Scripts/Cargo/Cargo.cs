using UnityEngine;

public class Cargo : MonoBehaviour
{
    [Header("Cargo Properties")]
    public Vector2 size = Vector2.one;
    public float mass = 2.0f;

    [Header("Placement")]
    public Compartment compartment;   // optional

    Boat boat;

    void Awake()
    {
        boat = GetComponentInParent<Boat>();
    }

    public float WorldX => transform.position.x;
    public float WorldY => transform.position.y;

    [Header("Runtime")]
    public bool isAttachedToBoat = false;
    public Boat attachedBoat;
    public Vector2 localPositionOnBoat;

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, size);
    }
}

