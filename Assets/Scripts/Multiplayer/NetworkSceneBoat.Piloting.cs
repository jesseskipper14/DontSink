using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public struct NetworkHelmOrders : INetworkSerializable, IEquatable<NetworkHelmOrders>
{
    public bool HasThrottle, HasRudder, Moored;
    public float Throttle, Rudder, ForwardSpeed, MaxRudder;
    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref HasThrottle); s.SerializeValue(ref HasRudder);
        s.SerializeValue(ref Moored);
        s.SerializeValue(ref Throttle); s.SerializeValue(ref Rudder);
        s.SerializeValue(ref ForwardSpeed); s.SerializeValue(ref MaxRudder);
    }
    public bool Equals(NetworkHelmOrders other) => HasThrottle == other.HasThrottle && HasRudder == other.HasRudder && Moored == other.Moored &&
        Throttle == other.Throttle && Rudder == other.Rudder && ForwardSpeed == other.ForwardSpeed && MaxRudder == other.MaxRudder;
}

// The existing cartridge stays local. Its orders and keyboard input are requests
// against the host-owned seat claim, never client propulsion/steering simulation.
public sealed partial class NetworkSceneBoat
{
    private sealed class HelmInput
    {
        public int Station;
        public uint Revision;
        public double Received;
        public BoatControlIntent Intent;
    }
    private readonly NetworkVariable<NetworkHelmOrders> helmOrders = new();
    private readonly Dictionary<ulong, HelmInput> helmInputs = new();
    private readonly Dictionary<ulong, NetworkInteractionRequestGate> helmRequests = new();
    private uint helmSequence;
    private double nextHelmInput, nextHelmOrders;
    private bool stopRequested, localPilotingOpen, localPilotingClosing;
    private PilotChairInteractable localPilotingChair;
    private PilotingOverlayRunner localPilotingRunner;
    public float? ThrottleOrder => helmOrders.Value.HasThrottle ? helmOrders.Value.Throttle : null;
    public float? RudderOrder => helmOrders.Value.HasRudder ? helmOrders.Value.Rudder : null;
    public float HelmForwardSpeed => helmOrders.Value.ForwardSpeed;
    public float HelmMaxRudder => helmOrders.Value.MaxRudder;
    public bool HelmRequestPending => stationPending != 0;

    public bool HasLocalHelmControl(PilotChairInteractable chair)
    {
        int index = chairs != null ? Array.IndexOf(chairs, chair) : -1;
        return IsSpawned && index >= 0 && index < stationState.Count && stationState[index].Occupied &&
            stationState[index].Owner == NetworkManager.LocalClientId && stationState[index].ControlsHelm;
    }
    public PilotChairInteractable.PilotingHelmStatus HelmStatus(PilotChairInteractable chair)
    {
        int index = chairs != null ? Array.IndexOf(chairs, chair) : -1;
        return index >= 0 && index < stationState.Count ? (PilotChairInteractable.PilotingHelmStatus)stationState[index].HelmStatus :
            PilotChairInteractable.PilotingHelmStatus.Unlinked;
    }
    public bool RequestHelmOrder(PilotChairInteractable chair, bool rudder, float value)
    {
        if (!HasLocalHelmControl(chair) || HelmRequestPending) return false;
        RequestStation(rudder ? NetworkStationAction.Rudder : NetworkStationAction.Throttle, Array.IndexOf(chairs, chair), value);
        return true;
    }
    private void PublishHelmOrders()
    {
        if (simulation == null || Time.realtimeSinceStartupAsDouble < nextHelmOrders) return;
        nextHelmOrders = Time.realtimeSinceStartupAsDouble + .1;
        helmOrders.Value = new NetworkHelmOrders { HasThrottle = simulation.ThrottleTarget.HasValue,
            HasRudder = simulation.RudderTargetFraction.HasValue, Throttle = simulation.ThrottleTarget ?? 0,
            Rudder = simulation.RudderTargetFraction ?? 0, ForwardSpeed = simulation.PhysicalForwardSpeed,
            MaxRudder = simulation.MaxRudderDegrees, Moored = NodeSceneMooring.IsLocked(boat) };
    }
    private void UpdatePilotingPresentation(NetworkPlayerActor actor)
    {
        int index = OwnedStation(actor.OwnerClientId);
        var desired = index >= 0 ? chairs[index] : null;
        if (localPilotingChair != desired)
        {
            CloseLocalPiloting("Left station");
            localPilotingClosing = false; localPilotingChair = desired;
            if (desired != null) desired.SetNetworkPresentationRequester(actor.gameObject);
        }
        if (desired == null) return;
        if (localPilotingOpen && (localPilotingRunner == null || !localPilotingRunner.IsOpenFor(desired)))
        { localPilotingOpen = false; localPilotingClosing = true; }
        if (localPilotingClosing)
        { if (stationPending == 0) RequestStation(NetworkStationAction.Stand, index); return; }
        if (HelmStatus(desired) == PilotChairInteractable.PilotingHelmStatus.Unlinked || localPilotingOpen) return;
        if (localPilotingRunner == null)
        {
            var runners = UnityEngine.Object.FindObjectsByType<PilotingOverlayRunner>(FindObjectsSortMode.None);
            if (runners.Length == 1) localPilotingRunner = runners[0];
        }
        if (localPilotingRunner != null) localPilotingOpen = localPilotingRunner.OpenForHelm(desired);
    }
    private void CloseLocalPiloting(string reason)
    {
        if (localPilotingChair != null)
        {
            localPilotingRunner?.CloseForHelm(localPilotingChair, reason);
            localPilotingChair.SetNetworkPresentationRequester(null);
        }
        localPilotingChair = null; localPilotingOpen = false; stopRequested = false;
    }
    private void SampleLocalHelmInput()
    {
        var session = DontSinkNetworkSession.Instance;
        if (!IsSpawned || chairs == null || !session.TryGetParticipantActor(NetworkManager.LocalClientId, out var actor) ||
            !CameraManager.HasGameplayInput(actor)) return;
        int index = OwnedStation(actor.OwnerClientId);
        if (index < 0 || !HasLocalHelmControl(chairs[index]) || localPilotingClosing) return;
        var source = actor.GetComponent<LocalBoatControlIntentSource>();
        if (source == null || !source.enabled) return;
        var intent = source.Current; stopRequested |= intent.ThrottleStopPressed;
        if (helmOrders.Value.Moored && Mathf.Abs(intent.ThrottleAdjust) > .001f)
        {
            stationFeedback = "Cannot throttle while docked.";
            intent.ThrottleAdjust = 0;
        }
        if (Time.realtimeSinceStartupAsDouble < nextHelmInput) return;
        nextHelmInput = Time.realtimeSinceStartupAsDouble + .1;
        SubmitHelmInputRpc(++helmSequence, index, stationRevision.Value, intent.ThrottleAdjust, intent.RudderAdjust, stopRequested);
        stopRequested = false;
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SubmitHelmInputRpc(uint sequence, int index, uint revision, float throttle, float rudder, bool stop, RpcParams rpc = default)
    {
        var session = DontSinkNetworkSession.Instance; ulong sender = rpc.Receive.SenderClientId;
        if (!session.Registry.TryResolveConnectedSender(sender, out var participant) ||
            !session.TryGetParticipantActor(sender, out var actor) || actor.OwnerClientId != sender ||
            participant.CharacterIdentity != actor.CharacterIdentity || !session.GameplayScene.IsParticipantReady(sender) ||
            index < 0 || index >= seats.Length || revision != stationRevision.Value || seats[index].Occupant != actor.gameObject ||
            !simulation.HasControlAuthority(chairs[index]) || !float.IsFinite(throttle) || !float.IsFinite(rudder) ||
            Mathf.Abs(throttle) > 1 || Mathf.Abs(rudder) > 1) return;
        if (!helmRequests.TryGetValue(sender, out var gate)) helmRequests.Add(sender, gate = new NetworkInteractionRequestGate());
        if (gate.Begin(sequence, Time.realtimeSinceStartupAsDouble) != NetworkInteractionRequestDisposition.New) return;
        helmInputs[sender] = new HelmInput { Station = index, Revision = revision, Received = Time.realtimeSinceStartupAsDouble,
            Intent = new BoatControlIntent { ThrottleAdjust = throttle, RudderAdjust = rudder } };
        if (stop)
        {
            helmInputs[sender].Intent.ThrottleAdjust = 0;
            simulation.SubmitIntent(chairs[index], helmInputs[sender].Intent);
            simulation.TrySetThrottleTarget(chairs[index], 0);
        }
    }
    private void TickHelmInputs()
    {
        if (simulation == null) return;
        for (int i = 0; i < seats.Length; i++)
        {
            if (!simulation.HasControlAuthority(chairs[i])) continue;
            var actor = seats[i].Occupant != null ? seats[i].Occupant.GetComponent<NetworkPlayerActor>() : null;
            BoatControlIntent intent = default;
            if (actor != null && helmInputs.TryGetValue(actor.OwnerClientId, out var input) && input.Station == i &&
                input.Revision == stationRevision.Value && Time.realtimeSinceStartupAsDouble - input.Received < .3)
                intent = input.Intent;
            simulation.SubmitIntent(chairs[i], intent);
        }
    }
}
