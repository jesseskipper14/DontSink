using UnityEngine;

public interface IInteractPromptProvider
{
    /// Example: "Pilot", "Board", "Chart", "Trade"
    string GetPromptVerb(in InteractContext context);

    /// Optional: where the prompt should hover near. Return null to use transform.position.
    Transform GetPromptAnchor();
}