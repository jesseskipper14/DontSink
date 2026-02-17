using UnityEngine;

[CreateAssetMenu(menuName = "WorldMap/Trade/Resource Def", fileName = "ResourceDef_")]
public sealed class ResourceDef : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable, global string ID. Never change after shipping.")]
    public string itemId = "fish";

    public string displayName = "Fish";

    [Header("Economy")]
    [Min(1)] public int basePrice = 5;

    [Tooltip("How 'swingy' this item is under pressure. 0 = stable, 1 = very volatile.")]
    [Range(0f, 1f)] public float volatility01 = 0.25f;

    [Tooltip("If true, treat as exotic (rarer, wider price bands, more prosperity-linked).")]
    public bool isExotic = false;

    [Header("Tags")]
    public ResourceTag tags = ResourceTag.None;
}
