using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public sealed partial class NetworkSceneBoat
{
    private readonly Dictionary<ulong, NetworkInteractionRequestGate> inventoryRequests = new();
    private uint inventorySequence, pendingInventory;
    private BottomBarSlotType? queuedSelection;
    public string InventoryFeedback { get; private set; }
    public bool InventoryRequestPending => pendingInventory != 0;
    public bool RequestInventoryMove(NetworkInventoryAddress source, NetworkInventoryAddress destination, int quantity,
        ulong sourceFingerprint, ulong destinationFingerprint)
    {
        if (!IsSpawned || InventoryRequestPending || DontSinkNetworkSession.Instance?.GameplayScene?.LocalSceneReady != true) return false;
        pendingInventory = ++inventorySequence;
        SubmitInventoryRpc(pendingInventory, false, source, destination, quantity, sourceFingerprint, destinationFingerprint);
        return true;
    }
    public void RequestInventorySelection(BottomBarSlotType selection)
    {
        if (!IsSpawned) return;
        if (InventoryRequestPending) { queuedSelection = selection; return; }
        pendingInventory = ++inventorySequence;
        SubmitInventoryRpc(pendingInventory, true, new NetworkInventoryAddress(NetworkInventoryKind.Equipment, (int)selection), default, 0, 0, 0);
    }
    public bool RequestUiInventoryMove(InventorySlotUI source, InventorySlotUI destination, int quantity, ulong fingerprint)
    {
        if (source == null || destination == null || !source.TryGetNetworkAddress(out var from) || !destination.TryGetNetworkAddress(out var to)) return false;
        return RequestInventoryMove(from, to, quantity, fingerprint, NetworkInventoryModel.Fingerprint(destination.GetBoundItem()));
    }
    public bool RequestQuickInventoryTransfer(InventorySlotUI source)
    {
        if (InventoryRequestPending || source == null || !source.TryGetNetworkAddress(out var from)) return false;
        var item = source.GetBoundItem(); if (item == null) return false;
        var session = DontSinkNetworkSession.Instance;
        if (!session.TryGetParticipantActor(NetworkManager.LocalClientId, out var actor)) return false;
        var inventory = actor.GetComponentInChildren<PlayerInventory>(true);
        var overlay = UnityEngine.Object.FindAnyObjectByType<ExternalContainerOverlayUI>();
        if (from.Kind == NetworkInventoryKind.Container || from.Kind == NetworkInventoryKind.Storage)
        {
            for (int i = 0; i < inventory.HotbarSlotCount; i++)
            {
                var target = inventory.GetSlot(i).Instance;
                if (item.Definition.StowableInInventory && item.Definition.IsAllowedInParentSlot(PlayerInventory.HotbarIndexToSlotType(i)) &&
                    (target == null || target.CanStackWith(item) && target.RemainingStackSpace > 0))
                    return RequestInventoryMove(from, new NetworkInventoryAddress(NetworkInventoryKind.Hotbar, i), item.Quantity,
                        NetworkInventoryModel.Fingerprint(item), NetworkInventoryModel.Fingerprint(target));
            }
        }
        else if (overlay?.IsOpen == true && overlay.CurrentStorageModule != null)
        {
            var storage = overlay.CurrentStorageModule;
            for (int i = 0; i < storage.ContainerState.SlotCount; i++)
                if (ContainerPlacementUtility.CanPlaceIntoSlot(storage, i, item))
                    return RequestInventoryMove(from, StorageAddress(storage, i), item.Quantity,
                        NetworkInventoryModel.Fingerprint(item), NetworkInventoryModel.Fingerprint(storage.ContainerState.GetSlot(i).Instance));
        }
        else if (overlay?.IsOpen == true && overlay.CurrentContainer != null)
        {
            var parent = overlay.CurrentContainer;
            for (int i = 0; i < parent.ContainerState.SlotCount; i++)
                if (ContainerPlacementUtility.CanPlaceIntoSlot(parent, i, item))
                    return RequestInventoryMove(from, new NetworkInventoryAddress(NetworkInventoryKind.Container, i, parent.InstanceId), item.Quantity,
                        NetworkInventoryModel.Fingerprint(item), NetworkInventoryModel.Fingerprint(parent.ContainerState.GetSlot(i).Instance));
        }
        return false;
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SubmitInventoryRpc(uint sequence, bool selection, NetworkInventoryAddress source, NetworkInventoryAddress destination,
        int quantity, ulong expectedSource, ulong expectedDestination, RpcParams rpc = default)
    {
        var session = DontSinkNetworkSession.Instance; ulong sender = rpc.Receive.SenderClientId;
        if (!session.Registry.TryResolveConnectedSender(sender, out var participant) ||
            !session.TryGetParticipantActor(sender, out var actor) || actor.OwnerClientId != sender ||
            actor.CharacterIdentity != participant.CharacterIdentity || !session.GameplayScene.IsParticipantReady(sender)) return;
        if (!inventoryRequests.TryGetValue(sender, out var gate)) inventoryRequests.Add(sender, gate = new NetworkInteractionRequestGate());
        var disposition = gate.Begin(sequence, Time.realtimeSinceStartupAsDouble);
        string reason = disposition == NetworkInteractionRequestDisposition.Replay ? gate.LastReason : "Stale or rate-limited inventory request.";
        if (disposition == NetworkInteractionRequestDisposition.New)
        {
            bool accepted = false; reason = "Invalid inventory selection.";
            if (selection)
            {
                var inventory = actor.GetComponentInChildren<PlayerInventory>(true); var slot = (BottomBarSlotType)source.Slot;
                int index = PlayerInventory.SlotTypeToHotbarIndex(slot);
                if (Array.IndexOf(NetworkInventoryModel.EquipmentSlots, slot) >= 0 || index >= 0 && index < inventory.HotbarSlotCount)
                { inventory.SetSelectedSlot(slot); accepted = true; reason = "Done."; }
            }
            else
            {
                try { accepted = NetworkInventoryModel.TryMove(actor, source, destination, quantity, expectedSource, expectedDestination, out _, out reason); }
                catch (Exception exception) when (exception is System.IO.InvalidDataException || exception is ArgumentException || exception is InvalidOperationException)
                { reason = "Item data/layout changed or exceeded its limits. No transfer committed."; }
            }
            gate.Complete(accepted, reason);
        }
        InventoryResultRpc(sequence, new FixedString128Bytes(reason), RpcTarget.Single(sender, RpcTargetUse.Temp));
    }
    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void InventoryResultRpc(uint sequence, FixedString128Bytes reason, RpcParams rpc = default)
    {
        if (rpc.Receive.SenderClientId != NetworkManager.ServerClientId || sequence != pendingInventory) return;
        pendingInventory = 0; InventoryFeedback = reason.ToString();
        if (InventoryFeedback != "Done.") GameMessageService.PostWarning(InventoryFeedback);
        if (queuedSelection.HasValue) { var selection = queuedSelection.Value; queuedSelection = null; RequestInventorySelection(selection); }
    }
}
