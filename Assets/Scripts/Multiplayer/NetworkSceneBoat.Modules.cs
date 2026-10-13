using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public enum NetworkModuleCommand : byte { On = 1, Off, DepositFuel, WithdrawFuel }
public struct NetworkModuleRecord : INetworkSerializable, IEquatable<NetworkModuleRecord>
{
    public FixedString64Bytes Hardpoint;
    public uint Revision, Generation;
    public byte Kind;
    public bool On;
    public NetworkItemData Fuel;
    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    { s.SerializeValue(ref Hardpoint); s.SerializeValue(ref Revision); s.SerializeValue(ref Generation); s.SerializeValue(ref Kind); s.SerializeValue(ref On); Fuel.NetworkSerialize(s); }
    public bool Equals(NetworkModuleRecord other) => Hardpoint.Equals(other.Hardpoint) && Revision == other.Revision && Generation == other.Generation &&
        Kind == other.Kind && On == other.On && Fuel.Equals(other.Fuel);
}
public struct NetworkModuleSnapshot : INetworkSerializable, IEquatable<NetworkModuleSnapshot>
{
    public NetworkModuleRecord[] Records;
    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        int count = Records?.Length ?? 0; s.SerializeValue(ref count);
        if (count < 0 || count > 64) throw new InvalidOperationException("Module snapshot capacity exceeded.");
        if (s.IsReader) Records = new NetworkModuleRecord[count];
        int bytes = 0;
        for (int i = 0; i < count; i++)
        {
            Records[i].NetworkSerialize(s); bytes += Records[i].Fuel.Bytes?.Length ?? 0;
            if (bytes > NetworkItemData.MaxBytes) throw new InvalidOperationException("Module snapshot byte budget exceeded.");
        }
    }
    public bool Equals(NetworkModuleSnapshot other)
    {
        int count = Records?.Length ?? 0;
        if (count != (other.Records?.Length ?? 0)) return false;
        for (int i = 0; i < count; i++) if (!Records[i].Equals(other.Records[i])) return false;
        return true;
    }
}
public sealed partial class NetworkSceneBoat
{
    private readonly NetworkVariable<NetworkModuleSnapshot> moduleData = new();
    private HardpointInteractable[] moduleTargets;
    private uint[] appliedModuleRevisions;
    private readonly HashSet<ItemInstance> watchedModuleFuel = new();
    private readonly HashSet<EngineModule> sharedModuleEngines = new();
    private readonly Dictionary<ulong, NetworkInteractionRequestGate> moduleRequests = new();
    private bool moduleFuelDirty = true;
    private double nextModuleSnapshot;
    private uint moduleSequence, pendingModule;
    private string moduleFeedback;
    public string ModuleFeedback => moduleFeedback;
    public int ModuleCount => moduleTargets?.Length ?? 0;
    public HardpointInteractable ModuleTarget(int index) => moduleTargets[index];
    public NetworkModuleRecord ModuleState(int index) => moduleData.Value.Records[index];
    public static bool SupportsModule(HardpointInteractable target)
    {
        var module = target.TargetHardpoint?.InstalledModule;
        return module != null && (module.GetComponent<EngineModule>() != null ||
            module.GetComponent<GeneratorModule>() != null || module.GetComponent<PumpModule>() != null);
    }
    private void BindModules()
    {
        foreach (var fuel in watchedModuleFuel) fuel.Changed -= ModuleFuelChanged;
        watchedModuleFuel.Clear(); sharedModuleEngines.Clear(); moduleFuelDirty = true;
        moduleTargets = boat.GetComponentsInChildren<HardpointInteractable>(true).Where(SupportsModule).ToArray();
        if (moduleTargets.Length > 64) throw new InvalidOperationException("Shared module capacity exceeded.");
        appliedModuleRevisions = new uint[moduleTargets.Length];
        foreach (var target in moduleTargets)
            if (target.TargetHardpoint.InstalledModule.GetComponent<EngineModule>() is EngineModule engine) sharedModuleEngines.Add(engine);
        if (IsServer) TickModules(true);
    }
    private ItemInstance ModuleFuel(int index)
    {
        var module = moduleTargets[index].TargetHardpoint.InstalledModule;
        return module.GetComponent<EngineModule>()?.FuelContainerItem ?? module.GetComponent<GeneratorModule>()?.FuelContainerItem;
    }
    private void ModuleFuelChanged() => moduleFuelDirty = true;
    private void TickModules(bool force = false)
    {
        if (!IsServer || moduleTargets == null || (!force && Time.realtimeSinceStartupAsDouble < nextModuleSnapshot)) return;
        if (moduleTargets.Any(target => target == null || !SupportsModule(target))) return;
        nextModuleSnapshot = Time.realtimeSinceStartupAsDouble + .25;
        var previous = moduleData.Value.Records;
        var records = new NetworkModuleRecord[moduleTargets.Length];
        int bytes = 0;
        for (int i = 0; i < records.Length; i++)
        {
            var module = moduleTargets[i].TargetHardpoint.InstalledModule;
            var engine = module.GetComponent<EngineModule>(); var generator = module.GetComponent<GeneratorModule>(); var pump = module.GetComponent<PumpModule>();
            var fuel = ModuleFuel(i);
            if (fuel != null && watchedModuleFuel.Add(fuel))
            {
                // Prefab-deserialized item trees need their container event
                // bindings restored before edits can invalidate the snapshot.
                fuel.EnsureContainerStateMatchesDefinition();
                fuel.Changed += ModuleFuelChanged; moduleFuelDirty = true;
            }
            var record = new NetworkModuleRecord { Hardpoint = new FixedString64Bytes(moduleTargets[i].TargetHardpoint.HardpointId),
                Generation = HardpointGeneration(moduleTargets[i].TargetHardpoint),
                Kind = (byte)(engine != null ? 1 : generator != null ? 2 : 3),
                On = engine != null ? engine.IsOn : generator != null ? generator.IsOn : pump.IsOn,
                Revision = previous != null && i < previous.Length ? previous[i].Revision : 1,
                Fuel = moduleFuelDirty || previous == null ? NetworkItemCodec.Capture(fuel) : previous[i].Fuel };
            if (previous != null && i < previous.Length && !record.Equals(previous[i])) record.Revision++;
            bytes += record.Fuel.Bytes?.Length ?? 0;
            if (bytes > NetworkItemData.MaxBytes) throw new InvalidOperationException("Module snapshot byte budget exceeded.");
            records[i] = record;
        }
        moduleFuelDirty = false;
        var snapshot = new NetworkModuleSnapshot { Records = records };
        if (!snapshot.Equals(moduleData.Value)) moduleData.Value = snapshot;
    }
    private void ApplyModules()
    {
        var records = moduleData.Value.Records;
        if (moduleTargets == null || records == null || records.Length != moduleTargets.Length) return;
        var resolver = DontSinkNetworkSession.Instance.GameplayScene;
        var decoded = new ItemInstanceSnapshot[records.Length];
        for (int i = 0; i < records.Length; i++)
        {
            if (records[i].Hardpoint.ToString() != moduleTargets[i].TargetHardpoint.HardpointId)
                return; // Manifest and data may arrive in different frames.
            if (records[i].Generation != HardpointGeneration(moduleTargets[i].TargetHardpoint)) return;
            if (records[i].Revision != appliedModuleRevisions[i]) decoded[i] = NetworkItemCodec.ReadItem(records[i].Fuel, resolver);
        }
        for (int i = 0; i < records.Length; i++)
        {
            if (records[i].Revision == appliedModuleRevisions[i]) continue;
            var module = moduleTargets[i].TargetHardpoint.InstalledModule;
            if (module.GetComponent<EngineModule>() is EngineModule engine) engine.RestorePersistentState(records[i].On, decoded[i], resolver);
            else if (module.GetComponent<GeneratorModule>() is GeneratorModule generator) generator.RestorePersistentState(records[i].On, decoded[i], resolver);
            else module.GetComponent<PumpModule>().RestorePersistentState(records[i].On);
            appliedModuleRevisions[i] = records[i].Revision;
        }
    }
    public void RequestModule(int index, NetworkModuleCommand command, int slot = -1)
    {
        var session = DontSinkNetworkSession.Instance;
        if (!IsSpawned || pendingModule != 0 || !session.HasLocalParticipant || moduleData.Value.Records == null ||
            index < 0 || index >= moduleTargets.Length || index >= moduleData.Value.Records.Length ||
            moduleData.Value.Records[index].Hardpoint.ToString() != moduleTargets[index].TargetHardpoint.HardpointId ||
            moduleData.Value.Records[index].Generation != HardpointGeneration(moduleTargets[index].TargetHardpoint)) return;
        pendingModule = ++moduleSequence; if (pendingModule == 0) pendingModule = ++moduleSequence;
        session.TryGetParticipantActor(NetworkManager.LocalClientId, out var actor);
        var inventory = actor?.GetComponentInChildren<PlayerInventory>(true);
        string itemId = command == NetworkModuleCommand.DepositFuel && inventory != null && slot >= 0 && slot < inventory.HotbarSlotCount
            ? inventory.GetSlot(slot).Instance?.InstanceId ?? "" : "";
        SubmitModuleRpc(pendingModule, new FixedString64Bytes(session.LocalCharacterIdentity), index,
            moduleData.Value.Records[index].Revision, command, slot, new FixedString64Bytes(itemId), moduleData.Value.Records[index].Generation);
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone, Delivery = RpcDelivery.Reliable)]
    public void SubmitModuleRpc(uint id, FixedString64Bytes character, int index, uint revision, NetworkModuleCommand command, int slot,
        FixedString64Bytes expectedItem, uint generation = 1, RpcParams rpc = default)
    {
        var session = DontSinkNetworkSession.Instance; ulong sender = rpc.Receive.SenderClientId;
        if (!session.Registry.TryResolveConnectedSender(sender, out var participant) ||
            !session.TryGetParticipantActor(sender, out var actor) || actor.OwnerClientId != sender ||
            participant.CharacterIdentity != actor.CharacterIdentity || character.ToString() != participant.CharacterIdentity ||
            !session.GameplayScene.IsParticipantReady(sender)) return;
        if (!moduleRequests.TryGetValue(sender, out var gate)) moduleRequests.Add(sender, gate = new NetworkInteractionRequestGate());
        string reason = "Stale or rate-limited module request.";
        var disposition = gate.Begin(id, Time.realtimeSinceStartupAsDouble);
        if (disposition == NetworkInteractionRequestDisposition.Replay) reason = gate.LastReason;
        else if (disposition == NetworkInteractionRequestDisposition.New)
        {
            TickModules(true);
            int target = index >= 0 && index < moduleTargets.Length ? Array.IndexOf(interactionTargets, moduleTargets[index]) : -1;
            reason = "Module changed, unavailable, or move closer.";
            if (target >= 0 && SupportsModule(moduleTargets[index]) && moduleData.Value.Records[index].Revision == revision && moduleData.Value.Records[index].Generation == generation &&
                TryInteractionContext(actor, target, out _) && command >= NetworkModuleCommand.On && command <= NetworkModuleCommand.WithdrawFuel)
            {
                bool ok;
                if (command == NetworkModuleCommand.DepositFuel || command == NetworkModuleCommand.WithdrawFuel)
                    ok = TransferModuleFuel(actor, index, command == NetworkModuleCommand.WithdrawFuel, slot, expectedItem.ToString());
                else
                {
                    var module = moduleTargets[index].TargetHardpoint.InstalledModule; bool on = command == NetworkModuleCommand.On;
                    ok = module.GetComponent<EngineModule>() is EngineModule engine ? engine.SetOn(on) :
                        module.GetComponent<GeneratorModule>() is GeneratorModule generator ? generator.SetOn(on) : module.GetComponent<PumpModule>().SetOn(on);
                }
                reason = ok ? "Done." : "No compatible slot/fuel, or module cannot run.";
                if (ok) { moduleFuelDirty = true; TickModules(true); }
            }
            gate.Complete(reason == "Done.", reason);
        }
        ModuleResultRpc(id, new FixedString128Bytes(reason), RpcTarget.Single(sender, RpcTargetUse.Temp));
    }
    private bool TransferModuleFuel(NetworkPlayerActor actor, int index, bool withdraw, int slotIndex, string expectedItem)
    {
        var inventory = actor.GetComponentInChildren<PlayerInventory>(true); var fuel = ModuleFuel(index);
        if (inventory == null || fuel?.ContainerState == null || slotIndex < 0 || slotIndex >= inventory.HotbarSlotCount) return false;
        var playerSlot = inventory.GetSlot(slotIndex);
        if (!withdraw && playerSlot.Instance?.InstanceId != expectedItem) return false;
        for (int i = 0; i < fuel.ContainerState.SlotCount; i++)
        {
            var fuelSlot = fuel.ContainerState.GetSlot(i); var item = withdraw ? fuelSlot.Instance : playerSlot.Instance;
            if (item == null) continue;
            if (withdraw)
            {
                if (!playerSlot.IsEmpty || !item.Definition.StowableInInventory ||
                    !item.Definition.IsAllowedInParentSlot(PlayerInventory.HotbarIndexToSlotType(slotIndex))) continue;
                fuelSlot.Clear(); playerSlot.Set(item);
            }
            else
            {
                if (!fuelSlot.IsEmpty || !ContainerPlacementUtility.CanPlaceIntoSlot(fuel, i, item)) continue;
                playerSlot.Clear(); fuelSlot.Set(item);
            }
            fuel.ContainerState.NotifyChanged(); inventory.NotifyChanged(); return true;
        }
        return false;
    }
    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void ModuleResultRpc(uint id, FixedString128Bytes reason, RpcParams rpc = default)
    {
        if (rpc.Receive.SenderClientId != NetworkManager.ServerClientId || id != pendingModule) return;
        pendingModule = 0; moduleFeedback = reason.ToString();
    }
    public void DrawModulePanel(Rect panel, Hardpoint hardpoint, GameObject requester)
    {
        var actor = requester != null ? requester.GetComponent<NetworkPlayerActor>() : null;
        int index = moduleTargets != null ? Array.FindIndex(moduleTargets, t => t.TargetHardpoint == hardpoint) : -1;
        if (actor == null || !actor.IsOwner || index < 0 || !SupportsModule(moduleTargets[index]) || moduleData.Value.Records == null || index >= moduleData.Value.Records.Length ||
            moduleData.Value.Records[index].Hardpoint.ToString() != hardpoint.HardpointId ||
            moduleData.Value.Records[index].Generation != HardpointGeneration(hardpoint))
        { GUI.Label(new Rect(panel.x + 15, panel.y + 40, 400, 24), "Module networking unavailable."); return; }
        GUILayout.BeginArea(new Rect(panel.x + 15, panel.y + 15, panel.width - 65, panel.height - 30));
        var module = hardpoint.InstalledModule; var record = ModuleState(index);
        GUILayout.Label(module.Definition.DisplayName + " — " + (record.On ? "On" : "Off"));
        bool old = GUI.enabled; GUI.enabled = old && pendingModule == 0;
        if (GUILayout.Button(record.On ? "Turn off" : "Turn on")) RequestModule(index, record.On ? NetworkModuleCommand.Off : NetworkModuleCommand.On);
        var fuel = ModuleFuel(index);
        if (fuel?.ContainerState != null)
        {
            GUILayout.Label("Fuel: " + string.Join(", ", Enumerable.Range(0, fuel.ContainerState.SlotCount).Select(i => fuel.ContainerState.GetSlot(i).Instance)
                .Where(item => item != null).Select(item => item.Definition.DisplayName + " x" + item.Quantity + " (" + item.CurrentCharges + " charges)")));
            var inventory = actor.GetComponentInChildren<PlayerInventory>(true);
            for (int i = 0; i < inventory.HotbarSlotCount; i++)
            {
                var item = inventory.GetSlot(i).Instance; GUILayout.BeginHorizontal();
                GUILayout.Label("Slot " + (i + 1) + ": " + (item != null ? item.Definition.DisplayName : "Empty"));
                if (GUILayout.Button(item != null ? "Add fuel" : "Take fuel")) RequestModule(index,
                    item != null ? NetworkModuleCommand.DepositFuel : NetworkModuleCommand.WithdrawFuel, i);
                GUILayout.EndHorizontal();
            }
        }
        GUI.enabled = old; GUILayout.Label(moduleFeedback ?? "Commands use your own inventory; fuel stays on the host.");
        GUILayout.EndArea();
    }
    private void ReleaseModules()
    {
        foreach (var item in watchedModuleFuel) item.Changed -= ModuleFuelChanged;
        watchedModuleFuel.Clear(); moduleRequests.Clear();
    }
}
