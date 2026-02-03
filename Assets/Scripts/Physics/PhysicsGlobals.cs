using UnityEngine;

[CreateAssetMenu(fileName = "PhysicsGlobals", menuName = "Physics/Physics Globals")]
public class PhysicsGlobals : ScriptableObject
{
    [Header("Gravity")]
    public float Gravity = 9.8f;

    [Header("Water Properties")]
    public float WaterDensity = 1.0f;

    [Header("Sloshing")]
    public float SloshStiffness = 8f;
    public float SloshDamping = 2f;

    [Header("Flow")]
    public float DefaultFlowRate = 0.5f;

    [Header("Water Drag")]
    public float WaterVerticalDrag = 1.0f;
    public float WaterHorizontalDrag = 1.0f;
    public float WaterAngularDrag = 0.0f;

    [Header("Velocity Thresholds")]
    public float MinRelativeVelocityFactor = 0.01f;
    public float MinRelativeVelocityAbsolute = 0.1f;

    [Header("Buoyancy & Waves")]
    public float RightingStiffness = 2.0f;
    public float AngularDamping = 4.0f;
    public float MaxBuoyantAcceleration = 20.0f;
    public float MinBuoyantAcceleration = 12.0f;
    //public float MaxWaveVelocity = 10.0f;
    public float SurfaceInteractionDepth = 2.0f;
}
