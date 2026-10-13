using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public struct NetworkPilotSeat : INetworkSerializable, IEquatable<NetworkPilotSeat>
{
    public bool Occupied, ControlsHelm;
    public byte HelmStatus;
    public ulong Owner;
    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    { s.SerializeValue(ref Occupied); s.SerializeValue(ref ControlsHelm); s.SerializeValue(ref Owner); s.SerializeValue(ref HelmStatus); }
    public bool Equals(NetworkPilotSeat other) => Occupied == other.Occupied && ControlsHelm == other.ControlsHelm && Owner == other.Owner && HelmStatus == other.HelmStatus;
}
public enum NetworkStationAction : byte { Sit = 1, Stand, Throttle, Rudder, EnginesOn, EnginesOff }

public sealed partial class NetworkSceneBoat
{
    private NetworkList<NetworkPilotSeat> stationState;
    private readonly NetworkVariable<uint> stationRevision = new(1);
    private readonly Dictionary<ulong, NetworkInteractionRequestGate> stationRequests = new();
    private PilotChairInteractable[] chairs;
    private SeatController2D[] seats;
    private BoatPilotingSimulation simulation;
    private DeckBoardZone[] boardingZones;
    private uint stationSequence, stationPending, stationAcknowledged;
    private string stationFeedback = "Hold W / Up in a deck boarding zone.";
    public string StationFeedback => stationFeedback;
    public uint StationRevision => stationRevision.Value;
    public int StationCount => chairs?.Length ?? 0;
    public NetworkPilotSeat StationState(int index) => stationState[index];
    public PilotChairInteractable Station(int index) => chairs[index];
    private void InitializeStations() => stationState = new NetworkList<NetworkPilotSeat>();
    private void BindStations()
    {
        BindInteractions();
        chairs = boat.GetComponentsInChildren<PilotChairInteractable>(true);
        if (chairs.Length > 64) throw new InvalidOperationException("Pilot station snapshot capacity exceeded.");
        seats = new SeatController2D[chairs.Length];
        boardingZones = boat.GetComponentsInChildren<DeckBoardZone>(true);
        simulation = boat.GetComponent<BoatPilotingSimulation>();
        for (int i = 0; i < chairs.Length; i++)
        {
            // Ordinary chair Update opens a local cartridge and submits local
            // intent. This adapter owns its shared occupancy/control instead.
            chairs[i].enabled = false;
            seats[i] = chairs[i].GetComponent<SeatController2D>(); seats[i].enabled = IsServer;
            if (IsServer) stationState.Add(default);
        }
    }
    public SeatController2D SeatFor(NetworkPlayerActor actor)
    {
        if (actor == null || seats == null) return null;
        foreach (var seat in seats) if (seat.Occupant == actor.gameObject) return seat;
        return null;
    }
    private void TickStations()
    {
        TickInteractions();
        for (int i = 0; i < seats.Length; i++)
        {
            var seat = seats[i]; var chair = chairs[i];
            var actor = seat.Occupant != null ? seat.Occupant.GetComponent<NetworkPlayerActor>() : null;
            if (actor != null && (!DontSinkNetworkSession.Instance.IsParticipantActor(actor.OwnerClientId, actor) ||
                actor.GetComponent<PlayerBoardingState>().CurrentBoatRoot != boat.transform))
            { seat.Eject(SeatEjectReason.AccessInvalid); actor = null; }
            if (actor != null && !seat.TickSeat()) actor = null;
            if (actor == null) simulation?.ReleaseControl(chair);
            else if (chair.IsHelmControlAvailable)
            {
                if (simulation != null && !simulation.HasControlAuthority(chair)) simulation.TryClaimControl(chair);
            }
            else simulation?.ReleaseControl(chair);
            var value = new NetworkPilotSeat { Occupied = actor != null, Owner = actor != null ? actor.OwnerClientId : 0,
                ControlsHelm = actor != null && simulation != null && simulation.HasControlAuthority(chair),
                HelmStatus = (byte)chair.CurrentPilotingHelmStatus };
            if (!stationState[i].Equals(value)) { stationState[i] = value; stationRevision.Value++; }
        }
        TickHelmInputs(); PublishHelmOrders();
    }
    public void ReleaseParticipant(ulong clientId)
    {
        if (!IsServer || seats == null) return;
        for (int i = 0; i < seats.Length; i++)
            if (stationState[i].Occupied && stationState[i].Owner == clientId)
            { seats[i].Eject(SeatEjectReason.OccupantMissing); simulation?.ReleaseControl(chairs[i]); }
        stationRequests.Remove(clientId); TickStations();
        interactionHolds.Remove(clientId); interactionRequests.Remove(clientId);
        moduleRequests.Remove(clientId);
        debugRequests.Remove(clientId);
        inventoryRequests.Remove(clientId);
        looseRequests.Remove(clientId); looseHolds.Remove(clientId); throwHolds.Remove(clientId);
        linkRequests.Remove(clientId); selectedHelmGenerations.Remove(clientId);
        installationRequests.Remove(clientId); removalHolds.Remove(clientId);
        helmInputs.Remove(clientId); helmRequests.Remove(clientId);
    }
    public void RequestStation(NetworkStationAction action, int index, float value = 0)
    {
        var session = DontSinkNetworkSession.Instance;
        if (!IsSpawned || stationPending != 0 || !session.HasLocalParticipant) return;
        uint revision = stationRevision.Value;
        if (unchecked((int)(stationAcknowledged - revision)) > 0) revision = stationAcknowledged;
        stationPending = ++stationSequence; if (stationPending == 0) stationPending = ++stationSequence;
        SubmitStationRpc(stationPending, new FixedString64Bytes(session.LocalCharacterIdentity), revision, action, index, value);
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone, Delivery = RpcDelivery.Reliable)]
    public void SubmitStationRpc(uint id, FixedString64Bytes character, uint revision, NetworkStationAction action, int index, float value, RpcParams rpc = default)
    {
        ulong sender = rpc.Receive.SenderClientId; var session = DontSinkNetworkSession.Instance;
        if (!session.Registry.TryResolveConnectedSender(sender, out var participant) ||
            !session.TryGetParticipantActor(sender, out var actor) || actor.OwnerClientId != sender ||
            actor.CharacterIdentity != participant.CharacterIdentity || character.ToString() != participant.CharacterIdentity ||
            !session.GameplayScene.IsParticipantReady(sender)) return;
        if (!stationRequests.TryGetValue(sender, out var gate)) stationRequests.Add(sender, gate = new NetworkInteractionRequestGate());
        var disposition = gate.Begin(id, Time.realtimeSinceStartupAsDouble);
        string reason = "Invalid station command.";
        if (disposition == NetworkInteractionRequestDisposition.New)
        {
            if (revision != stationRevision.Value) reason = "Station changed; try again.";
            else if (index >= 0 && index < seats.Length && action >= NetworkStationAction.Sit && action <= NetworkStationAction.EnginesOff &&
                float.IsFinite(value) && Mathf.Abs(value) <= 1) ExecuteStation(actor, action, index, value, out reason);
            gate.Complete(reason == "Done.", reason);
        }
        else reason = disposition == NetworkInteractionRequestDisposition.Replay ? gate.LastReason : "Stale or rate-limited command.";
        StationResultRpc(id, new FixedString128Bytes(reason), stationRevision.Value, RpcTarget.Single(sender, RpcTargetUse.Temp));
    }
    private void ExecuteStation(NetworkPlayerActor actor, NetworkStationAction action, int index, float value, out string reason)
    {
        reason = "You do not control this station.";
        var seat = seats[index]; var chair = chairs[index];
        if (action == NetworkStationAction.Sit)
        {
            var context = new InteractContext(actor.gameObject, actor.transform, actor.AuthoritativePosition, Vector2.right);
            if (actor.GetComponent<PlayerBoardingState>().CurrentBoatRoot != boat.transform ||
                SeatFor(actor) != null || !chair.CanInteract(context) || !seat.TrySeat(actor.gameObject))
            { reason = "Seat occupied, wrong boat context, or move closer."; return; }
        }
        else
        {
            if (seat.Occupant != actor.gameObject) return;
            if (action == NetworkStationAction.Stand) seat.Eject(SeatEjectReason.Manual);
            else
            {
                if (simulation == null || !simulation.HasControlAuthority(chair)) return;
                bool ok = action == NetworkStationAction.Throttle ? simulation.TrySetThrottleTarget(chair, value) :
                    action == NetworkStationAction.Rudder ? simulation.TrySetRudderTarget(chair, value) :
                    simulation.TrySetEnginesRunning(chair, action == NetworkStationAction.EnginesOn, out _, out _);
                if (!ok) { reason = NodeSceneMooring.IsLocked(boat) ? "Cannot throttle while docked." : "Helm command unavailable."; return; }
            }
        }
        nextHelmOrders = 0; TickStations(); reason = "Done.";
    }
    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void StationResultRpc(uint id, FixedString128Bytes reason, uint revision, RpcParams rpc = default)
    {
        if (rpc.Receive.SenderClientId != NetworkManager.ServerClientId || id != stationPending) return;
        stationPending = 0; stationAcknowledged = revision; stationFeedback = reason.ToString();
    }
    private void Update()
    {
        var session = DontSinkNetworkSession.Instance;
        if (!IsSpawned || chairs == null || !session.TryGetParticipantActor(NetworkManager.LocalClientId, out var actor) ||
            !CameraManager.HasGameplayInput(actor) || stationState.Count != chairs.Length) return;
        UpdatePilotingPresentation(actor);
        if (Input.GetKeyDown(KeyCode.E))
        {
            int owned = OwnedStation(actor.OwnerClientId);
            if (owned < 0 && actor.GetComponent<Interactor2D>()?.enabled == true) return;
            int target = owned >= 0 ? owned : NearestStation(actor.transform.position);
            if (target >= 0) RequestStation(owned >= 0 ? NetworkStationAction.Stand : NetworkStationAction.Sit, target);
        }
    }
    private int OwnedStation(ulong owner)
    { for (int i = 0; i < stationState.Count; i++) if (stationState[i].Occupied && stationState[i].Owner == owner) return i; return -1; }
    private int NearestStation(Vector2 point)
    {
        int target = -1; float closest = 1.5f * 1.5f;
        for (int i = 0; i < chairs.Length; i++)
        { float distance = ((Vector2)chairs[i].transform.position - point).sqrMagnitude; if (distance < closest) { closest = distance; target = i; } }
        return target;
    }
    private void OnGUI()
    {
        var session = DontSinkNetworkSession.Instance;
        if (!IsSpawned || chairs == null || stationState.Count != chairs.Length ||
            !session.TryGetParticipantActor(NetworkManager.LocalClientId, out var actor) || !CameraManager.HasGameplayInput(actor)) return;
        int owned = OwnedStation(actor.OwnerClientId), nearby = NearestStation(actor.transform.position);
        bool inZone = false;
        foreach (var zone in boardingZones)
            if (Vector2.Distance(ClosestInteractionPoint(zone.GetComponent<Collider2D>(), actor.transform.position), actor.transform.position) < .3f) { inZone = true; break; }
        if (owned < 0 && nearby < 0 && !inZone) return;
        if (owned >= 0 && localPilotingOpen) return;
        GUILayout.BeginArea(new Rect(10, 10, 320, 310), GUI.skin.box);
        if (inZone) GUILayout.Label(actor.GetComponent<PlayerBoardingState>().IsBoarded ? "Hold Down, then press Jump to leave deck." : "Hold W / Up to board deck.");
        if (owned < 0) GUILayout.Label(nearby >= 0 ? "E: sit at pilot station." : "Deck boarding zone");
        else
        {
            GUILayout.Label("E: leave station. Helm unlinked/offline.");
        }
        GUILayout.Label(stationFeedback); GUILayout.EndArea();
    }
    private void ReleaseStations()
    {
        CloseLocalPiloting("Session ended");
        if (IsServer && seats != null) for (int i = 0; i < seats.Length; i++)
        { seats[i].Eject(SeatEjectReason.AccessInvalid); simulation?.ReleaseControl(chairs[i]); }
        stationRequests.Clear();
        linkRequests.Clear(); selectedHelmGenerations.Clear();
        installationRequests.Clear(); removalHolds.Clear();
        interactionHolds.Clear(); interactionRequests.Clear();
    }
}
