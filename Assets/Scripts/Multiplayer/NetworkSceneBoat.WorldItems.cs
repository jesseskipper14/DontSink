using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public struct NetworkLooseItem : INetworkSerializable, IEquatable<NetworkLooseItem>
{
    public uint Entity, Revision;
    public NetworkItemData Item;
    public NetworkPlayerPose Pose;
    public Vector3 Scale;
    public bool Owned, Secured;
    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    { s.SerializeValue(ref Entity); s.SerializeValue(ref Revision); Item.NetworkSerialize(s); Pose.NetworkSerialize(s); s.SerializeValue(ref Scale); s.SerializeValue(ref Owned); s.SerializeValue(ref Secured); }
    public bool Equals(NetworkLooseItem other) => Entity == other.Entity && Revision == other.Revision && Item.Equals(other.Item) && Pose.Equals(other.Pose) && Scale == other.Scale && Owned == other.Owned && Secured == other.Secured;
}
public struct NetworkLooseItems : INetworkSerializable, IEquatable<NetworkLooseItems>
{
    public NetworkLooseItem[] Items;
    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        int count = Items?.Length ?? 0; s.SerializeValue(ref count);
        if (count < 0 || count > 128) throw new InvalidOperationException("Loose item capacity exceeded.");
        if (s.IsReader) Items = new NetworkLooseItem[count];
        int bytes = 0;
        for (int i = 0; i < count; i++) { Items[i].NetworkSerialize(s); bytes += Items[i].Item.Bytes?.Length ?? 0;
            if (bytes > NetworkItemData.MaxBytes) throw new InvalidOperationException("Loose item byte budget exceeded."); }
    }
    public bool Equals(NetworkLooseItems other)
    {
        int count = Items?.Length ?? 0;
        if (count != (other.Items?.Length ?? 0)) return false;
        for (int i = 0; i < count; i++) if (!Items[i].Equals(other.Items[i])) return false;
        return true;
    }
}
public sealed partial class NetworkSceneBoat
{
    private readonly NetworkVariable<NetworkLooseItems> looseItems = new();
    private readonly Dictionary<uint, WorldItem> looseRoots = new();
    private readonly Dictionary<WorldItem, uint> looseIds = new();
    private readonly Dictionary<uint, NetworkLooseItem> looseCache = new();
    private readonly Dictionary<uint, NetworkPlayerPoseHistory> looseHistory = new();
    private readonly Dictionary<ulong, NetworkInteractionRequestGate> looseRequests = new();
    private readonly Dictionary<ulong, LooseHold> looseHolds = new();
    private sealed class LooseHold { public uint Entity; public ulong Fingerprint; public double Started, LastSeen; public bool Fired; }
    private uint nextLooseId, looseSequence, pendingLoose;
    private double nextLooseScan, nextLooseSnapshot, nextLooseContents, nextLoosePickup;
    private WorldItem localLoosePickup;
    private float localLooseElapsed;
    private bool initializedLoose;
    public string WorldItemFeedback { get; private set; }
    public bool WorldItemRequestPending => pendingLoose != 0;
    public IEnumerable<WorldItem> LooseWorldItems => looseRoots.Values.Where(w => w != null && w.Instance != null);
    public uint WorldItemEntity(WorldItem item) => item != null && looseIds.TryGetValue(item, out var id) ? id : 0;
    public static bool SupportsLooseItem(ItemDefinition definition)
    {
        var p = definition?.WorldPrefab;
        if (p == null || p.GetComponentsInChildren<Compartment>(true).Length > 0 || p.GetComponent<TetherDeploymentModule>() != null || p.GetComponentInChildren<DivingBellAirVolume>(true) != null || p.GetComponentInChildren<MoneyChestState>(true) != null) return false;
        var bodies = p.GetComponentsInChildren<Rigidbody2D>(true);
        return bodies.Length == 0 || bodies.Length == 1 && bodies[0].gameObject == p.gameObject;
    }
    private bool IsLooseSceneItem(WorldItem item) => item != null && item.Instance != null && SupportsLooseItem(item.Item) &&
        item.gameObject.scene == SceneManager.GetActiveScene() && item.GetComponentInParent<NetworkPlayerActor>() == null &&
        item.GetComponentInParent<InstalledModule>() == null && item.GetComponent<WorldItemAttachmentRegistry>()?.ActiveAttachmentCount is not > 0;
    private void InitializeLooseItems()
    {
        if (initializedLoose) return;
        initializedLoose = true;
        looseItems.OnValueChanged += LooseChanged;
        if (!IsServer)
            foreach (var item in UnityEngine.Object.FindObjectsByType<WorldItem>(FindObjectsSortMode.None))
                if (IsLooseSceneItem(item)) { item.gameObject.SetActive(false); Destroy(item.gameObject); }
        else ScanLooseItems();
    }
    private void ScanLooseItems()
    {
        foreach (var item in UnityEngine.Object.FindObjectsByType<WorldItem>(FindObjectsSortMode.None))
            if (IsLooseSceneItem(item) && !looseIds.ContainsKey(item)) RegisterLooseItem(item);
    }
    private void RegisterLooseItem(WorldItem item)
    {
        if (looseRoots.Count >= 128) throw new InvalidOperationException("Loose item capacity exceeded.");
        uint id = ++nextLooseId; looseRoots.Add(id, item); looseIds.Add(item, id);
    }
    private void TickLooseItems(bool force = false)
    {
        if (!IsServer) return;
        InitializeLooseItems(); double now = Time.realtimeSinceStartupAsDouble;
        TickLooseHolds(now);
        foreach (var pair in looseRoots.ToArray())
            if (pair.Value == null || pair.Value.Instance == null || !pair.Value.gameObject.activeInHierarchy)
            { looseRoots.Remove(pair.Key); if (!ReferenceEquals(pair.Value, null)) looseIds.Remove(pair.Value); looseCache.Remove(pair.Key); }
        if (force || now >= nextLooseScan) { nextLooseScan = now + .25; ScanLooseItems(); }
        if (!force && now < nextLooseSnapshot) return;
        nextLooseSnapshot = now + .05;
        bool contents = force || now >= nextLooseContents;
        if (contents) nextLooseContents = now + .25;
        var records = new List<NetworkLooseItem>(); int bytes = 0;
        foreach (var pair in looseRoots)
        {
            var item = pair.Value; looseCache.TryGetValue(pair.Key, out var previous);
            var data = contents || previous.Item.IsEmpty ? NetworkItemCodec.Capture(item.Instance) : previous.Item;
            bytes += data.Bytes.Length;
            if (bytes > NetworkItemData.MaxBytes) throw new InvalidOperationException("Loose item byte budget exceeded.");
            var record = new NetworkLooseItem { Entity = pair.Key, Revision = previous.Revision == 0 ? 1 : previous.Revision + (data.Equals(previous.Item) ? 0u : 1u), Item = data,
                Pose = new NetworkPlayerPose { Position = item.transform.position, Rotation = item.transform.eulerAngles.z, Time = NetworkManager.ServerTime.Time, Facing = 1 },
                Scale = item.transform.lossyScale,
                Owned = item.GetComponent<BoatOwnedItem>()?.OwningBoat == boat, Secured = item.GetComponent<BoatSecuredItem>()?.IsSecured == true };
            records.Add(record); looseCache[pair.Key] = record;
        }
        looseItems.Value = new NetworkLooseItems { Items = records.ToArray() };
    }
    private void LooseChanged(NetworkLooseItems old, NetworkLooseItems value)
    {
        if (IsServer) return;
        foreach (var item in value.Items ?? Array.Empty<NetworkLooseItem>())
        {
            if (!looseHistory.TryGetValue(item.Entity, out var history)) looseHistory.Add(item.Entity, history = new NetworkPlayerPoseHistory());
            history.Add(item.Pose);
        }
    }
    private void ApplyLooseItems()
    {
        InitializeLooseItems(); var records = looseItems.Value.Items;
        if (records == null) return;
        var ids = new HashSet<uint>(records.Select(r => r.Entity));
        foreach (var pair in looseRoots.ToArray())
            if (!ids.Contains(pair.Key)) { if (pair.Value != null) { looseIds.Remove(pair.Value); pair.Value.gameObject.SetActive(false); Destroy(pair.Value.gameObject); }
                looseRoots.Remove(pair.Key); looseCache.Remove(pair.Key); looseHistory.Remove(pair.Key); }
        var resolver = DontSinkNetworkSession.Instance.GameplayScene;
        foreach (var record in records)
        {
            bool exists = looseRoots.TryGetValue(record.Entity, out var root) && root != null;
            looseCache.TryGetValue(record.Entity, out var previous);
            if (!exists || previous.Revision != record.Revision)
            {
                var item = ItemInstance.FromSnapshot(NetworkItemCodec.ReadItem(record.Item, resolver), resolver);
                if (!SupportsLooseItem(item?.Definition)) throw new InvalidOperationException("Unsupported loose item snapshot.");
                if (!exists)
                {
                    root = Instantiate(item.Definition.WorldPrefab, record.Pose.Position, Quaternion.Euler(0, 0, record.Pose.Rotation));
                    root.Initialize(item); ConfigureReplica(root.gameObject);
                    foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                        if (mb is BoatSecuredItem || mb is BoatOwnedItemEscapeTracker || mb is ThrownCargoPhysics) mb.enabled = false;
                    looseRoots[record.Entity] = root; looseIds[root] = record.Entity;
                    if (!looseHistory.ContainsKey(record.Entity)) { var history = new NetworkPlayerPoseHistory(); history.Reset(record.Pose); looseHistory.Add(record.Entity, history); }
                }
                else root.Initialize(item);
            }
            if (!looseCache.ContainsKey(record.Entity) || previous.Owned != record.Owned)
                WorldItemDropUtility.ApplyReplicaBoatOwnership(root, record.Owned ? boat : null);
            var pose = looseHistory[record.Entity].Sample(NetworkManager.ServerTime.Time - .1);
            root.transform.localScale = record.Scale;
            root.transform.SetPositionAndRotation(pose.Position, Quaternion.Euler(0, 0, pose.Rotation));
            looseCache[record.Entity] = record;
        }
    }
    public bool IsReplicatedSecured(WorldItem item) => !IsServer && looseIds.TryGetValue(item, out var id) && looseCache.TryGetValue(id, out var record) && record.Secured;
    private bool HasLooseBudget(ItemInstance adding)
    {
        if (looseRoots.Count >= 128 || !SupportsLooseItem(adding?.Definition)) return false;
        int bytes = NetworkItemCodec.Capture(adding).Bytes.Length;
        foreach (var item in LooseWorldItems) bytes += NetworkItemCodec.Capture(item.Instance).Bytes.Length;
        return bytes <= NetworkItemData.MaxBytes;
    }
    public void RequestWorldDrop(NetworkInventoryAddress source, ulong fingerprint)
    {
        if (!IsSpawned || WorldItemRequestPending) return;
        pendingLoose = ++looseSequence;
        LooseDropRpc(pendingLoose, source, fingerprint);
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void LooseDropRpc(uint sequence, NetworkInventoryAddress source, ulong fingerprint, RpcParams rpc = default)
    {
        if (!TryLooseRequester(rpc.Receive.SenderClientId, sequence, out var actor, out var gate)) return;
        string reason = "Item changed, unsupported drop, or source unavailable.";
        var plan = new NetworkInventoryModel.Plan(actor); var slot = source.Kind == NetworkInventoryKind.Hotbar || source.Kind == NetworkInventoryKind.Equipment ? plan.Resolve(source) : null;
        var item = slot?.Actual;
        if (item != null && item.Definition.Droppable && NetworkInventoryModel.Fingerprint(item) == fingerprint && HasLooseBudget(item) && SeatFor(actor) == null &&
            actor.GetComponentInChildren<HandheldSoundingLineController>(true)?.IsActiveDeployedSounder(item) != true)
        {
            int amount = source.Kind == NetworkInventoryKind.Hotbar ? 1 : item.Quantity;
            var data = slot.Read(); var dropped = ItemInstance.FromSnapshot(data, plan.Resolver).ToSnapshot();
            if (amount < item.Quantity)
            {
                if (!item.CanSplit) { LooseResult(sequence, "Item cannot split.", actor.OwnerClientId); return; }
                dropped.instanceId = Guid.NewGuid().ToString("N"); dropped.quantity = amount;
                dropped.retainedWaterVolume = data.retainedWaterVolume * amount / data.quantity;
                data.quantity -= amount; data.retainedWaterVolume -= dropped.retainedWaterVolume;
            }
            else slot.Write(null);
            if (plan.TryPrepare(out var commit, out reason))
            {
                reason = "Cannot release this item here.";
                var input = actor.GetComponentInChildren<PlayerInventoryInput>(true); Vector3 origin = input.GetThrowWorldPosition();
                if (Vector2.Distance(origin, actor.AuthoritativePosition) <= 2f && WorldItemDropUtility.TryDrop(ItemInstance.FromSnapshot(dropped, plan.Resolver), origin, actor.gameObject, out var world))
                { commit(); RegisterLooseItem(world); TickLooseItems(true); reason = "Dropped."; }
            }
        }
        gate.Complete(reason == "Dropped.", reason); LooseResult(sequence, reason, actor.OwnerClientId);
    }
    private bool TryLooseRequester(ulong sender, uint sequence, out NetworkPlayerActor actor, out NetworkInteractionRequestGate gate)
    {
        actor = null; gate = null; var session = DontSinkNetworkSession.Instance;
        if (!session.Registry.TryResolveConnectedSender(sender, out var participant) || !session.TryGetParticipantActor(sender, out actor) || actor.OwnerClientId != sender ||
            actor.CharacterIdentity != participant.CharacterIdentity || !session.GameplayScene.IsParticipantReady(sender)) return false;
        if (!looseRequests.TryGetValue(sender, out gate)) looseRequests.Add(sender, gate = new NetworkInteractionRequestGate());
        var disposition = gate.Begin(sequence, Time.realtimeSinceStartupAsDouble);
        if (disposition != NetworkInteractionRequestDisposition.New)
            LooseResult(sequence, disposition == NetworkInteractionRequestDisposition.Replay ? gate.LastReason : "Stale or rate-limited item request.", sender);
        return disposition == NetworkInteractionRequestDisposition.New;
    }
    private void LooseResult(uint sequence, string reason, ulong sender) => LooseResultRpc(sequence, new FixedString128Bytes(reason), RpcTarget.Single(sender, RpcTargetUse.Temp));
    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void LooseResultRpc(uint sequence, FixedString128Bytes reason, RpcParams rpc = default)
    {
        if (rpc.Receive.SenderClientId != NetworkManager.ServerClientId) return;
        if (sequence == pendingLoose) pendingLoose = 0;
        WorldItemFeedback = reason.ToString(); GameMessageService.PostInfo(WorldItemFeedback);
    }
    private void ProcessLoosePickup(Interactor2D interactor, in InteractContext context, InteractionIntent intent)
    {
        interactor.TryResolveBestPickup(context, out var pickup); var item = pickup as WorldItem;
        bool held = item != null && WorldItemEntity(item) != 0 && (intent.PickupHeld || intent.PickupPressed);
        if (!held || item != localLoosePickup) localLooseElapsed = 0;
        if (held) localLooseElapsed += Time.deltaTime;
        if (item != null) interactor.PresentNetworkPickup(held ? item : null, localLooseElapsed);
        if (held && (item != localLoosePickup || Time.realtimeSinceStartupAsDouble >= nextLoosePickup) || !held && localLoosePickup != null)
        {
            var target = held ? item : localLoosePickup;
            LoosePickupRpc(++looseSequence, WorldItemEntity(target), held, held ? NetworkInventoryModel.Fingerprint(item.Instance) : 0);
            nextLoosePickup = Time.realtimeSinceStartupAsDouble + .12;
        }
        localLoosePickup = held ? item : null;
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void LoosePickupRpc(uint sequence, uint entity, bool held, ulong fingerprint, RpcParams rpc = default)
    {
        ulong sender = rpc.Receive.SenderClientId;
        if (!TryLooseRequester(sender, sequence, out var actor, out var gate)) return;
        if (!held) { looseHolds.Remove(sender); gate.Complete(true, "Released."); return; }
        if (!looseRoots.TryGetValue(entity, out var item) || item == null || item.Instance == null || fingerprint != NetworkInventoryModel.Fingerprint(item.Instance) ||
            SeatFor(actor) != null || item.GetComponent<WorldItemAttachmentRegistry>()?.ActiveAttachmentCount is > 0 || !item.CanPickup(new InteractContext(actor.gameObject, actor.transform, actor.AuthoritativePosition, Vector2.right)))
        { looseHolds.Remove(sender); gate.Complete(false, "Item changed, secured, out of reach, or no inventory space."); LooseResult(sequence, gate.LastReason, sender); return; }
        double now = Time.realtimeSinceStartupAsDouble;
        if (!looseHolds.TryGetValue(sender, out var hold) || hold.Entity != entity || hold.Fingerprint != fingerprint || now - hold.LastSeen > .3)
            looseHolds[sender] = hold = new LooseHold { Entity = entity, Fingerprint = fingerprint, Started = now };
        hold.LastSeen = now; gate.Complete(true, "Holding."); TickLooseHolds(now);
    }
    private void TickLooseHolds(double now)
    {
        foreach (var pair in looseHolds.ToArray())
        {
            var hold = pair.Value;
            if (hold.Fired) continue;
            if (now - hold.LastSeen > .3 || !looseRoots.TryGetValue(hold.Entity, out var item) || item == null || item.Instance == null ||
                NetworkInventoryModel.Fingerprint(item.Instance) != hold.Fingerprint || !DontSinkNetworkSession.Instance.TryGetParticipantActor(pair.Key, out var actor) || SeatFor(actor) != null)
            { looseHolds.Remove(pair.Key); continue; }
            var context = new InteractContext(actor.gameObject, actor.transform, actor.AuthoritativePosition, Vector2.right);
            if (item.GetComponent<WorldItemAttachmentRegistry>()?.ActiveAttachmentCount is > 0 || !item.CanPickup(context)) { looseHolds.Remove(pair.Key); continue; }
            float duration = item.PickupMode == PickupInteractionMode.Instant ? 0 : item.PickupHoldDuration;
            if (now - hold.Started < duration) continue;
            hold.Fired = true; item.Pickup(context);
            LooseResult(0, item.Instance == null ? "Picked up." : "Pickup refused.", pair.Key);
        }
    }
    public bool TryLooseHover(in InteractContext context, ref InteractionHoverTarget hover)
    {
        float best = hover.IsValid ? ((hover.Interact?.InteractionPriority ?? hover.Pickup?.PickupPriority ?? 0) * 10 - Vector2.Distance(context.Origin, ClosestInteractionPoint(hover.SourceCollider, context.Origin))) : float.NegativeInfinity;
        foreach (var item in LooseWorldItems)
            foreach (var col in item.GetComponentsInChildren<Collider2D>())
            {
                if (!col.enabled || Vector2.Distance(context.AimWorld, ClosestInteractionPoint(col, context.AimWorld)) > .08f) continue;
                float distance = Vector2.Distance(context.Origin, ClosestInteractionPoint(col, context.Origin));
                var candidate = new InteractionHoverTarget(col, item, null, item, null, null, item, item, null, item, (IInteractionDetailProvider[])null, null);
                float score = item.PickupPriority * 10 - distance;
                if (distance <= 4 && score > best && context.InteractorGO.GetComponent<Interactor2D>().PassesHoverSelectionPolicy(candidate, context)) { hover = candidate; best = score; }
            }
        return hover.IsValid;
    }
    private void ReleaseLooseItems()
    {
        looseItems.OnValueChanged -= LooseChanged;
        if (!IsServer) foreach (var item in looseRoots.Values) if (item != null) Destroy(item.gameObject);
        throwHolds.Clear();
        looseRoots.Clear(); looseIds.Clear(); looseCache.Clear(); looseHistory.Clear(); looseRequests.Clear(); looseHolds.Clear();
    }
}
