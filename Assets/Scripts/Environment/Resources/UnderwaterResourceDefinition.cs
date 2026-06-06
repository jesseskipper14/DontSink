using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "UnderwaterResourceDefinition",
    menuName = "Underwater/Resource Definition")]
public sealed class UnderwaterResourceDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable ID used by spawning, save logic later, and modding.")]
    public string stableId;

    public string displayName;

    [Header("Behavior")]
    public UnderwaterResourceCategory category = UnderwaterResourceCategory.Collectable;

    [Tooltip("Legacy/simple enum requirement. Prefer requiredToolCapability for new tool logic.")]
    public UnderwaterResourceToolKind requiredTool = UnderwaterResourceToolKind.None;

    [Tooltip("Preferred requirement path. Example: Drill capability. Any equipped item matching this capability can harvest.")]
    public ToolCapabilityDefinition requiredToolCapability;

    [Min(0f)]
    public float harvestSeconds = 0f;

    [Min(1)]
    public int charges = 1;

    [Header("Spawn")]
    [Tooltip("Baseline weight. This applies even when no POI is nearby.")]
    [Min(0f)]
    public float baselineWeight = 1f;

    [Tooltip("Minimum depth below surface, in world units.")]
    [Min(0f)]
    public float minDepth = 0.5f;

    [Tooltip("Maximum depth below surface, in world units.")]
    [Min(0f)]
    public float maxDepth = 8f;

    [Header("Ground Placement")]
    public bool alignToGeneratedGround = true;

    [Tooltip("How far above the generated ground this resource sits.")]
    [Min(0f)] public float groundClearance = 0.12f;

    [Tooltip("Small vertical randomness after ground alignment.")]
    [Min(0f)] public float verticalJitter = 0.05f;

    [Tooltip("Reject placement on slopes steeper than this.")]
    [Range(0f, 89f)] public float maxGroundSlopeDegrees = 70f;

    [Tooltip("Loose tags used by POI/biome modifiers. Examples: reef, wreck, algae, salvage, ore.")]
    public List<string> spawnTags = new();

    [Header("Yield")]
    public List<UnderwaterResourceYield> yields = new();

    [Header("Scene Presentation")]
    public GameObject scenePrefab;

    [Tooltip("Used only if scenePrefab is missing a SpriteRenderer or if the spawner creates a fallback object.")]
    public Sprite fallbackSprite;

    public bool HasTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        for (int i = 0; i < spawnTags.Count; i++)
        {
            if (string.Equals(spawnTags[i], tag, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public bool RequiresTool()
    {
        return requiredToolCapability != null ||
               requiredTool != UnderwaterResourceToolKind.None;
    }

    public string GetRequiredToolDisplayName()
    {
        if (requiredToolCapability != null)
        {
            string capDisplay =
                TryReadStringPropertyOrField(requiredToolCapability, "displayName") ??
                TryReadStringPropertyOrField(requiredToolCapability, "DisplayName");

            if (!string.IsNullOrWhiteSpace(capDisplay))
                return capDisplay;

            if (!string.IsNullOrWhiteSpace(requiredToolCapability.name))
                return requiredToolCapability.name;
        }

        return requiredTool switch
        {
            UnderwaterResourceToolKind.SimpleDrill => "Drill",
            UnderwaterResourceToolKind.Crane => "Crane",
            UnderwaterResourceToolKind.None => "Tool",
            _ => requiredTool.ToString()
        };
    }

    private void OnValidate()
    {
        if (maxDepth < minDepth)
            maxDepth = minDepth;

        if (charges < 1)
            charges = 1;

        if (category == UnderwaterResourceCategory.Collectable)
        {
            requiredTool = UnderwaterResourceToolKind.None;
            requiredToolCapability = null;
            harvestSeconds = Mathf.Max(0f, harvestSeconds);
        }

        if (category == UnderwaterResourceCategory.Extractable && harvestSeconds <= 0f)
            harvestSeconds = 2f;

        if (category == UnderwaterResourceCategory.Craneable)
        {
            requiredTool = UnderwaterResourceToolKind.Crane;
            requiredToolCapability = null;
        }
    }

    private static string TryReadStringPropertyOrField(object obj, string memberName)
    {
        if (obj == null || string.IsNullOrWhiteSpace(memberName))
            return null;

        System.Type type = obj.GetType();

        System.Reflection.PropertyInfo prop = type.GetProperty(
            memberName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);

        if (prop != null && prop.PropertyType == typeof(string))
            return prop.GetValue(obj) as string;

        System.Reflection.FieldInfo field = type.GetField(
            memberName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);

        if (field != null && field.FieldType == typeof(string))
            return field.GetValue(obj) as string;

        return null;
    }
}