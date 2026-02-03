using UnityEngine;

[CreateAssetMenu(
    fileName = "HullMaterial",
    menuName = "Boat/Hull Material",
    order = 1
)]
public class HullMaterial : ScriptableObject
{
    [Header("Identity")]
    public string materialName = "Steel";

    [Header("Visuals")]
    public Sprite sprite;
    public Color color = Color.white;
    public string sortingLayer = "Default";
    public int sortingOrder = 0;

    [Header("Physical Properties")]
    public float density = 7850f;       // kg/m³ (steel-ish default)
    public float durability = 100f;     // HP before breach
    public float thickness = 0.2f;      // meters (used for damage / leaks)

    [Header("Fluid Behavior")]
    public bool blocksWater = true;
    public bool blocksAir = true;

    [Tooltip("How quickly this material leaks once damaged (0 = never leaks)")]
    public float leakRate = 0.0f;

    [Header("Gameplay Tweaks")]
    [Tooltip("Multiplier for buoyancy contribution (wood > steel)")]
    public float buoyancyMultiplier = 1f;
}
