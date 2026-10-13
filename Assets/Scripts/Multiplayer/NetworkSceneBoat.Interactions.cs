using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public sealed partial class NetworkSceneBoat
{
    private sealed class InteractionHold
    {
        public int Target;
        public double Started, LastSeen;
        public bool Fired;
        public float Duration;
    }
    private MonoBehaviour[] interactionTargets;
    private Collider2D[][] interactionColliders;
    private readonly NetworkVariable<uint> interactionRevision = new(1);
    private readonly NetworkVariable<ulong> openHatches = new();
    private HatchRuntime[] hatches;
    private readonly Dictionary<ulong, InteractionHold> interactionHolds = new();
    private readonly Dictionary<ulong, NetworkInteractionRequestGate> interactionRequests = new();
    private uint interactionSequence;
    private double nextInteraction;
    private int localHoldTarget = -1;
    private float localHoldElapsed;
    private bool localHoldFired;
    private bool previousInteractHeld;

    private void BindInteractions()
    {
        interactionTargets = boat.GetComponentsInChildren<MonoBehaviour>(true).Where(c =>
            c is BoatBoardingInteractable || c is DoorInteractable || c is HatchInteractable ||
            c is LadderZone || c is PilotChairInteractable || c is HardpointInteractable).ToArray();
        interactionColliders = interactionTargets.Select(t => t.GetComponentsInChildren<Collider2D>(true)).ToArray();
        hatches = boat.GetComponentsInChildren<HatchRuntime>(true);
        if (hatches.Length > 64 || interactionTargets.Length > 512)
            throw new InvalidOperationException("Boat interaction snapshot capacity exceeded.");
    }

    // Replica rigidbodies are not simulated, so their colliders are absent from
    // Physics2D overlap queries. Pick the authored box/circle in rendered space.
    public static Vector2 ClosestInteractionPoint(Collider2D collider, Vector2 point)
    {
        if (collider.attachedRigidbody == null || collider.attachedRigidbody.simulated)
            return PhysicsFrame2D.ClosestRenderedPoint(collider, point);
        Vector2 local = collider.transform.InverseTransformPoint(point);
        if (collider is BoxCollider2D box)
        {
            Vector2 half = box.size * .5f;
            local = box.offset + new Vector2(Mathf.Clamp(local.x - box.offset.x, -half.x, half.x),
                Mathf.Clamp(local.y - box.offset.y, -half.y, half.y));
        }
        else if (collider is CircleCollider2D circle)
            local = circle.offset + Vector2.ClampMagnitude(local - circle.offset, circle.radius);
        else return new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        return collider.transform.TransformPoint(local);
    }

    public bool TryResolveInteractionHover(in InteractContext context, out InteractionHoverTarget hover)
    {
        hover = default;
        if (interactionTargets == null || !context.HasAimWorld) return false;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < interactionTargets.Length; i++)
        {
            var component = interactionTargets[i];
            if (component == null || !component.gameObject.activeInHierarchy ||
                (!component.enabled && component is not PilotChairInteractable)) continue;
            var target = (IInteractable)component;
            foreach (var collider in interactionColliders[i])
            {
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy ||
                    (target is IInteractionColliderScope scope && !scope.AllowsInteractionCollider(collider)) ||
                    Vector2.Distance(context.AimWorld, ClosestInteractionPoint(collider, context.AimWorld)) > .08f) continue;
                float distance = Vector2.Distance(context.Origin, ClosestInteractionPoint(collider, context.Origin));
                if (distance > 4) continue;
                float score = target.InteractionPriority * 10 - distance;
                if (score <= bestScore) continue;
                var candidate = new InteractionHoverTarget(collider, component, target, target as IPickupInteractable, null, null,
                    target as IInteractPromptProvider, target as IPickupPromptProvider, target as IInteractPromptActionProvider,
                    target as IInteractionLabelProvider, (IInteractionDetailProvider[])null, target as IInteractionRangeProvider);
                var interactor = context.InteractorGO.GetComponent<Interactor2D>();
                if (interactor == null || !interactor.PassesHoverSelectionPolicy(candidate, context)) continue;
                bestScore = score; hover = candidate;
            }
        }
        return TryLooseHover(context, ref hover);
    }

    public void ProcessInteraction(Interactor2D interactor, in InteractContext context, InteractionIntent intent)
    {
        var actor = interactor.GetComponent<NetworkPlayerActor>();
        if (actor == null || !actor.IsOwner || !CameraManager.HasGameplayInput(actor)) return;
        interactor.TryResolveBest(context, out var target);
        int index = target is MonoBehaviour component && interactionTargets != null ? Array.IndexOf(interactionTargets, component) : -1;
        if (index != localHoldTarget || !intent.InteractHeld)
        { localHoldTarget = index; localHoldElapsed = 0; if (!intent.InteractHeld) localHoldFired = false; }
        float duration = target is IHoldInteractable hold ? hold.GetInteractionHoldDuration(context) : 0;
        if (intent.InteractHeld && duration > 0 && target.CanInteract(context) && !localHoldFired)
        { localHoldElapsed += Time.deltaTime; localHoldFired = localHoldElapsed >= duration; }
        interactor.PresentNetworkHold(intent.InteractHeld && duration > 0 ? target : null, localHoldElapsed, duration, localHoldFired);
        if (target is PilotChairInteractable chair)
        {
            if (intent.InteractPressed && OwnedStation(actor.OwnerClientId) < 0)
                RequestStation(NetworkStationAction.Sit, Array.IndexOf(chairs, chair));
            index = -1;
        }
        if (target is HardpointInteractable hardpoint)
        {
            if (intent.InteractPressed && (!hardpoint.TargetHardpoint.HasInstalledModule || SupportsModule(hardpoint) || SupportsStorage(hardpoint))) hardpoint.Interact(context); // Owner-local panel only; all panel mutations use RPCs.
            index = -1;
        }
        if (intent.LinkPressed && target is MonoBehaviour linkTarget) RequestHelmLink(linkTarget);
        ProcessModulePickup(interactor, context, intent);
        ProcessLoosePickup(interactor, context, intent);
        bool released = previousInteractHeld && !intent.InteractHeld;
        previousInteractHeld = intent.InteractHeld;
        if (intent.InteractPressed || released || (intent.InteractHeld && Time.realtimeSinceStartupAsDouble >= nextInteraction))
        {
            RequestInteraction(index, intent.InteractHeld, intent.InteractPressed);
            nextInteraction = Time.realtimeSinceStartupAsDouble + .12;
        }
    }

    public int InteractionTargetCount => interactionTargets?.Length ?? 0;
    public MonoBehaviour InteractionTarget(int index) => interactionTargets[index];
    public void RequestInteraction(int index, bool held, bool pressed)
    {
        var session = DontSinkNetworkSession.Instance;
        if (!IsSpawned || !session.HasLocalParticipant) return;
        uint id = ++interactionSequence; if (id == 0) id = ++interactionSequence;
        SubmitInteractionRpc(id, new FixedString64Bytes(session.LocalCharacterIdentity), interactionRevision.Value, index, held, pressed);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone, Delivery = RpcDelivery.Reliable)]
    public void SubmitInteractionRpc(uint id, FixedString64Bytes character, uint revision, int index, bool held, bool pressed, RpcParams rpc = default)
    {
        ulong sender = rpc.Receive.SenderClientId;
        var session = DontSinkNetworkSession.Instance;
        if (!session.Registry.TryResolveConnectedSender(sender, out var participant) ||
            !session.TryGetParticipantActor(sender, out var actor) || actor.OwnerClientId != sender ||
            actor.CharacterIdentity != participant.CharacterIdentity || character.ToString() != participant.CharacterIdentity ||
            !session.GameplayScene.IsParticipantReady(sender)) return;
        if (!interactionRequests.TryGetValue(sender, out var gate)) interactionRequests.Add(sender, gate = new NetworkInteractionRequestGate());
        if (gate.Begin(id, Time.realtimeSinceStartupAsDouble) != NetworkInteractionRequestDisposition.New) return;
        if (!held && !pressed) { interactionHolds.Remove(sender); gate.Complete(true, "Released."); return; }
        if (index == -1)
        {
            if (interactionHolds.TryGetValue(sender, out var cancelled)) { cancelled.Target = -1; cancelled.Started = Time.realtimeSinceStartupAsDouble; }
            gate.Complete(false, "No supported target."); return;
        }
        if (revision != interactionRevision.Value || index < 0 || index >= interactionTargets.Length ||
            interactionTargets[index] is HardpointInteractable || SeatFor(actor) != null)
        { gate.Complete(false, "Interaction changed or unavailable."); return; }
        if (!TryInteractionContext(actor, index, out var context))
        {
            // Keep the completion latch until the physical key is released,
            // even if boarding changes access or the cursor leaves this target.
            if (interactionHolds.TryGetValue(sender, out var cancelled))
            { cancelled.Target = -1; cancelled.Started = Time.realtimeSinceStartupAsDouble; }
            gate.Complete(false, "Move closer or board the boat."); return;
        }
        var target = (IInteractable)interactionTargets[index];
        if (!interactionHolds.TryGetValue(sender, out var hold))
            interactionHolds.Add(sender, hold = new InteractionHold { Target = -1 });
        if (hold.Target != index)
        {
            hold.Target = index; hold.Started = Time.realtimeSinceStartupAsDouble;
            hold.Duration = target is IHoldInteractable timed ? timed.GetInteractionHoldDuration(context) : 0;
        }
        hold.LastSeen = Time.realtimeSinceStartupAsDouble;
        if (!hold.Fired && hold.Duration <= 0 && pressed) ExecuteInteraction(actor, hold, context);
        if (!held) interactionHolds.Remove(sender);
        gate.Complete(true, "Accepted.");
    }

    private bool TryInteractionContext(NetworkPlayerActor actor, int index, out InteractContext context)
    {
        context = new InteractContext(actor.gameObject, actor.transform, actor.AuthoritativePosition, Vector2.right);
        var component = interactionTargets[index];
        if (component == null || !component.isActiveAndEnabled) return false;
        float distance = float.PositiveInfinity;
        foreach (var collider in interactionColliders[index])
            if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
                distance = Mathf.Min(distance, Vector2.Distance(context.Origin, ClosestInteractionPoint(collider, context.Origin)));
        return distance <= 1.75f && ((IInteractable)component).CanInteract(context);
    }
    private void ExecuteInteraction(NetworkPlayerActor actor, InteractionHold hold, in InteractContext context)
    {
        ((IInteractable)interactionTargets[hold.Target]).Interact(context);
        hold.Fired = true; interactionRevision.Value++;
    }
    private void TickInteractions()
    {
        if (interactionTargets == null) return;
        double now = Time.realtimeSinceStartupAsDouble;
        foreach (var pair in interactionHolds)
        {
            var hold = pair.Value;
            if (hold.Fired || hold.Target < 0) continue;
            if (now - hold.LastSeen > .3)
            { hold.Started = now; continue; }
            if (!DontSinkNetworkSession.Instance.TryGetParticipantActor(pair.Key, out var actor) ||
                SeatFor(actor) != null || !TryInteractionContext(actor, hold.Target, out var context))
            { hold.Started = now; continue; }
            if (hold.Duration > 0 && now - hold.Started >= hold.Duration) ExecuteInteraction(actor, hold, context);
        }
        ulong bits = 0;
        for (int i = 0; i < hatches.Length; i++) if (hatches[i].IsOpen) bits |= 1UL << i;
        openHatches.Value = bits;
    }
    private void ApplyInteractionState()
    {
        for (int i = 0; i < hatches.Length; i++)
        {
            bool open = (openHatches.Value & (1UL << i)) != 0;
            if (hatches[i].IsOpen != open) hatches[i].RestoreOpenState(open);
        }
    }
}
