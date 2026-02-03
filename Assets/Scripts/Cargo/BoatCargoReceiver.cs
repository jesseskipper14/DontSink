using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BoatCargoReceiver : MonoBehaviour
{
    private Boat boat;

    void Awake()
    {
        boat = GetComponent<Boat>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Cargo cargo = other.GetComponent<Cargo>();
        if (cargo == null) return;

        if (cargo.isAttachedToBoat) return;

        boat.AttachCargo(cargo);
    }
}
