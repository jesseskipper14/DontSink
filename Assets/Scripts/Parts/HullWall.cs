using UnityEngine;

/// <summary>
/// Basic hull wall. Mostly inherits from BoatPart.
/// Can add wall-specific properties like thickness or breach behavior.
/// </summary>
public class HullWall : BoatPart
{
    [Header("Wall Properties")]
    public float thicknessOverride = -1f; // -1 = use material

    public float Thickness => thicknessOverride > 0f ? thicknessOverride : (material != null ? material.thickness : 0.2f);

    // Add any HullWall-specific methods here (damage, breach, etc.)
}
