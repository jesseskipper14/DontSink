using UnityEngine;

[CreateAssetMenu(
    fileName = "ToolCapabilityDefinition",
    menuName = "Items/Tool Capability")]
public sealed class ToolCapabilityDefinition : ScriptableObject
{
    [Header("Identity")]
    public string stableId;

    public string displayName;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = name;
    }
}