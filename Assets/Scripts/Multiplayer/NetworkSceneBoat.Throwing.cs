using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum NetworkThrowPhase : byte { Begin = 1, Pulse, Cancel, Release }
public sealed partial class NetworkSceneBoat
{
    private sealed class ThrowHold { public string ItemId; public ulong Fingerprint; public double Started, LastSeen; }
    private readonly Dictionary<ulong, ThrowHold> throwHolds = new();
    public void RequestWorldThrow(NetworkThrowPhase phase, ulong fingerprint, Vector2 direction = default)
    {
        if (!IsSpawned || DontSinkNetworkSession.Instance.GameplayScene.LocalSceneReady != true) return;
        uint sequence = ++looseSequence;
        if (phase == NetworkThrowPhase.Release) pendingLoose = sequence;
        ThrowRpc(sequence, phase, fingerprint, direction);
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ThrowRpc(uint sequence, NetworkThrowPhase phase, ulong fingerprint, Vector2 direction, RpcParams rpc = default)
    {
        ulong sender = rpc.Receive.SenderClientId;
        if (!TryLooseRequester(sender, sequence, out var actor, out var gate)) return;
        if (phase == NetworkThrowPhase.Cancel) { throwHolds.Remove(sender); gate.Complete(true, "Cancelled."); return; }
        var inventory = actor.GetComponentInChildren<PlayerInventory>(true);
        var item = actor.GetComponentInChildren<PlayerEquipment>(true).Get(BottomBarSlotType.Hands);
        var input = actor.GetComponentInChildren<PlayerInventoryInput>(true);
        string reason = "Held cargo changed, unavailable, or throw charge expired.";
        bool valid = item != null && input != null && inventory.SelectedSlot == BottomBarSlotType.Hands && SeatFor(actor) == null &&
            inventory.CanThrowHeld(item) && SupportsLooseItem(item.Definition) && NetworkInventoryModel.Fingerprint(item) == fingerprint;
        double now = Time.realtimeSinceStartupAsDouble;
        if (phase == NetworkThrowPhase.Begin && valid)
        {
            // Repeated Begin cannot preserve an old charge on a different item.
            throwHolds[sender] = new ThrowHold { ItemId = item.InstanceId, Fingerprint = fingerprint, Started = now, LastSeen = now };
            gate.Complete(true, "Charging."); return;
        }
        bool current = valid && throwHolds.TryGetValue(sender, out var existing) && existing.ItemId == item.InstanceId &&
            existing.Fingerprint == fingerprint && now - existing.LastSeen <= .4 && now - existing.Started <= 10;
        if (phase == NetworkThrowPhase.Pulse)
        {
            if (current) throwHolds[sender].LastSeen = now; else throwHolds.Remove(sender);
            gate.Complete(current, current ? "Charging." : reason); return;
        }
        if (phase != NetworkThrowPhase.Release) { gate.Complete(false, "Invalid throw phase."); return; }
        if (current && HasLooseBudget(item) && WorldTopology.IsFinite(direction.x) && WorldTopology.IsFinite(direction.y) && direction.sqrMagnitude > .0001f && WorldTopology.IsFinite(direction.sqrMagnitude))
        {
            var hold = throwHolds[sender]; float elapsed = (float)(now - hold.Started);
            float charge = Mathf.Clamp01((elapsed - input.ThrowHoldThreshold) / input.FullChargeDuration);
            if (elapsed >= input.ThrowHoldThreshold && inventory.TryThrowHeld(item.InstanceId, direction.normalized, charge, out var thrown, out var failure))
            { RegisterLooseItem(thrown); TickLooseItems(true); reason = "Thrown."; }
            else reason = "Hold Q longer before throwing.";
        }
        throwHolds.Remove(sender); gate.Complete(reason == "Thrown.", reason); LooseResult(sequence, reason, sender);
    }
    private void ExpireThrowHolds()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        var stale = new List<ulong>();
        foreach (var pair in throwHolds)
            if (now - pair.Value.LastSeen > .4 || now - pair.Value.Started > 10) stale.Add(pair.Key);
        foreach (var sender in stale) throwHolds.Remove(sender);
    }
}
