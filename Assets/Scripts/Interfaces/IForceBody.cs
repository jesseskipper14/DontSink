using UnityEngine;

public interface IForceBody
{
    Rigidbody2D rb { get; }

    // ========================
    // Basic State
    // ========================

    Vector2 Position { get; }
    float Mass { get; }
    float MomentOfInertia { get; }

    // ========================
    // Dimensions (for drag / buoyancy)
    // ========================

    float Width { get; }
    float Height { get; }
    float Volume { get; }

    // ========================
    // Force API
    // ========================

    void AddForce(Vector2 force);
    void AddTorque(float torque);

}
