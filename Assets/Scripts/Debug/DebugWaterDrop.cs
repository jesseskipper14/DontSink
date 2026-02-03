//using Unity.VisualScripting;
//using UnityEngine;

//public class DebugWaterDrop : MonoBehaviour
//{
//    public float volume = 1f;
//    public float fallSpeed = 6f;
//    public Boat boat;
//    public float lifetime = 5f;

//    void Awake()
//    {
//        // Find the active boat once
//        boat = Object.FindAnyObjectByType<Boat>();

//        if (boat == null)
//        {
//            Debug.LogError("DebugWaterDrop: No Boat found in scene.");
//        }
//    }

//    void Update()
//    {
//        transform.position += Vector3.down * fallSpeed * Time.deltaTime;

//        // Check against all compartments
//        foreach (var c in boat.Compartments)
//        {
//            if (c == null) continue;

//            // Only flood if droplet is above compartment bottom
//            if (transform.position.y < c.WorldTopY &&
//                c.ContainsWorldPoint(transform.position))
//            {
//                c.AddWater(volume);
//                Destroy(gameObject);
//                return;
//            }
//        }

//        // Optional: destroy if too low
//        if (transform.position.y < -50f)
//        {
//            Destroy(gameObject);
//        }

//        lifetime -= Time.deltaTime;

//        if (lifetime <= 0f)
//        {
//            Destroy(gameObject);
//        }
//    }
//}
