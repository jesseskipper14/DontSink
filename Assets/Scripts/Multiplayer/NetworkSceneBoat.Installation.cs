using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public sealed partial class NetworkSceneBoat
{
    private sealed class ModuleRemovalHold { public HardpointInteractable Target; public uint Generation; public double Started, LastSeen; public bool Fired; }
    private readonly Dictionary<ulong, NetworkInteractionRequestGate> installationRequests = new();
    private readonly Dictionary<ulong, ModuleRemovalHold> removalHolds = new();
    private uint installationSequence;
    private HardpointInteractable localPickupTarget;
    private float localPickupElapsed;
    private double nextPickup;
    public string InstallationFeedback { get; private set; }
    public static bool SupportsInstallation(ModuleDefinition definition)
    {
        var p = definition?.InstalledPrefab;
        return p != null && (p.GetComponent<EngineModule>() != null || p.GetComponent<GeneratorModule>() != null ||
            p.GetComponent<PumpModule>() != null || p.GetComponent<HelmModule>() != null || p.GetComponent<RudderModule>() != null || IsPlainStorage(p.GetComponent<StorageModule>()));
    }
    public void RequestHardpointInstall(HardpointInteractable target, in InteractContext context)
    {
        if (target == null || !target.TryFindCompatiblePlayerModule(context, out _, out var item, out var slot, out var equipment)) return;
        var address = new NetworkInventoryAddress(equipment ? NetworkInventoryKind.Equipment : NetworkInventoryKind.Hotbar,
            equipment ? (int)slot : PlayerInventory.SlotTypeToHotbarIndex(slot));
        InstallationRpc(++installationSequence, new FixedString64Bytes(target.TargetHardpoint.HardpointId),
            HardpointGeneration(target.TargetHardpoint), false, false, address, NetworkInventoryModel.Fingerprint(item));
    }
    private void ProcessModulePickup(Interactor2D interactor, in InteractContext context, InteractionIntent intent)
    {
        interactor.TryResolveBestPickup(context, out var pickup);
        var target = pickup as HardpointInteractable;
        bool held = intent.PickupHeld && target != null && SupportsInstallation(target.TargetHardpoint.InstalledModule?.Definition);
        if (!held || target != localPickupTarget) localPickupElapsed = 0;
        if (held) localPickupElapsed += Time.deltaTime;
        interactor.PresentNetworkPickup(held ? target : null, localPickupElapsed);
        if (held && (target != localPickupTarget || Time.realtimeSinceStartupAsDouble >= nextPickup) || !held && localPickupTarget != null)
        {
            var send = held ? target : localPickupTarget;
            InstallationRpc(++installationSequence, new FixedString64Bytes(send.TargetHardpoint.HardpointId),
                HardpointGeneration(send.TargetHardpoint), true, held, default, 0);
            nextPickup = Time.realtimeSinceStartupAsDouble + .12;
        }
        localPickupTarget = held ? target : null;
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void InstallationRpc(uint sequence, FixedString64Bytes hardpointId, uint generation, bool removal, bool held,
        NetworkInventoryAddress source, ulong expectedItem, RpcParams rpc = default)
    {
        var session = DontSinkNetworkSession.Instance; ulong sender = rpc.Receive.SenderClientId;
        if (!session.Registry.TryResolveConnectedSender(sender, out var participant) || !session.TryGetParticipantActor(sender, out var actor) ||
            actor.OwnerClientId != sender || actor.CharacterIdentity != participant.CharacterIdentity || !session.GameplayScene.IsParticipantReady(sender)) return;
        if (!installationRequests.TryGetValue(sender, out var gate)) installationRequests.Add(sender, gate = new NetworkInteractionRequestGate());
        var disposition = gate.Begin(sequence, Time.realtimeSinceStartupAsDouble);
        if (disposition != NetworkInteractionRequestDisposition.New) return;
        if (removal && !held) { removalHolds.Remove(sender); gate.Complete(true, "Released."); return; }
        var target = interactionTargets.OfType<HardpointInteractable>().FirstOrDefault(t => t.TargetHardpoint.HardpointId == hardpointId.ToString());
        int index = Array.IndexOf(interactionTargets, target);
        string reason = "Module changed, unavailable, or move closer.";
        if (target == null || generation != HardpointGeneration(target.TargetHardpoint) || !LayoutHasCapacity(target.TargetHardpoint) ||
            index < 0 || !TryInteractionContext(actor, index, out _) || SeatFor(actor) != null)
        { removalHolds.Remove(sender); gate.Complete(false, reason); InstallationResultRpc(new FixedString128Bytes(reason), RpcTarget.Single(sender, RpcTargetUse.Temp)); return; }
        if (removal)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (!removalHolds.TryGetValue(sender, out var hold) || hold.Target != target || hold.Generation != generation || now - hold.LastSeen > .3)
                removalHolds[sender] = hold = new ModuleRemovalHold { Target = target, Generation = generation, Started = now };
            hold.LastSeen = now; gate.Complete(true, "Holding."); return;
        }
        bool accepted = TryInstallItem(actor, target, source, expectedItem, out reason);
        gate.Complete(accepted, reason);
        InstallationResultRpc(new FixedString128Bytes(reason), RpcTarget.Single(sender, RpcTargetUse.Temp));
    }
    private bool TryInstallItem(NetworkPlayerActor actor, HardpointInteractable target, NetworkInventoryAddress source, ulong fingerprint, out string reason)
    {
        reason = "Select a supported module item and a compatible empty hardpoint.";
        if (source.Kind != NetworkInventoryKind.Hotbar && source.Kind != NetworkInventoryKind.Equipment) return false;
        var plan = new NetworkInventoryModel.Plan(actor); var slot = plan.Resolve(source); var item = slot?.Actual;
        var hp = target.TargetHardpoint;
        if (item == null || item.Quantity != 1 || !item.Definition.IsInstallableModule || FingerprintMismatch(item, fingerprint) ||
            !SupportsInstallation(item.Definition.ModuleDefinition) || !hp.CanInstallAuthoritatively(item.Definition.ModuleDefinition, out _) || ModuleCount >= 64) return false;
        var saved = NetworkItemCodec.Capture(item); // Complete identity and metadata stay with the installed module.
        slot.Write(null);
        if (!plan.TryPrepare(out var commit, out reason)) return false;
        if (!hp.TryInstall(item.Definition.ModuleDefinition, out _)) return false;
        PublishLayout(hp, item.Definition, saved); commit(); RefreshDebugModuleBindings(); reason = "Installed " + item.Definition.DisplayName + "."; return true;
    }
    private static bool FingerprintMismatch(ItemInstance item, ulong fingerprint) => NetworkInventoryModel.Fingerprint(item) != fingerprint;
    private void TickModuleRemoval()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        foreach (var pair in removalHolds.ToArray())
        {
            var hold = pair.Value; var target = hold.Target;
            int index = Array.IndexOf(interactionTargets, target);
            if (hold.Fired) continue;
            if (now - hold.LastSeen > .3 || target == null || index < 0 || !DontSinkNetworkSession.Instance.TryGetParticipantActor(pair.Key, out var actor) ||
                HardpointGeneration(target.TargetHardpoint) != hold.Generation || SeatFor(actor) != null || !TryInteractionContext(actor, index, out _))
            { removalHolds.Remove(pair.Key); continue; }
            if (now - hold.Started < target.PickupHoldDuration) continue;
            hold.Fired = true;
            TryReturnModule(actor, target, out var reason);
            InstallationResultRpc(new FixedString128Bytes(reason), RpcTarget.Single(pair.Key, RpcTargetUse.Temp));
        }
    }
    private bool TryReturnModule(NetworkPlayerActor actor, HardpointInteractable target, out string reason)
    {
        reason = "Empty and switch off the module before removing it.";
        var hp = target.TargetHardpoint; var module = hp.InstalledModule;
        if (module == null || !SupportsInstallation(module.Definition) || target.InstalledModuleHasContents() ||
            module.GetComponent<EngineModule>()?.IsOn == true || module.GetComponent<GeneratorModule>()?.IsOn == true ||
            module.GetComponent<PumpModule>()?.IsOn == true || !LayoutHasCapacity(hp)) return false;
        var definition = module.Definition.ItemDefinition;
        if (definition == null) return false;
        installedItems.TryGetValue(hp.HardpointId, out var source);
        var item = source.IsEmpty ? ItemInstance.Create(definition, 1) : ItemInstance.FromSnapshot(NetworkItemCodec.ReadItem(source, DontSinkNetworkSession.Instance.GameplayScene), DontSinkNetworkSession.Instance.GameplayScene);
        var plan = new NetworkInventoryModel.Plan(actor); NetworkInventoryModel.SlotView destination = null;
        for (int i = 0; i < plan.Inventory.HotbarSlotCount; i++)
        {
            var slot = plan.Resolve(new NetworkInventoryAddress(NetworkInventoryKind.Hotbar, i));
            if (slot.Actual == null && slot.Accept(item)) { destination = slot; break; }
        }
        if (destination == null)
        {
            var hands = plan.Resolve(new NetworkInventoryAddress(NetworkInventoryKind.Equipment, (int)BottomBarSlotType.Hands));
            if (hands.Actual == null && hands.Accept(item)) destination = hands;
        }
        reason = "No inventory space or empty hands for this module.";
        if (destination == null) return false;
        destination.Write(item.ToSnapshot());
        if (!plan.TryPrepare(out var commit, out reason)) return false;
        if (!hp.TryRemove(out _)) { reason = "Module removal was blocked."; return false; }
        PublishLayout(hp, null, default); commit(); RefreshDebugModuleBindings(); reason = "Returned " + definition.DisplayName + "."; return true;
    }
    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void InstallationResultRpc(FixedString128Bytes reason, RpcParams rpc = default)
    {
        if (rpc.Receive.SenderClientId != NetworkManager.ServerClientId) return;
        InstallationFeedback = reason.ToString(); GameMessageService.PostInfo(InstallationFeedback);
    }
}
