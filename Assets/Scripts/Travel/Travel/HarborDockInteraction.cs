using System.Collections.Generic;
using UnityEngine;

/// <summary>Ambient berth action. Ordinary hovered interactions retain priority; no extra E listener.</summary>
public sealed class HarborDockInteraction : MonoBehaviour, IInteractable, IInteractPromptProvider
{
    public BoatSceneController Controller;
    private static readonly List<HarborDockInteraction> Active = new();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => Active.Clear();
    private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
    private void OnDisable() => Active.Remove(this);
    public int InteractionPriority => -100;
    public bool CanInteract(in InteractContext context) => Controller != null &&
        Controller.CanDockAtHarbor(Controller.CurrentHarborNodeId, context.InteractorGO);
    public void Interact(in InteractContext context)
    {
        if (CanInteract(context) && SceneTransitionController.I != null)
            SceneTransitionController.I.TryDockAtHarbor(Controller.CurrentHarborNodeId, context.InteractorGO);
    }
    public string GetPromptVerb(in InteractContext context) => Controller != null ? Controller.HarborDockVerb : "Dock";
    public Transform GetPromptAnchor() => null;
    public static bool TryGet(in InteractContext context, out IInteractable action)
    {
        action = null;
        if (InteractionInputBlocker.IsBlocked) return false;
        foreach (var candidate in Active)
            if (candidate != null && candidate.isActiveAndEnabled && candidate.CanInteract(context))
            { action = candidate; return true; }
        return false;
    }
}
