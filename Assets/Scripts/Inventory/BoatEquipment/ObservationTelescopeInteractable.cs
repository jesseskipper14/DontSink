using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlaceableBoatEquipment), typeof(SkyClearanceRequirement))]
public sealed class ObservationTelescopeInteractable : MonoBehaviour,
    IInteractable, IUnsecureInteractable, IInteractPromptProvider, IInteractPromptActionProvider
{
    [SerializeField, Min(0.1f)] private float maxInteractDistance = 2f;
    [SerializeField] private int interactionPriority = 1100;
    [SerializeField, Min(1f)] private float minimumZoom = 1f;
    [SerializeField, Min(1f)] private float maximumZoom = 1.5f;
    [SerializeField, Min(0f)] private float zoomSpeed = 0.1f;
    [Header("Presentation")]
    [SerializeField, Range(0.5f, 1f)] private float fadeDuration = 0.75f;
    [Tooltip("Additional GameObject layers hidden only for this telescope's viewing camera. Boat-owned visuals are already included. Use dedicated NPC/building layers, not a broad sky/terrain layer.")]
    [SerializeField] private LayerMask additionalHiddenLayers = 0;
    [Tooltip("Additional sorting layers, such as WorldBuildings. These are separate from GameObject layers.")]
    [SerializeField, ObservationSortingLayer] private string[] additionalHiddenSortingLayers = new string[0];
    public int InteractionPriority => interactionPriority;
    public PlaceableBoatEquipment Equipment => GetComponent<PlaceableBoatEquipment>();
    public SkyClearanceRequirement Clearance => GetComponent<SkyClearanceRequirement>();
    public float MinimumZoom => Mathf.Max(1f, minimumZoom);
    public float MaximumZoom => Mathf.Max(MinimumZoom, maximumZoom);
    public float ZoomSpeed => zoomSpeed;
    public float FadeDuration => Mathf.Clamp(fadeDuration, 0.5f, 1f);
    public LayerMask AdditionalHiddenLayers => additionalHiddenLayers;
    public IReadOnlyList<string> AdditionalHiddenSortingLayers => additionalHiddenSortingLayers;

    public bool IsValidUser(GameObject actor) => isActiveAndEnabled && Equipment.IsActorOnBoat(actor) &&
        Vector2.Distance(actor.transform.position, transform.position) <= maxInteractDistance;
    public bool CanInteract(in InteractContext context) => IsValidUser(context.InteractorGO) &&
        (Equipment.IsDeployed || (GameplayAuthority.IsAuthoritative && Equipment.HasValidSupport()));
    public void Interact(in InteractContext context)
    {
        if (!CanInteract(context)) return;
        if (!Equipment.IsDeployed) { Equipment.TryDeploy(context.InteractorGO); return; }
        var session = context.InteractorGO.GetComponentInParent<BoatObservationPresentationController>();
        if (session == null)
        {
            session = context.InteractorGO.AddComponent<BoatObservationPresentationController>();
            context.InteractorGO.GetComponent<Interactor2D>()?.RefreshInteractionTargetFilters();
        }
        if (session.IsObserving(this)) session.ExitObservation();
        else session.TryEnter(this);
    }
    public bool CanUnsecure(in InteractContext context) => GameplayAuthority.IsAuthoritative &&
        Equipment.IsDeployed && IsValidUser(context.InteractorGO);
    public void Unsecure(in InteractContext context)
    {
        if (CanUnsecure(context)) Equipment.TryUnpin(context.InteractorGO);
    }
    public string GetPromptVerb(in InteractContext context) => Equipment.IsDeployed ? "Observe sky" : "Deploy telescope";
    public Transform GetPromptAnchor() => transform;
    public void GetPromptActions(in InteractContext context, List<PromptAction> actions)
    {
        var session = context.InteractorGO != null ? context.InteractorGO.GetComponentInParent<BoatObservationPresentationController>() : null;
        if (CanInteract(context)) actions.Add(new PromptAction(session != null && session.IsObserving(this)
            ? "E: Exit observation" : Equipment.IsDeployed ? "E: Observe sky" : "E: Deploy telescope"));
        if (CanUnsecure(context)) actions.Add(new PromptAction("X: Unpin telescope"));
    }
}
