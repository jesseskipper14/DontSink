using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public enum SharedInteractionCommand : byte
{
    GeneratorOn = 1, GeneratorOff, BurnTestFuel, ClaimSeat, ReleaseSeat, PickupCargo, DropCargo,
    WithdrawFuel, DepositFuel
}

public struct SharedInteractionState : INetworkSerializable, IEquatable<SharedInteractionState>
{
    public uint Incarnation, ModuleRevision, StationRevision, CargoRevision;
    public bool GeneratorOn, SeatOccupied, CargoHeld;
    public ulong SeatOwner, CargoOwner;
    public float Power, CargoRotation;
    public int FuelCharges;
    public Vector2 CargoPosition;
    public double Time;
    public FixedString64Bytes CargoIdentity, FuelIdentity, FuelContainerIdentity;
    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref Incarnation); s.SerializeValue(ref ModuleRevision);
        s.SerializeValue(ref StationRevision); s.SerializeValue(ref CargoRevision);
        s.SerializeValue(ref GeneratorOn); s.SerializeValue(ref SeatOccupied); s.SerializeValue(ref CargoHeld);
        s.SerializeValue(ref SeatOwner); s.SerializeValue(ref CargoOwner);
        s.SerializeValue(ref Power); s.SerializeValue(ref FuelCharges);
        s.SerializeValue(ref CargoPosition); s.SerializeValue(ref CargoRotation); s.SerializeValue(ref Time);
        s.SerializeValue(ref CargoIdentity); s.SerializeValue(ref FuelIdentity); s.SerializeValue(ref FuelContainerIdentity);
    }
    public bool Equals(SharedInteractionState other) => Incarnation == other.Incarnation &&
        ModuleRevision == other.ModuleRevision && StationRevision == other.StationRevision &&
        CargoRevision == other.CargoRevision && GeneratorOn == other.GeneratorOn &&
        SeatOccupied == other.SeatOccupied && SeatOwner == other.SeatOwner &&
        CargoHeld == other.CargoHeld && CargoOwner == other.CargoOwner && Power == other.Power &&
        FuelCharges == other.FuelCharges && CargoPosition == other.CargoPosition &&
        CargoRotation == other.CargoRotation && Time == other.Time && CargoIdentity == other.CargoIdentity &&
        FuelIdentity == other.FuelIdentity && FuelContainerIdentity == other.FuelContainerIdentity;
}

// MP.3's contained vertical slice: server-owned object, existing domain operations,
// explicit actor lookup, optimistic per-target revisions, passive late-join replicas.
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkSharedInteractionWorld : NetworkBehaviour, IItemDefinitionResolver
{
    [SerializeField] private GameObject generatorPrefab;
    [SerializeField] private GameObject chairPrefab;
    [SerializeField] private ItemDefinition cargoDefinition, fuelDefinition, fuelContainerDefinition;
    [SerializeField] private ItemDefinitionCatalog itemCatalog;
    private readonly NetworkVariable<SharedInteractionState> state = new();
    private readonly NetworkVariable<NetworkItemData> cargoData = new(), fuelData = new();
    private readonly Dictionary<ulong, NetworkInteractionRequestGate> requests = new();
    private readonly NetworkPlayerPoseHistory cargoHistory = new();
    private GeneratorModule generator;
    private SeatController2D seat;
    private BoatPowerState power;
    private WorldItem cargo;
    private ItemInstance cargoItem;
    private Transform stationAnchor;
    private GameObject content;
    private Material unlit;
    private NetworkBoatPlaytest carrier;
    private SharedInteractionState authoritative;
    private uint nextRequest, pendingRequest;
    private double nextPublish;
    private bool replicaDirty;
    private bool cargoDataDirty = true, fuelDataDirty = true, cargoReplicaDirty = true, fuelReplicaDirty = true;
    private ItemInstance watchedFuel;
    private uint acknowledgedModuleRevision, acknowledgedStationRevision, acknowledgedCargoRevision;
    private string feedback = "Walk to a station to interact.";
    public SharedInteractionState CurrentState => state.Value;
    public GeneratorModule Generator => generator;
    public WorldItem Cargo => cargo;
    public SeatController2D Seat => seat;
    public string Feedback => feedback;
    public Vector2 GeneratorPosition => generator.transform.position;
    public Vector2 SeatPosition => seat != null && seat.SeatPoint != null ? seat.SeatPoint.position : stationAnchor.position;

    public override void OnNetworkSpawn()
    {
        DontSinkNetworkSession.Instance.BindInteractionWorld(this);
        content = new GameObject("Shared station content");
        content.transform.SetParent(transform, false);
        content.SetActive(false);
        unlit = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default"));
        carrier = DontSinkNetworkSession.Instance.BoatWorld;
        if (carrier != null)
        {
            content.transform.SetParent(carrier.Boat.transform, false);
            content.transform.localPosition = carrier.DeckLocalPoint;
        }
        power = carrier != null ? carrier.Boat.GetComponent<BoatPowerState>() : content.AddComponent<BoatPowerState>();
        generator = MakeProp(generatorPrefab, new Vector2(-4, 0)).GetComponent<GeneratorModule>();
        generator.enabled = true;
        generator.BindPowerSource(power);
        var chair = MakeProp(chairPrefab, new Vector2(4, 0));
        seat = chair.GetComponent<SeatController2D>();
        if (seat == null) seat = chair.AddComponent<SeatController2D>();
        // The authored pilot chair supplies its seat point; the network station
        // uses SeatController2D without opening an unreplicated piloting cartridge.
        var anchor = new GameObject("MP3 seat point");
        anchor.transform.SetParent(chair.transform, false);
        anchor.transform.localPosition = Vector3.up;
        stationAnchor = anchor.transform;
        seat.SetRuntimeFallbackSeat(stationAnchor, true);
        seat.enabled = IsServer;
        seat.OccupantEjected += SeatEjected;
        state.OnValueChanged += StateChanged;
        cargoData.OnValueChanged += CargoDataChanged;
        fuelData.OnValueChanged += FuelDataChanged;
        if (IsServer)
        {
            authoritative.Incarnation = unchecked((uint)Guid.NewGuid().GetHashCode());
            if (authoritative.Incarnation == 0) authoritative.Incarnation = 1;
            authoritative.ModuleRevision = authoritative.StationRevision = authoritative.CargoRevision = 1;
            var container = ItemInstance.Create(fuelContainerDefinition);
            container.ContainerState.GetSlot(0).Set(ItemInstance.Create(fuelDefinition));
            generator.RestorePersistentState(false, container.ToSnapshot(), this);
            watchedFuel = generator.FuelContainerItem;
            watchedFuel.Changed += FuelDirty;
            authoritative.FuelContainerIdentity = container.InstanceId;
            authoritative.FuelIdentity = container.ContainerState.GetSlot(0).Instance.InstanceId;
            cargoItem = ItemInstance.Create(cargoDefinition);
            cargoItem.Changed += CargoDirty;
            authoritative.CargoIdentity = cargoItem.InstanceId;
            cargo = SpawnCargo(cargoItem, content.transform.position + Vector3.up);
            Publish();
        }
        else
        {
            replicaDirty = true;
            cargoHistory.Reset(Pose(state.Value));
        }
        content.SetActive(true);
        foreach (var actor in DontSinkNetworkSession.Instance.SpawnedPlayers) Present(actor);
    }

    public ItemDefinition Resolve(string id)
    {
        if (cargoDefinition.ItemId == id) return cargoDefinition;
        if (fuelDefinition.ItemId == id) return fuelDefinition;
        if (fuelContainerDefinition.ItemId == id) return fuelContainerDefinition;
        return itemCatalog != null ? itemCatalog.Resolve(id) : id == "item_cartographic_chart"
            ? Resources.Load<ItemDefinition>("Cartography/item_cartographic_chart") : null;
    }

    private GameObject MakeProp(GameObject prefab, Vector2 localPosition)
    {
        var prop = Instantiate(prefab, content.transform);
        prop.transform.localPosition = localPosition;
        prop.transform.localRotation = Quaternion.identity;
        // Leave authored sprites/colliders in place; only the explicit domain
        // components are allowed to simulate this connected test.
        foreach (var component in prop.GetComponentsInChildren<MonoBehaviour>(true)) component.enabled = false;
        foreach (var body in prop.GetComponentsInChildren<Rigidbody2D>(true)) body.simulated = false;
        foreach (var collider in prop.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
        foreach (var renderer in prop.GetComponentsInChildren<SpriteRenderer>(true)) renderer.sharedMaterial = unlit;
        return prop;
    }

    private WorldItem SpawnCargo(ItemInstance item, Vector3 position)
    {
        // Instantiate inactive so restore and replica policy precede callbacks.
        var staging = new GameObject("_MP3CargoStaging");
        staging.SetActive(false);
        staging.transform.SetParent(content.transform, false);
        var instance = Instantiate(cargoDefinition.WorldPrefab, position, Quaternion.identity, staging.transform);
        foreach (var component in instance.GetComponentsInChildren<MonoBehaviour>(true)) component.enabled = component is WorldItem;
        foreach (var body in instance.GetComponentsInChildren<Rigidbody2D>(true))
        {
            body.bodyType = IsServer ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
            body.simulated = IsServer;
            body.interpolation = IsServer ? RigidbodyInterpolation2D.Interpolate : RigidbodyInterpolation2D.None;
        }
        foreach (var renderer in instance.GetComponentsInChildren<SpriteRenderer>(true)) renderer.sharedMaterial = unlit;
        instance.Initialize(item);
        if (carrier != null)
        {
            var owned = instance.GetComponent<BoatOwnedItem>() ?? instance.gameObject.AddComponent<BoatOwnedItem>();
            owned.AssignToBoat(carrier.Boat);
            instance.GetComponent<BoatOwnedItemLayerPolicy>()?.ApplyNow();
            instance.GetComponent<BoatOwnedItemVisualPolicy>()?.ApplyNow();
        }
        instance.transform.SetParent(content.transform, true);
        Destroy(staging);
        return instance;
    }

    public uint RevisionFor(SharedInteractionCommand command)
        => IsModuleCommand(command) ? (IsServer ? authoritative : state.Value).ModuleRevision :
            command <= SharedInteractionCommand.ReleaseSeat ? (IsServer ? authoritative : state.Value).StationRevision :
                (IsServer ? authoritative : state.Value).CargoRevision;

    private static bool IsModuleCommand(SharedInteractionCommand command) => command <= SharedInteractionCommand.BurnTestFuel ||
        command == SharedInteractionCommand.WithdrawFuel || command == SharedInteractionCommand.DepositFuel;
    public void Request(SharedInteractionCommand command, int inventorySlot = 0)
    {
        var session = DontSinkNetworkSession.Instance;
        if (!IsSpawned || pendingRequest != 0 || !session.HasLocalParticipant) return;
        pendingRequest = ++nextRequest;
        if (pendingRequest == 0) pendingRequest = ++nextRequest;
        feedback = "Waiting for host...";
        uint revision = RevisionFor(command);
        uint acknowledged = IsModuleCommand(command) ? acknowledgedModuleRevision :
            command <= SharedInteractionCommand.ReleaseSeat ? acknowledgedStationRevision : acknowledgedCargoRevision;
        if (unchecked((int)(acknowledged - revision)) > 0) revision = acknowledged;
        SubmitRequestRpc(pendingRequest, new FixedString64Bytes(session.LocalCharacterIdentity),
            state.Value.Incarnation, revision, command, inventorySlot);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone, Delivery = RpcDelivery.Reliable)]
    public void SubmitRequestRpc(uint id, FixedString64Bytes character, uint incarnation, uint revision,
        SharedInteractionCommand command, int inventorySlot = 0, RpcParams rpc = default)
    {
        ulong sender = rpc.Receive.SenderClientId;
        var session = DontSinkNetworkSession.Instance;
        // Identity comes from the transport, not a client-supplied player object.
        if (session?.Registry == null || !session.Registry.TryResolveConnectedSender(sender, out var participant) ||
            !session.TryGetParticipantActor(sender, out var actor) ||
            actor.OwnerClientId != sender || participant.CharacterIdentity != character.ToString() ||
            actor.CharacterIdentity != participant.CharacterIdentity)
        { Reply(sender, id, false, "Invalid participant."); return; }
        if (!requests.TryGetValue(sender, out var gate)) requests.Add(sender, gate = new NetworkInteractionRequestGate());
        var disposition = gate.Begin(id, Time.realtimeSinceStartupAsDouble);
        if (disposition != NetworkInteractionRequestDisposition.New)
        {
            Reply(sender, id, disposition == NetworkInteractionRequestDisposition.Replay && gate.LastSucceeded,
                disposition == NetworkInteractionRequestDisposition.Stale ? "Stale request." : gate.LastReason);
            return;
        }
        string reason;
        bool succeeded = false;
        if (incarnation != authoritative.Incarnation) reason = "Station was replaced.";
        else if (command < SharedInteractionCommand.GeneratorOn || command > SharedInteractionCommand.DepositFuel) reason = "Unknown action.";
        else if (revision != RevisionFor(command)) reason = "Object changed; try again.";
        else succeeded = Execute(sender, actor, command, inventorySlot, out reason);
        gate.Complete(succeeded, reason);
        if (succeeded) Publish();
        Reply(sender, id, succeeded, reason);
    }

    private bool Execute(ulong sender, NetworkPlayerActor actor, SharedInteractionCommand command, int inventorySlot, out string reason)
    {
        reason = "Action unavailable.";
        Vector2 origin = actor.AuthoritativePosition;
        bool release = command == SharedInteractionCommand.ReleaseSeat || command == SharedInteractionCommand.DropCargo;
        Vector2 target = IsModuleCommand(command) ? GeneratorPosition :
            command <= SharedInteractionCommand.ReleaseSeat ? SeatPosition : (Vector2)(cargo != null ? cargo.transform.position : actor.transform.position);
        if (!release && (origin - target).sqrMagnitude > 2.25f * 2.25f)
        { reason = "Move closer."; return false; }
        if (command == SharedInteractionCommand.WithdrawFuel || command == SharedInteractionCommand.DepositFuel)
            return TransferFuel(actor, command == SharedInteractionCommand.WithdrawFuel, inventorySlot, out reason);
        if (command <= SharedInteractionCommand.BurnTestFuel)
        {
            bool ok = command == SharedInteractionCommand.BurnTestFuel ? generator.TryConsumeFuelCharges(1) :
                generator.SetOn(command == SharedInteractionCommand.GeneratorOn);
            if (!ok) { reason = "Generator has no usable fuel."; return false; }
        }
        else if (command == SharedInteractionCommand.ClaimSeat)
        {
            if (authoritative.SeatOccupied || !seat.TrySeat(actor.gameObject))
            { reason = "Station is occupied."; return false; }
            authoritative.SeatOccupied = true; authoritative.SeatOwner = sender;
            authoritative.StationRevision++;
        }
        else if (command == SharedInteractionCommand.ReleaseSeat)
        {
            if (!authoritative.SeatOccupied || authoritative.SeatOwner != sender)
            { reason = "You do not control this station."; return false; }
            seat.Eject(SeatEjectReason.Manual);
        }
        else if (command == SharedInteractionCommand.PickupCargo)
        {
            var equipment = actor.GetComponentInChildren<PlayerEquipment>(true);
            var context = new InteractContext(actor.gameObject, actor.transform, origin, Vector2.right);
            if (authoritative.CargoHeld || cargo == null || equipment.Get(BottomBarSlotType.Hands) != null || !cargo.CanPickup(context))
            { reason = "Cargo unavailable or hands full."; return false; }
            cargo.Pickup(context);
            if (equipment.Get(BottomBarSlotType.Hands) != cargoItem)
            { reason = "Pickup did not enter hands."; return false; }
            cargo = null;
            authoritative.CargoHeld = true; authoritative.CargoOwner = sender;
            authoritative.CargoRevision++;
        }
        else
        {
            if (!authoritative.CargoHeld || authoritative.CargoOwner != sender)
            { reason = "You are not carrying this cargo."; return false; }
            if (!DropCargo(actor)) { reason = "Could not drop cargo."; return false; }
        }
        reason = "Done.";
        return true;
    }

    private bool TransferFuel(NetworkPlayerActor actor, bool withdraw, int index, out string reason)
    {
        reason = "No compatible empty slot, or fuel unavailable.";
        var inventory = actor.GetComponentInChildren<PlayerInventory>(true);
        var container = generator.FuelContainerItem;
        if (inventory == null || container?.ContainerState == null || index < 0 || index >= inventory.HotbarSlotCount)
        { reason = "Invalid inventory slot."; return false; }
        var playerSlot = inventory.GetSlot(index);
        // Exact whole-instance transfers only. No swaps, merges, partial stacks,
        // client-provided items or cross-player inventory references in this adapter.
        for (int i = 0; i < container.ContainerState.SlotCount; i++)
        {
            var containerSlot = container.ContainerState.GetSlot(i);
            var item = withdraw ? containerSlot.Instance : playerSlot.Instance;
            if (item == null || item.Definition != fuelDefinition || item.Quantity != 1) continue;
            if (withdraw)
            {
                if (!playerSlot.IsEmpty || !item.Definition.StowableInInventory ||
                    !item.Definition.IsAllowedInParentSlot(PlayerInventory.HotbarIndexToSlotType(index))) continue;
                containerSlot.Clear(); playerSlot.Set(item);
            }
            else
            {
                if (!containerSlot.IsEmpty || !ContainerPlacementUtility.CanPlaceIntoSlot(container, i, item)) continue;
                playerSlot.Clear(); containerSlot.Set(item);
            }
            // Notify after both writes: observers never see a temporary duplicate.
            container.ContainerState.NotifyChanged(); inventory.NotifyChanged();
            authoritative.ModuleRevision++;
            reason = withdraw ? "Fuel moved to your hotbar." : "Fuel returned to generator.";
            return true;
        }
        return false;
    }

    private bool DropCargo(NetworkPlayerActor actor)
    {
        var equipment = actor.GetComponentInChildren<PlayerEquipment>(true);
        if (equipment.Get(BottomBarSlotType.Hands) != cargoItem) return false;
        Vector2 origin = actor.AuthoritativePosition;
        // Placement is calculated by the host; clients never submit arbitrary coordinates.
        Vector3 position = origin + Vector2.right * 1.2f;
        if (!WorldItemDropUtility.TryDrop(cargoItem, position, actor.gameObject, out var dropped)) return false;
        equipment.Remove(BottomBarSlotType.Hands);
        cargo = dropped;
        cargo.transform.SetParent(content.transform, true);
        foreach (var component in cargo.GetComponentsInChildren<MonoBehaviour>(true)) component.enabled = component is WorldItem;
        foreach (var renderer in cargo.GetComponentsInChildren<SpriteRenderer>(true)) renderer.sharedMaterial = unlit;
        authoritative.CargoHeld = false;
        authoritative.CargoOwner = 0;
        authoritative.CargoRevision++;
        return true;
    }

    private void SeatEjected(GameObject occupant, SeatEjectReason reason)
    {
        if (!IsServer || !authoritative.SeatOccupied) return;
        authoritative.SeatOccupied = false; authoritative.SeatOwner = 0;
        authoritative.StationRevision++;
    }

    public void ReleaseParticipant(ulong sender)
    {
        if (!IsServer) return;
        if (authoritative.SeatOccupied && authoritative.SeatOwner == sender) seat.Eject(SeatEjectReason.OccupantMissing);
        if (authoritative.CargoHeld && authoritative.CargoOwner == sender)
        {
            if (DontSinkNetworkSession.Instance.TryGetParticipantActor(sender, out var actor)) DropCargo(actor);
            // NGO may despawn the player before the disconnect callback. The host
            // still owns the exact item instance and releases it at its last pose.
            if (authoritative.CargoHeld)
            {
                cargo = SpawnCargo(cargoItem, authoritative.CargoPosition);
                authoritative.CargoHeld = false; authoritative.CargoOwner = 0; authoritative.CargoRevision++;
            }
        }
        requests.Remove(sender);
        Publish();
    }

    private void FixedUpdate()
    {
        if (!IsSpawned || !IsServer) return;
        if (authoritative.SeatOccupied)
        {
            if (!DontSinkNetworkSession.Instance.TryGetParticipantActor(authoritative.SeatOwner, out _))
            { seat.Eject(SeatEjectReason.OccupantMissing); SeatEjected(null, SeatEjectReason.OccupantMissing); }
            else seat.TickSeat();
        }
        if (NetworkManager.ServerTime.Time < nextPublish) return;
        Publish();
        nextPublish = NetworkManager.ServerTime.Time + .05;
    }

    private void Publish()
    {
        if (!IsServer) return;
        if (cargoDataDirty) { cargoDataDirty = false; cargoData.Value = NetworkItemCodec.Capture(cargoItem); }
        if (fuelDataDirty) { fuelDataDirty = false; fuelData.Value = NetworkItemCodec.Capture(generator.FuelContainerItem); }
        bool on = generator.IsOn;
        int charges = generator.TryGetActiveFuelItem(out var fuel) ? fuel.CurrentCharges : 0;
        if (authoritative.GeneratorOn != on || authoritative.FuelCharges != charges) authoritative.ModuleRevision++;
        authoritative.GeneratorOn = on;
        authoritative.Power = power.CurrentPower;
        authoritative.FuelCharges = charges;
        if (cargo != null)
        {
            var body = cargo.GetComponent<Rigidbody2D>();
            authoritative.CargoPosition = body != null ? body.position : (Vector2)cargo.transform.position;
            authoritative.CargoRotation = body != null ? body.rotation : cargo.transform.eulerAngles.z;
        }
        else if (authoritative.CargoHeld && DontSinkNetworkSession.Instance.TryGetParticipantActor(authoritative.CargoOwner, out var holder))
            authoritative.CargoPosition = holder.AuthoritativePosition;
        authoritative.Time = NetworkManager.ServerTime.Time;
        state.Value = authoritative;
    }

    private void StateChanged(SharedInteractionState oldValue, SharedInteractionState value)
    {
        replicaDirty = true;
        if (oldValue.CargoHeld != value.CargoHeld || oldValue.CargoRevision != value.CargoRevision) cargoHistory.Reset(Pose(value));
        else cargoHistory.Add(Pose(value));
    }
    private void CargoDirty() => cargoDataDirty = true;
    private void FuelDirty() => fuelDataDirty = true;
    private void CargoDataChanged(NetworkItemData previous, NetworkItemData value) => cargoReplicaDirty = true;
    private void FuelDataChanged(NetworkItemData previous, NetworkItemData value) => fuelReplicaDirty = true;
    private static NetworkPlayerPose Pose(SharedInteractionState value) => new()
    { Position = value.CargoPosition, Rotation = value.CargoRotation, Facing = 1, Time = value.Time };

    private void LateUpdate()
    {
        if (!IsSpawned) return;
        if (carrier == null && DontSinkNetworkSession.Instance.BoatWorld != null)
        {
            carrier = DontSinkNetworkSession.Instance.BoatWorld;
            content.transform.SetParent(carrier.Boat.transform, false);
            content.transform.localPosition = carrier.DeckLocalPoint;
            power = carrier.Boat.GetComponent<BoatPowerState>();
            generator.BindPowerSource(power);
            fuelReplicaDirty = true;
            replicaDirty = true;
            if (cargo != null)
            {
                (cargo.GetComponent<BoatOwnedItem>() ?? cargo.gameObject.AddComponent<BoatOwnedItem>()).AssignToBoat(carrier.Boat);
                cargo.GetComponent<BoatOwnedItemLayerPolicy>()?.ApplyNow();
                cargo.GetComponent<BoatOwnedItemVisualPolicy>()?.ApplyNow();
            }
        }
        // Actor synchronization may arrive after this object's late-join snapshot.
        if (IsServer) return;
        if (state.Value.Incarnation == 0 || state.Value.CargoIdentity.Length == 0) return;
        if (cargoReplicaDirty && !cargoData.Value.IsEmpty)
        {
            var snapshot = NetworkItemCodec.ReadItem(cargoData.Value, this);
            if (snapshot == null || snapshot.instanceId != state.Value.CargoIdentity.ToString()) return;
            cargoItem = ItemInstance.FromSnapshot(snapshot, this);
            if (cargo != null) cargo.Initialize(cargoItem);
            cargoReplicaDirty = false;
            replicaDirty = true;
        }
        if (cargoItem == null || fuelData.Value.IsEmpty) return;
        if (replicaDirty)
        {
            power.SetCurrentPower(state.Value.Power);
            if (fuelReplicaDirty || generator.IsOn != state.Value.GeneratorOn)
            {
                generator.RestorePersistentState(state.Value.GeneratorOn,
                    NetworkItemCodec.ReadItem(fuelData.Value, this), this);
                fuelReplicaDirty = false;
            }
            if (!state.Value.CargoHeld && cargo == null) cargo = SpawnCargo(cargoItem, state.Value.CargoPosition);
            if (state.Value.CargoHeld && cargo != null) { Destroy(cargo.gameObject); cargo = null; }
            replicaDirty = false;
        }
        if (cargo != null)
        {
            var pose = cargoHistory.Sample(NetworkManager.ServerTime.Time - .1);
            cargo.transform.SetPositionAndRotation(pose.Position, Quaternion.Euler(0, 0, pose.Rotation));
        }
    }

    public void Present(NetworkPlayerActor actor)
    {
        foreach (var visual in actor.GetComponentsInChildren<PlayerHeldItemVisual>(true)) visual.enabled = true;
    }

    private void Reply(ulong sender, uint id, bool success, string reason)
    {
        if (!NetworkManager.ConnectedClients.ContainsKey(sender)) return;
        ResultRpc(id, success, new FixedString128Bytes(reason ?? "Action unavailable."),
            authoritative.ModuleRevision, authoritative.StationRevision, authoritative.CargoRevision,
            RpcTarget.Single(sender, RpcTargetUse.Temp));
    }
    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void ResultRpc(uint id, bool success, FixedString128Bytes reason,
        uint moduleRevision, uint stationRevision, uint cargoRevision, RpcParams rpc = default)
    {
        if (rpc.Receive.SenderClientId != NetworkManager.ServerClientId) return;
        if (id != pendingRequest) return;
        pendingRequest = 0;
        acknowledgedModuleRevision = moduleRevision; acknowledgedStationRevision = stationRevision;
        acknowledgedCargoRevision = cargoRevision;
        feedback = reason.ToString();
    }

    private void OnGUI()
    {
        if (!IsSpawned || DontSinkNetworkSession.Instance == null ||
            !DontSinkNetworkSession.Instance.TryGetParticipantActor(NetworkManager.LocalClientId, out var actor)) return;
        GUILayout.BeginArea(new Rect(10, 10, 285, 320), GUI.skin.box);
        GUILayout.Label("MP.3 — Shared interactions");
        GUILayout.Label("Generator: " + (state.Value.GeneratorOn ? "ON" : "OFF") +
            "   Power: " + state.Value.Power.ToString("F1") + "   Fuel: " + state.Value.FuelCharges);
        GUILayout.Label("Seat: " + (state.Value.SeatOccupied ? "player " + state.Value.SeatOwner : "available"));
        GUILayout.Label("Cargo: " + (state.Value.CargoHeld ? "player " + state.Value.CargoOwner + " carrying" : "on deck"));
        GUILayout.Label(feedback);
        bool previous = GUI.enabled;
        Vector2 origin = actor.transform.position;
        GUI.enabled = previous && pendingRequest == 0 && Vector2.Distance(origin, GeneratorPosition) <= 2.25f;
        if (GUILayout.Button(state.Value.GeneratorOn ? "Stop generator" : "Start generator"))
            Request(state.Value.GeneratorOn ? SharedInteractionCommand.GeneratorOff : SharedInteractionCommand.GeneratorOn);
        if (GUILayout.Button("Burn one test fuel charge")) Request(SharedInteractionCommand.BurnTestFuel);
        GUI.enabled = previous && pendingRequest == 0 && (Vector2.Distance(origin, SeatPosition) <= 2.25f ||
            state.Value.SeatOccupied && state.Value.SeatOwner == NetworkManager.LocalClientId);
        if (GUILayout.Button(state.Value.SeatOccupied && state.Value.SeatOwner == NetworkManager.LocalClientId ? "Leave station" : "Use station"))
            Request(state.Value.SeatOccupied && state.Value.SeatOwner == NetworkManager.LocalClientId ? SharedInteractionCommand.ReleaseSeat : SharedInteractionCommand.ClaimSeat);
        GUI.enabled = previous && pendingRequest == 0 && (state.Value.CargoHeld && state.Value.CargoOwner == NetworkManager.LocalClientId ||
            !state.Value.CargoHeld && Vector2.Distance(origin, state.Value.CargoPosition) <= 2.25f);
        if (GUILayout.Button(state.Value.CargoHeld ? "Drop cargo" : "Pick up cargo"))
            Request(state.Value.CargoHeld ? SharedInteractionCommand.DropCargo : SharedInteractionCommand.PickupCargo);
        GUI.enabled = previous;
        GUILayout.Label("Generator left • cargo center • station right");
        GUILayout.EndArea();
        GUILayout.BeginArea(new Rect(300, 10, 250, 210), GUI.skin.box);
        GUILayout.Label("MP.4 — Shared fuel / personal hotbar");
        var inventory = actor.GetComponentInChildren<PlayerInventory>(true);
        GUI.enabled = previous && pendingRequest == 0 && Vector2.Distance(origin, GeneratorPosition) <= 2.25f;
        if (GUILayout.Button("Take fuel → hotbar slot 1")) Request(SharedInteractionCommand.WithdrawFuel);
        if (GUILayout.Button("Return fuel from slot 1")) Request(SharedInteractionCommand.DepositFuel);
        GUI.enabled = previous;
        var fuel = inventory.GetSlot(0).Instance;
        GUILayout.Label("Your slot 1: " + (fuel == null ? "empty" : fuel.Definition.DisplayName + " • " + fuel.CurrentCharges + " charges"));
        GUILayout.Label("Transfers keep the exact item and its state.");
        GUILayout.EndArea();
    }

    public override void OnNetworkDespawn()
    {
        state.OnValueChanged -= StateChanged;
        cargoData.OnValueChanged -= CargoDataChanged;
        fuelData.OnValueChanged -= FuelDataChanged;
        if (IsServer && cargoItem != null) cargoItem.Changed -= CargoDirty;
        if (watchedFuel != null) watchedFuel.Changed -= FuelDirty;
        if (seat != null) { seat.OccupantEjected -= SeatEjected; seat.Eject(SeatEjectReason.AccessInvalid); }
        if (DontSinkNetworkSession.Instance?.InteractionWorld == this) DontSinkNetworkSession.Instance.BindInteractionWorld(null);
        if (content != null) Destroy(content);
        if (unlit != null) Destroy(unlit);
    }
}
