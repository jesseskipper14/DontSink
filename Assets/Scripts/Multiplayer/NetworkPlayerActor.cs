using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Survival.Attributes;

public struct NetworkPlayerPose : INetworkSerializable, IEquatable<NetworkPlayerPose>
{
    public Vector2 Position;
    public float Rotation, Facing;
    public double Time;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref Rotation);
        serializer.SerializeValue(ref Facing);
        serializer.SerializeValue(ref Time);
    }
    public bool Equals(NetworkPlayerPose other) => Position == other.Position &&
        Rotation == other.Rotation && Facing == other.Facing && Time == other.Time;
}

// Keep enough history to bracket delayed playback, including network jitter.
// Two samples alone cannot interpolate a playback delay longer than their interval.
public sealed class NetworkPlayerPoseHistory
{
    private readonly NetworkPlayerPose[] samples = new NetworkPlayerPose[32];
    private int count;

    public void Reset(NetworkPlayerPose value)
    {
        count = 1;
        samples[0] = value;
    }

    public void Add(NetworkPlayerPose value)
    {
        if (count == 0) { Reset(value); return; }
        var last = samples[count - 1];
        if (value.Time <= last.Time) return;
        // Teleports must not draw a journey across the deck.
        if ((value.Position - last.Position).sqrMagnitude > 25)
        { Reset(value); return; }
        if (count == samples.Length)
        {
            Array.Copy(samples, 1, samples, 0, samples.Length - 1);
            count--;
        }
        samples[count++] = value;
    }

    public NetworkPlayerPose Sample(double time)
    {
        if (count == 0) return default;
        if (time <= samples[0].Time) return samples[0];
        for (int i = 1; i < count; i++)
        {
            var after = samples[i];
            if (time > after.Time) continue;
            var before = samples[i - 1];
            float t = (float)((time - before.Time) / (after.Time - before.Time));
            before.Position = Vector2.Lerp(before.Position, after.Position, t);
            before.Rotation = Mathf.LerpAngle(before.Rotation, after.Rotation, t);
            before.Time = time;
            return before;
        }
        // Hold on a stalled connection instead of extrapolating through geometry.
        return samples[count - 1];
    }
}

// NGO ownership grants permission to submit intent, never authority over physics.
// The dormant component leaves ordinary scene/prefab single-player behavior intact.
[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkPlayerActor : NetworkBehaviour, ILocalPlayerAuthority
{
    // Do not put a second ICharacterIntentSource on the GameObject: existing
    // motors cache GetComponent<ICharacterIntentSource> in Awake, including solo.
    private sealed class SimulationIntent : ICharacterIntentSource, IJumpPressConsumer
    {
        private readonly NetworkPlayerActor actor;
        public SimulationIntent(NetworkPlayerActor actor) => this.actor = actor;
        public CharacterIntent Current => actor.Current;
        public void ConsumeJumpPressed() => actor.ConsumeJumpPressed();
    }
    private readonly NetworkVariable<FixedString64Bytes> persistence = new();
    private readonly NetworkVariable<FixedString64Bytes> character = new();
    private readonly NetworkVariable<bool> gameplayActor = new();
    private readonly NetworkVariable<bool> sceneBoarded = new();
    private readonly NetworkVariable<NetworkPlayerPose> pose = new();
    private readonly NetworkVariable<NetworkItemData> loadout = new();
    private PlayerInventory inventory;
    private PlayerEquipment equipment;
    private readonly System.Collections.Generic.HashSet<ItemInstance> watchedItems = new();
    private bool loadoutDirty, loadoutReplicaDirty;
    private readonly CharacterNetworkInputGate inputGate = new();
    private LocalCharacterIntentSource localInput;
    private Rigidbody2D body;
    private CharacterIntent serverIntent;
    private uint sequence;
    private double nextInput, nextPose;
    private readonly NetworkPlayerPoseHistory poseHistory = new();
    private MonoBehaviour[] components;
    private bool[] authoredEnabled;
    private SpriteRenderer[] actorRenderers;
    private bool renderingHidden;

    public bool IsLocal => !DontSinkNetworkSession.IsSessionActive || (IsSpawned && IsOwner);
    public string PersistenceIdentity => persistence.Value.ToString();
    public string CharacterIdentity => character.Value.ToString();
    public bool DebugSpawningAllowed
    {
        get
        {
            for (int i = 0; components != null && i < components.Length; i++)
                if (components[i] is InventoryDebugSpawner && authoredEnabled[i]) return true;
            return false;
        }
    }
    public Vector2 AuthoritativePosition
    {
        get
        {
            if (body != null && body.simulated) return body.position;
            var world = DontSinkNetworkSession.Instance?.InteractionWorld;
            if (IsServer && world != null && world.Seat != null && world.Seat.Occupant == gameObject)
                return PhysicsFrame2D.Point(world.Seat.SeatPoint);
            var sceneSeat = DontSinkNetworkSession.Instance?.GameplayScene?.Replication?.SeatFor(this);
            if (IsServer && sceneSeat != null) return PhysicsFrame2D.Point(sceneSeat.SeatPoint);
            return transform.position;
        }
    }
    public CharacterIntent Current => IsServer && inputGate.IsFresh(Time.realtimeSinceStartupAsDouble)
        ? serverIntent : default;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        actorRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        localInput = GetComponent<LocalCharacterIntentSource>();
        if (!DontSinkNetworkSession.IsSessionActive) return;
        // No input, restore coroutine or simulation runs between Instantiate and Spawn.
        components = GetComponentsInChildren<MonoBehaviour>(true);
        authoredEnabled = new bool[components.Length];
        for (int i = 0; i < components.Length; i++)
        {
            var component = components[i];
            if (component == null || component == this || component is NetworkObject) continue;
            authoredEnabled[i] = component.enabled;
            component.enabled = false;
        }
        body.simulated = false;
    }

    public void Initialize(SessionParticipantRegistry.Participant participant)
    {
        gameplayActor.Value = DontSinkNetworkSession.Instance?.GameplayScene?.IsActive == true;
        persistence.Value = participant.PersistenceIdentity;
        character.Value = participant.CharacterIdentity;
        pose.Value = CapturePose();
    }

    public override void OnNetworkSpawn()
    {
        DontDestroyOnLoad(gameObject);
        if (components == null || body == null || localInput == null)
            throw new InvalidOperationException("Network players require an active session and the CharacterPlayer prefab.");
        name = "Player_" + OwnerClientId;
        GetComponentInChildren<PlayerLoadoutPersistence>(true)?.SetPersistenceKey(PersistenceIdentity);
        localInput.BindSimulationSource(new SimulationIntent(this));
        body.bodyType = IsServer ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
        body.interpolation = IsServer ? RigidbodyInterpolation2D.Interpolate : RigidbodyInterpolation2D.None;
        body.simulated = IsServer;
        GetComponent<ForceSystem>()?.ConfigureGameplayAuthorityGate(true, GameplayAuthorityMode.SinglePlayerOrAuthoritative);
        // Simulation runs only on the host. Owner interaction and debug UIs
        // submit validated requests; unreplicated save/death paths stay disabled.
        for (int i = 0; i < components.Length; i++)
        {
            var component = components[i];
            if (component == null || component == this || component is NetworkObject) continue;
            bool model = component is PlayerInventory || component is PlayerEquipment ||
                component is PlayerLoadoutPersistence;
            bool simulation = component is IForceProvider || component is ForceSystem ||
                component is ForceBody2D || component is CharacterPlayer ||
                component is CharacterMotor2D || component is CharacterLocomotionModeSwitcher ||
                component is PlayerBoardingState || component is PlayerSubmersionState ||
                component is PlayerLadderClimber || component is PlayerHatchLedgeDropper || component is PlayerLoadState || component is PlayerAttributeState ||
                component is PlayerExertionEnergyState || component is SpriteFacingController;
            bool interaction = gameplayActor.Value && IsOwner && (component is LocalInteractionIntentSource ||
                component is Interactor2D || component is InteractPromptDriver || component is InventoryDebugSpawner ||
                component is LocalBoatControlIntentSource || component is PlayerInventoryInput);
            component.enabled = authoredEnabled[i] && (model || interaction ||
                (component == localInput ? IsOwner : IsServer && simulation));
        }
        // Added by CharacterPlayer.Awake after our pre-spawn pass.
        var penetrationGuard = GetComponent<GeneratedGroundPenetrationGuard2D>();
        if (penetrationGuard != null) penetrationGuard.enabled = IsServer;
        pose.OnValueChanged += PoseChanged;
        poseHistory.Reset(pose.Value);
        if (!IsServer) ApplyPose(pose.Value);
        DontSinkNetworkSession.Instance?.PlayerSpawned(this);
        inventory = GetComponentInChildren<PlayerInventory>(true);
        equipment = GetComponentInChildren<PlayerEquipment>(true);
        loadout.OnValueChanged += LoadoutChanged;
        if (IsServer)
        {
            inventory.InventoryChanged += LoadoutDirty;
            inventory.SelectionChanged += LoadoutDirty;
            equipment.EquipmentChanged += LoadoutDirty;
            loadoutDirty = true;
        }
        else loadoutReplicaDirty = true;
    }

    // Run after the legacy input adapter's Update has sampled this frame.
    private void LateUpdate()
    {
        if (!IsSpawned) return;
        if (!IsServer && gameplayActor.Value)
        {
            bool hide = DontSinkNetworkSession.Instance?.GameplayScene?.LocalSceneReady != true;
            if (renderingHidden != hide)
            {
                renderingHidden = hide;
                foreach (var renderer in actorRenderers) if (renderer != null) renderer.forceRenderingOff = hide;
            }
        }
        if (IsOwner && Time.realtimeSinceStartupAsDouble >= nextInput)
        {
            var input = localInput.LocalIntent;
            ushort buttons = (ushort)((input.JumpPressed ? 1 : 0) | (input.JumpHeld ? 2 : 0) |
                (input.UprightHeld ? 4 : 0) | (input.SwimUpHeld ? 8 : 0) |
                (input.DiveHeld ? 16 : 0) | (input.SprintHeld ? 32 : 0) |
                (input.ClimbUpHeld ? 64 : 0) | (input.ClimbDownHeld ? 128 : 0));
            if (IsServer) AcceptInput(OwnerClientId, ++sequence, input.MoveX, buttons);
            else SubmitInputRpc(++sequence, input.MoveX, buttons);
            localInput.ClearLocalJumpPressed();
            nextInput = Time.realtimeSinceStartupAsDouble + 1.0 / 30;
        }
        if (!IsServer) ApplyPose(poseHistory.Sample(NetworkManager.ServerTime.Time - .1));
        IItemDefinitionResolver resolver = DontSinkNetworkSession.Instance?.InteractionWorld;
        var gameplay = DontSinkNetworkSession.Instance?.GameplayScene;
        if (!IsServer && gameplay?.LocalSceneReady == true && gameplayActor.Value)
        {
            var boarding = GetComponent<PlayerBoardingState>();
            if (sceneBoarded.Value && boarding.CurrentBoatRoot != gameplay.SceneBoat.transform)
                boarding.Board(gameplay.SceneBoat.transform);
            else if (!sceneBoarded.Value && boarding.IsBoarded) boarding.Unboard();
        }
        if (resolver == null && gameplay?.IsActive == true && gameplay.LocalSceneReady) resolver = gameplay;
        if (IsServer && loadoutDirty)
        {
            loadoutDirty = false;
            WatchLoadoutItems();
            loadout.Value = NetworkItemCodec.Capture(inventory, equipment);
        }
        else if (!IsServer && loadoutReplicaDirty && !loadout.Value.IsEmpty && resolver != null)
        {
            var snapshot = NetworkItemCodec.ReadLoadout(loadout.Value, resolver);
            // Decode/validate the entire tree before replacing either model.
            equipment.RestoreSnapshot(snapshot.equipment, resolver);
            inventory.RestoreSnapshot(snapshot.inventory, resolver);
            loadoutReplicaDirty = false;
        }
    }

    private void LoadoutChanged(NetworkItemData previous, NetworkItemData value) => loadoutReplicaDirty = true;
    private void LoadoutDirty() => loadoutDirty = true;
    private void WatchLoadoutItems()
    {
        foreach (var item in watchedItems) item.Changed -= LoadoutDirty;
        watchedItems.Clear();
        for (int i = 0; i < inventory.HotbarSlotCount; i++) Watch(inventory.GetSlot(i).Instance);
        foreach (var slot in new[] { BottomBarSlotType.Hands, BottomBarSlotType.Head, BottomBarSlotType.Feet,
            BottomBarSlotType.Toolbelt, BottomBarSlotType.Backpack, BottomBarSlotType.Body }) Watch(equipment.Get(slot));
    }
    private void Watch(ItemInstance item)
    {
        if (item != null && watchedItems.Add(item)) item.Changed += LoadoutDirty;
    }

    // Keep jump edges reliable with the input stream. Mixing a separate reliable
    // edge with unreliable sequenced axes can reject a delayed edge as stale.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Reliable)]
    private void SubmitInputRpc(uint inputSequence, float moveX, ushort buttons, RpcParams rpc = default)
        => AcceptInput(rpc.Receive.SenderClientId, inputSequence, moveX, buttons);

    private void AcceptInput(ulong sender, uint inputSequence, float moveX, ushort buttons)
    {
        var session = DontSinkNetworkSession.Instance;
        double now = Time.realtimeSinceStartupAsDouble;
        bool previousFresh = inputGate.IsFresh(now);
        if (!IsServer || sender != OwnerClientId || session?.Registry == null ||
            (session.GameplayScene?.IsActive == true && !session.GameplayScene.IsParticipantReady(sender)) ||
            !session.Registry.TryResolveConnectedSender(sender, out var participant) ||
            participant.CharacterIdentity != CharacterIdentity ||
            !session.IsParticipantActor(sender, this) ||
            !inputGate.TryAccept(inputSequence, moveX, buttons, now)) return;
        serverIntent = new CharacterIntent
        {
            MoveX = moveX,
            JumpPressed = (previousFresh && serverIntent.JumpPressed) || (buttons & 1) != 0,
            JumpHeld = (buttons & 2) != 0,
            UprightHeld = (buttons & 4) != 0,
            SwimUpHeld = (buttons & 8) != 0,
            DiveHeld = (buttons & 16) != 0,
            SprintHeld = (buttons & 32) != 0,
            ClimbUpHeld = (buttons & 64) != 0, ClimbDownHeld = (buttons & 128) != 0
        };
    }

    public void ConsumeJumpPressed() => serverIntent.JumpPressed = false;

    private void FixedUpdate()
    {
        if (!IsSpawned || !IsServer) return;
        if (!inputGate.IsFresh(Time.realtimeSinceStartupAsDouble)) serverIntent = default;
        if (NetworkManager.ServerTime.Time < nextPose) return;
        if (gameplayActor.Value) sceneBoarded.Value = GetComponent<PlayerBoardingState>().CurrentBoatRoot ==
            DontSinkNetworkSession.Instance.GameplayScene.SceneBoat.transform;
        pose.Value = CapturePose();
        // Preserve the cadence across fixed steps instead of rounding every
        // 50 ms deadline up to a new 60 ms interval. Never burst after a stall.
        nextPose += 1.0 / 20;
        if (nextPose <= NetworkManager.ServerTime.Time)
            nextPose = NetworkManager.ServerTime.Time + 1.0 / 20;
    }

    private NetworkPlayerPose CapturePose() => new NetworkPlayerPose
    {
        Position = AuthoritativePosition,
        Rotation = body.simulated ? body.rotation : transform.eulerAngles.z,
        Facing = transform.localScale.x,
        Time = NetworkManager != null ? NetworkManager.ServerTime.Time : 0
    };

    private void PoseChanged(NetworkPlayerPose oldPose, NetworkPlayerPose newPose)
    {
        poseHistory.Add(newPose);
    }

    private void ApplyPose(NetworkPlayerPose value)
    {
        transform.SetPositionAndRotation(new Vector3(value.Position.x, value.Position.y, transform.position.z),
            Quaternion.Euler(0, 0, value.Rotation));
        var scale = transform.localScale;
        scale.x = value.Facing;
        transform.localScale = scale;
    }

    public override void OnNetworkDespawn()
    {
        pose.OnValueChanged -= PoseChanged;
        loadout.OnValueChanged -= LoadoutChanged;
        if (inventory != null) { inventory.InventoryChanged -= LoadoutDirty; inventory.SelectionChanged -= LoadoutDirty; }
        if (equipment != null) equipment.EquipmentChanged -= LoadoutDirty;
        foreach (var item in watchedItems) item.Changed -= LoadoutDirty;
        watchedItems.Clear();
        serverIntent = default;
        DontSinkNetworkSession.Instance?.PlayerDespawned(this);
    }
}
