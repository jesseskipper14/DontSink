using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlaceableBoatEquipment), typeof(SkyClearanceRequirement))]
public sealed class ChartingInstrumentInteractable : MonoBehaviour, IInteractable, IToggleInteractable,
    IUnsecureInteractable, IInteractPromptProvider, IInteractPromptActionProvider
{
    [SerializeField, Min(0.1f)] private float maxInteractDistance = 2f;
    [SerializeField] private int interactionPriority = 1100;
    [Tooltip("Optional scene runner override. Prefabs normally leave this empty; exactly one scene runner must exist.")]
    [SerializeField] private CelestialObservationOverlayRunner runner;
    private GameObject operatorActor;
    private CelestialObservationOverlayRunner activeRunner;
    public ItemInstance Item => GetComponent<WorldItem>().Instance;
    public PlaceableBoatEquipment Equipment => GetComponent<PlaceableBoatEquipment>();
    public SkyClearanceRequirement Clearance => GetComponent<SkyClearanceRequirement>();
    public int InteractionPriority => interactionPriority;
    public bool IsInUse => operatorActor != null;
    public CelestialObservation PendingObservation => Item?.ChartingInstrument?.HasPendingObservation == true
        ? Item.ChartingInstrument.pendingObservation : null;

    public bool IsValidUser(GameObject actor) => isActiveAndEnabled && actor != null && Item != null &&
        Item.ChartingInstrument?.invalidated != true && Equipment.IsActorOnBoat(actor) &&
        Vector2.Distance(actor.transform.position, transform.position) <= maxInteractDistance;
    public bool CanInteract(in InteractContext context) => IsValidUser(context.InteractorGO);
    public void Interact(in InteractContext context)
    {
        if (!GameplayAuthority.IsAuthoritative || !CanInteract(context)) return;
        if (!ChartingInstrumentReplacement.IsValidInstrument(Equipment.OwningBoat, Item))
        { GameMessageService.PostWarning("This boat already has a valid charting instrument."); return; }
        if (!Equipment.IsDeployed)
        {
            if (!Equipment.TryDeploy(context.InteractorGO)) GameMessageService.PostWarning("Place the instrument upright on a boat surface first.");
            return;
        }
        if (IsInUse) { GameMessageService.PostWarning("Charting instrument in use."); return; }
        if (!Clearance.HasClearance()) { GameMessageService.PostWarning(SkyClearanceRequirement.BlockedMessage); return; }
        var chosen = runner;
        if (chosen == null)
        {
            var runners = FindObjectsByType<CelestialObservationOverlayRunner>(FindObjectsSortMode.None);
            if (runners.Length == 1) chosen = runners[0];
        }
        if (chosen == null) { GameMessageService.PostWarning("Charting instrument runner is unavailable or ambiguous."); return; }
        if (!TryAcquire(context.InteractorGO, chosen)) return;
        if (!chosen.TryOpenObservationFor(context.InteractorGO, this)) ReleaseOperator();
    }
    public bool TryAcquire(GameObject actor, CelestialObservationOverlayRunner session)
    {
        if (!GameplayAuthority.IsAuthoritative || session == null || IsInUse || !IsValidUser(actor) ||
            !Equipment.IsDeployed || !Clearance.HasClearance() ||
            !ChartingInstrumentReplacement.IsValidInstrument(Equipment.OwningBoat, Item)) return false;
        var state = Item.ChartingInstrument?.Copy() ?? new ChartingInstrumentState();
        state.boatInstanceId = Equipment.OwningBoat.BoatInstanceId;
        state.revision++;
        Item.SetChartingInstrumentState(state);
        operatorActor = actor;
        activeRunner = session;
        return true;
    }
    public bool IsOperatedBy(GameObject actor) => actor != null && operatorActor == actor;
    public bool SessionIsValid(GameObject actor) => IsOperatedBy(actor) && IsValidUser(actor) && Equipment.IsDeployed;
    public bool CanCommit(GameObject actor, CelestialObservation observation) => SessionIsValid(actor) &&
        GameplayAuthority.IsAuthoritative && Clearance.HasClearance() && observation != null &&
        PendingObservation?.observationId == observation.observationId &&
        ChartingInstrumentReplacement.IsValidInstrument(Equipment.OwningBoat, Item);
    public void StoreCheckpoint(CelestialObservation observation)
    {
        if (!GameplayAuthority.IsAuthoritative || !SessionIsValid(operatorActor)) return;
        var state = Item.ChartingInstrument?.Copy() ?? new ChartingInstrumentState();
        state.pendingObservation = observation;
        state.revision++;
        Item.SetChartingInstrumentState(state);
    }
    public void ClearPendingObservation()
    {
        if (!GameplayAuthority.IsAuthoritative || Item == null) return;
        var state = Item.ChartingInstrument?.Copy() ?? new ChartingInstrumentState();
        state.pendingObservation = null;
        state.revision++;
        Item.SetChartingInstrumentState(state);
    }
    public void ReleaseOperator() { operatorActor = null; activeRunner = null; }
    private void Update()
    {
        // Remember the boat on the item itself, including before first use. This lets replacement
        // retire a former boat item after its normal escape tracker clears physical ownership.
        if (!GameplayAuthority.IsAuthoritative || Item == null || Equipment.OwningBoat == null ||
            Item.ChartingInstrument?.invalidated == true) return;
        if (Item.ChartingInstrument?.boatInstanceId == Equipment.OwningBoat.BoatInstanceId) return;
        var state = Item.ChartingInstrument?.Copy() ?? new ChartingInstrumentState();
        state.boatInstanceId = Equipment.OwningBoat.BoatInstanceId;
        state.revision++;
        Item.SetChartingInstrumentState(state);
    }
    private void OnDisable()
    {
        if (activeRunner != null) activeRunner.InterruptInstrument(this, "Charting instrument unavailable.");
        ReleaseOperator();
    }
    // T uses the existing portable-container overlay. Occupancy deliberately does not gate storage.
    public bool CanToggle(in InteractContext context) => IsValidUser(context.InteractorGO) &&
        Item.IsContainer && Item.ContainerState != null;
    public void Toggle(in InteractContext context)
    {
        if (!CanToggle(context)) return;
        var overlay = context.InteractorGO.GetComponentInChildren<ExternalContainerOverlayUI>(true) ??
            context.InteractorGO.GetComponentInParent<ExternalContainerOverlayUI>(true);
        if (overlay == null)
        {
            var overlays = FindObjectsByType<ExternalContainerOverlayUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (overlays.Length == 1) overlay = overlays[0];
        }
        if (overlay == null) { GameMessageService.PostWarning("Paper storage UI is unavailable or ambiguous."); return; }
        if (overlay.IsOpen && ReferenceEquals(overlay.CurrentContainer, Item)) overlay.Close();
        else overlay.Open("Charting Paper", Item, transform, maxInteractDistance + 0.25f);
    }
    public bool CanUnsecure(in InteractContext context) => GameplayAuthority.IsAuthoritative &&
        Equipment.IsDeployed && IsValidUser(context.InteractorGO) && !IsInUse;
    public void Unsecure(in InteractContext context) { if (CanUnsecure(context)) Equipment.TryUnpin(context.InteractorGO); }
    public string GetPromptVerb(in InteractContext context) => !Equipment.IsDeployed ? "Deploy instrument" :
        IsInUse ? "Charting instrument in use" : PendingObservation != null ? "Resume chart recording" : "Chart sky";
    public Transform GetPromptAnchor() => transform;
    public void GetPromptActions(in InteractContext context, List<PromptAction> actions)
    {
        if (CanInteract(context)) actions.Add(new PromptAction("E: " + GetPromptVerb(context)));
        if (CanToggle(context)) actions.Add(new PromptAction("T: Paper storage"));
        if (CanUnsecure(context)) actions.Add(new PromptAction("X: Unpin instrument"));
    }
}
