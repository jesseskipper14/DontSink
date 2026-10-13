using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public struct NetworkStationLinks : INetworkSerializable, IEquatable<NetworkStationLinks>
{
    public FixedString64Bytes[] Helms;
    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        int count = Helms?.Length ?? 0; s.SerializeValue(ref count);
        if (count < 0 || count > 32) throw new InvalidOperationException("Station link capacity exceeded.");
        if (s.IsReader) Helms = new FixedString64Bytes[count];
        for (int i = 0; i < count; i++) s.SerializeValue(ref Helms[i]);
    }
    public bool Equals(NetworkStationLinks other)
    {
        int count = Helms?.Length ?? 0;
        if (count != (other.Helms?.Length ?? 0)) return false;
        for (int i = 0; i < count; i++) if (!Helms[i].Equals(other.Helms[i])) return false;
        return true;
    }
}
public sealed partial class NetworkSceneBoat
{
    private readonly NetworkVariable<NetworkStationLinks> stationLinks = new();
    private readonly Dictionary<ulong, NetworkInteractionRequestGate> linkRequests = new();
    private readonly Dictionary<ulong, uint> selectedHelmGenerations = new();
    private uint linkSequence;
    public string LinkFeedback { get; private set; }
    public void RequestHelmLink(MonoBehaviour target)
    {
        int index = Array.IndexOf(interactionTargets, target);
        if (index < 0 || !IsSpawned) return;
        uint generation = target is HardpointInteractable hp ? HardpointGeneration(hp.TargetHardpoint) : 0;
        HelmLinkRpc(++linkSequence, interactionRevision.Value, index, generation);
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void HelmLinkRpc(uint sequence, uint revision, int index, uint generation, RpcParams rpc = default)
    {
        var session = DontSinkNetworkSession.Instance; ulong sender = rpc.Receive.SenderClientId;
        if (!session.Registry.TryResolveConnectedSender(sender, out var participant) || !session.TryGetParticipantActor(sender, out var actor) ||
            actor.OwnerClientId != sender || actor.CharacterIdentity != participant.CharacterIdentity || !session.GameplayScene.IsParticipantReady(sender)) return;
        if (!linkRequests.TryGetValue(sender, out var gate)) linkRequests.Add(sender, gate = new NetworkInteractionRequestGate());
        var disposition = gate.Begin(sequence, Time.realtimeSinceStartupAsDouble);
        string reason = disposition == NetworkInteractionRequestDisposition.Replay ? gate.LastReason : "Stale link request.";
        if (disposition == NetworkInteractionRequestDisposition.New)
        {
            reason = "Move closer, stand up, or select a current helm."; bool accepted = false;
            var context = new InteractContext(actor.gameObject, actor.transform, actor.AuthoritativePosition, Vector2.right);
            var selection = HelmLinkSession.GetOrCreate(actor.gameObject);
            if (revision == interactionRevision.Value && index >= 0 && index < interactionTargets.Length && SeatFor(actor) == null)
            {
                var target = interactionTargets[index];
                if (target is HardpointInteractable hp && generation == HardpointGeneration(hp.TargetHardpoint) && hp.CanLink(context))
                {
                    hp.Link(context); selectedHelmGenerations[sender] = generation; accepted = true;
                    reason = selection.HasPendingHelm ? "Helm selected. Press L on a station to link it." : "Helm selection cancelled.";
                }
                else if (target is PilotChairInteractable chair && chair.CanLink(context))
                {
                    int station = Array.IndexOf(chairs, chair); var pending = selection.PendingHelmHardpoint;
                    bool current = pending == null || selectedHelmGenerations.TryGetValue(sender, out var selected) && selected == HardpointGeneration(pending);
                    if (station >= 0 && !stationState[station].Occupied && current)
                    {
                        chair.Link(context); selectedHelmGenerations.Remove(sender); accepted = true;
                        reason = chair.LinkedHelmHardpoint != null ? "Station linked." : "Station unlinked.";
                        PublishStationLinks(); TickStations();
                    }
                    else reason = "Station occupied or the selected helm changed.";
                }
            }
            gate.Complete(accepted, reason);
        }
        string selectedId = HelmLinkSession.Get(actor.gameObject)?.PendingHelmHardpoint?.HardpointId ?? "";
        HelmLinkResultRpc(new FixedString128Bytes(reason), new FixedString64Bytes(selectedId), RpcTarget.Single(sender, RpcTargetUse.Temp));
    }
    private void PublishStationLinks()
    {
        if (!IsServer || chairs == null) return;
        stationLinks.Value = new NetworkStationLinks { Helms = chairs.Select(c => new FixedString64Bytes(c.LinkedHelmHardpoint?.HardpointId ?? "")).ToArray() };
    }
    private void ApplyStationLinks()
    {
        var links = stationLinks.Value.Helms;
        if (links == null || chairs == null || links.Length != chairs.Length) return;
        var hardpoints = boat.GetComponentsInChildren<Hardpoint>(true);
        for (int i = 0; i < links.Length; i++)
        {
            string id = links[i].ToString();
            if ((chairs[i].LinkedHelmHardpoint?.HardpointId ?? "") == id) continue;
            if (id.Length == 0) chairs[i].UnlinkHelm();
            else
            {
                var hp = hardpoints.SingleOrDefault(h => h.HardpointId == id);
                if (hp == null || !chairs[i].TryLinkToHelm(hp)) throw new InvalidOperationException("Invalid station link snapshot.");
            }
        }
    }
    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void HelmLinkResultRpc(FixedString128Bytes reason, FixedString64Bytes selectedId, RpcParams rpc = default)
    {
        if (rpc.Receive.SenderClientId != NetworkManager.ServerClientId || !DontSinkNetworkSession.Instance.TryGetParticipantActor(NetworkManager.LocalClientId, out var actor)) return;
        LinkFeedback = reason.ToString();
        var selection = HelmLinkSession.GetOrCreate(actor.gameObject);
        var hp = boat.GetComponentsInChildren<Hardpoint>(true).FirstOrDefault(h => h.HardpointId == selectedId.ToString());
        if (hp == null) selection.Cancel(); else selection.Begin(hp);
        GameMessageService.PostInfo(LinkFeedback);
    }
}
