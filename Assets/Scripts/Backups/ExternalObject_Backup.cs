
///*
// * ExternalObject.cs – Current State

//Purpose:

//Generic script for external objects (like the background or water elements) that need to follow the boat’s motion.

//Key Features:

//Scrolling / Following:

//scrollDelta = -boat.velocity * dt ensures the object moves opposite the boat’s velocity, giving a world-relative feel.

//Works for background objects, visual water elements, or debris.

//Optional rotation (commented out):

//Can rotate objects based on boat.angularVelocity if needed.

//External forces / damping:

//velocity *= 0.99f – simple drag to slow objects over time.

//External forces are integrated into position via transform.position += velocity * dt.

//Notes:

//Decouples visuals / non-player objects from full physics simulation.

//Useful for moving backgrounds, floating debris, or other secondary objects.
// */

//using UnityEngine;

//public class ExternalObject : MonoBehaviour
//{
//    // ========================
//    // References
//    // ========================

//    public Boat boat;

//    // ========================
//    // State
//    // ========================

//    public Vector2 velocity;

//    // ========================
//    // Unity Lifecycle
//    // ========================

//    // In ExternalObject.cs
//    protected virtual void FixedUpdate()
//    {
//        if (boat == null)
//            return;

//        float dt = Time.fixedDeltaTime;

//        // Move opposite the boat's velocity to maintain world-relative motion
//        Vector2 scrollDelta = -boat.velocity * dt;
//        transform.position += (Vector3)scrollDelta;

//        // Apply external forces (e.g., drag)
//        velocity *= 0.99f;
//        transform.position += (Vector3)(velocity * dt);
//    }

//}

