using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public enum NetworkHelmCommand : byte { EnginesOn = 1, EnginesOff, Throttle, Rudder, ToggleDoor }

public struct SharedBoatState : INetworkSerializable, IEquatable<SharedBoatState>
{
    public uint Incarnation, HelmRevision, DoorRevision;
    public NetworkPlayerPose Pose;
    public Vector2 Velocity, NavigationPosition, NavigationVelocity;
    public float AngularVelocity, Throttle, Rudder, Heading, NavigationAngularVelocity, Power;
    public ulong OpenDoors, RunningEngines;
    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref Incarnation); s.SerializeValue(ref HelmRevision); s.SerializeValue(ref DoorRevision);
        s.SerializeValue(ref Pose); s.SerializeValue(ref Velocity); s.SerializeValue(ref AngularVelocity);
        s.SerializeValue(ref Throttle); s.SerializeValue(ref Rudder); s.SerializeValue(ref Heading);
        s.SerializeValue(ref NavigationPosition); s.SerializeValue(ref NavigationVelocity);
        s.SerializeValue(ref NavigationAngularVelocity); s.SerializeValue(ref Power);
        s.SerializeValue(ref OpenDoors); s.SerializeValue(ref RunningEngines);
    }
    public bool Equals(SharedBoatState other) => Incarnation == other.Incarnation &&
        HelmRevision == other.HelmRevision && DoorRevision == other.DoorRevision && Pose.Equals(other.Pose) &&
        Velocity == other.Velocity && AngularVelocity == other.AngularVelocity && Throttle == other.Throttle &&
        Rudder == other.Rudder && Heading == other.Heading && NavigationPosition == other.NavigationPosition &&
        NavigationVelocity == other.NavigationVelocity && NavigationAngularVelocity == other.NavigationAngularVelocity &&
        Power == other.Power && OpenDoors == other.OpenDoors && RunningEngines == other.RunningEngines;
}

public struct SharedCompartmentFluid : INetworkSerializable, IEquatable<SharedCompartmentFluid>
{
    public float Water, Air;
    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    { s.SerializeValue(ref Water); s.SerializeValue(ref Air); }
    public bool Equals(SharedCompartmentFluid other) => Water == other.Water && Air == other.Air;
}

// MP.4 integration slice: an authored sailing boat, not a moving substitute deck.
// Root pose and bounded domain snapshots share the players' delayed server clock.
// No client Rigidbody, flooding, propulsion, control or save loop is permitted.
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkBoatPlaytest : NetworkBehaviour
{
    [SerializeField] private GameObject boatPrefab;
    [SerializeField] private Vector2 deckLocalPoint = new(0, 6.4f);
    private readonly NetworkVariable<SharedBoatState> state = new();
    private NetworkList<SharedCompartmentFluid> fluids;
    private readonly NetworkPlayerPoseHistory history = new();
    private readonly Dictionary<ulong, NetworkInteractionRequestGate> gates = new();
    private GameObject content;
    private Material unlit;
    private BoatPilotingSimulation simulation;
    private BoatPilotingState piloting;
    private BoatPowerState power;
    private EngineModule[] engines;
    private DoorRuntime[] doors;
    private Compartment[] compartments;
    private SharedBoatState authoritative;
    private NetworkPlayerActor helmOwner;
    private double nextPublish;
    private uint nextRequest, pending;
    private uint acknowledgedHelmRevision, acknowledgedDoorRevision;
    private string feedback = "Use the station to command the helm.";
    public Boat Boat { get; private set; }
    public BoatPilotingSimulation Simulation => simulation;
    public Vector2 DeckLocalPoint => deckLocalPoint;
    public SharedBoatState CurrentState => state.Value;
    public string Feedback => feedback;
    private void Awake() => fluids = new NetworkList<SharedCompartmentFluid>();

    public override void OnNetworkSpawn()
    {
        if (boatPrefab == null || PhysicsManager.Instance == null || PhysicsManager.Instance.globals == null)
            throw new InvalidOperationException("MP.4 needs an authored boat and the scene PhysicsManager/PhysicsGlobals.");
        content = new GameObject("_MP4_SailingBoat");
        content.transform.SetParent(transform, false);
        content.SetActive(false);
        unlit = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default"));
        // Configure before Awake. Never change the user's original boat prefab.
        var waterObject = new GameObject("MP4 local calm water");
        waterObject.transform.SetParent(content.transform, false);
        waterObject.transform.position = transform.position + Vector3.up * 2;
        var wave = waterObject.AddComponent<WaveField>();
        wave.width = 4096; wave.resolution = 128; wave.amplitude = .1f;
        wave.enabled = IsServer;
        var water = waterObject.AddComponent<WaveManager>();
        water.Initialize(null, wave);
        water.enabled = IsServer;
        var instance = Instantiate(boatPrefab, content.transform);
        instance.transform.position = transform.position;
        instance.transform.rotation = Quaternion.identity;
        Boat = instance.GetComponent<Boat>();
        if (Boat == null) throw new InvalidOperationException("MP.4 prefab must contain Boat on its root.");
        Boat.SetBoatInstanceId("mp_boat_" + NetworkObjectId);
        simulation = instance.GetComponent<BoatPilotingSimulation>();
        piloting = instance.GetComponent<BoatPilotingState>();
        power = instance.GetComponent<BoatPowerState>();
        if (simulation == null || piloting == null || power == null)
            throw new InvalidOperationException("MP.4 boat needs existing piloting and power components.");
        // This scene-only overlay is outside the connected slice. Removing its
        // adapter from the inactive clone prevents Awake demanding a map HUD;
        // its authored table visual and the source asset are retained.
        foreach (var table in instance.GetComponentsInChildren<MapTableInteractable>(true)) DestroyImmediate(table);
        foreach (var component in instance.GetComponentsInChildren<MonoBehaviour>(true))
        {
            bool physics = component is Boat || component is ForceSystem || component is IForceProvider ||
                component is BoatPilotingSimulation || component is BoatHandlingAggregator || component is EngineModule;
            bool model = component is BoatPilotingState || component is BoatPowerState || component is Compartment ||
                component is Hardpoint || component is InstalledModule || component is BoatWaterContextResolver ||
                component is DoorRuntime || component is HatchRuntime || component is BoatVisualStateController ||
                component is BoatVisibilityZone;
            component.enabled = component.enabled && (model || IsServer && physics);
        }
        // Child module bodies remain mounted. Only the root sailing body simulates.
        foreach (var body in instance.GetComponentsInChildren<Rigidbody2D>(true))
        {
            body.simulated = IsServer && body.gameObject == instance;
            body.interpolation = IsServer ? RigidbodyInterpolation2D.Interpolate : RigidbodyInterpolation2D.None;
            if (!IsServer) body.bodyType = RigidbodyType2D.Kinematic;
        }
        foreach (var renderer in instance.GetComponentsInChildren<SpriteRenderer>(true)) renderer.sharedMaterial = unlit;
        content.SetActive(true);
        // Boat.Awake auto-finds an ambient field; override with this isolated field.
        Boat.buoyancyForce.wave = wave;
        Boat.buoyancyForce.waveManager = water;
        engines = instance.GetComponentsInChildren<EngineModule>(true);
        doors = instance.GetComponentsInChildren<DoorRuntime>(true);
        compartments = instance.GetComponentsInChildren<Compartment>(true);
        if (engines.Length > 64 || doors.Length > 64 || compartments.Length > 64)
            throw new InvalidOperationException("MP.4 snapshot capacity exceeded; extend the schema before using this boat.");
        foreach (var engine in engines) engine.BindPropulsionWater(water);
        state.OnValueChanged += Changed;
        if (IsServer)
        {
            authoritative.Incarnation = unchecked((uint)Guid.NewGuid().GetHashCode());
            if (authoritative.Incarnation == 0) authoritative.Incarnation = 1;
            authoritative.HelmRevision = authoritative.DoorRevision = 1;
            // Development provisioning only. Production load restores its own power.
            power.SetMaxPower(1000); power.SetCurrentPower(1000);
            foreach (var compartment in compartments)
                fluids.Add(new SharedCompartmentFluid { Water = compartment.WaterArea, Air = compartment.airIntegrity });
            Publish();
        }
        else
        {
            power.SetMaxPower(1000);
            history.Reset(state.Value.Pose);
            ApplyReplica(state.Value);
        }
        DontSinkNetworkSession.Instance.BindBoatWorld(this);
    }

    public Vector2 SpawnPoint(int slot)
    {
        int columns = Mathf.Max(1, Mathf.FloorToInt((Boat.Width - 2) / 2));
        return Boat.transform.TransformPoint(deckLocalPoint + new Vector2((slot % columns - (columns - 1) * .5f) * 2, 2 + slot / columns * 3));
    }
    public void Present(NetworkPlayerActor actor, bool placeOnDeck = false, int spawnSlot = 0)
    {
        if (actor == null || Boat == null) return;
        if (IsServer && placeOnDeck)
        {
            var body = actor.GetComponent<Rigidbody2D>();
            body.position = SpawnPoint(spawnSlot);
            body.linearVelocity = Boat.rb.linearVelocity;
        }
        actor.GetComponent<PlayerBoardingState>()?.BoardDeferredPresentation(Boat.transform);
        if (actor.IsOwner)
            Boat.GetComponent<BoatVisualStateController>()?.SetLocalViewingPlayer(actor.GetComponent<PlayerBoardingState>());
    }

    private void FixedUpdate()
    {
        if (!IsSpawned || !IsServer || Boat == null) return;
        var interactions = DontSinkNetworkSession.Instance.InteractionWorld;
        NetworkPlayerActor seated = null;
        if (interactions != null && interactions.CurrentState.SeatOccupied)
            DontSinkNetworkSession.Instance.TryGetParticipantActor(interactions.CurrentState.SeatOwner, out seated);
        if (helmOwner != seated)
        {
            if (helmOwner != null) simulation.ReleaseControl(helmOwner);
            helmOwner = seated;
            if (helmOwner != null && !simulation.TryClaimControl(helmOwner)) helmOwner = null;
            authoritative.HelmRevision++;
        }
        if (NetworkManager.ServerTime.Time < nextPublish) return;
        Publish();
        nextPublish += .05;
        if (nextPublish <= NetworkManager.ServerTime.Time) nextPublish = NetworkManager.ServerTime.Time + .05;
    }

    private void Publish()
    {
        var body = Boat.rb;
        authoritative.Pose = new NetworkPlayerPose { Position = body.position, Rotation = body.rotation,
            Facing = 1, Time = NetworkManager.ServerTime.Time };
        authoritative.Velocity = body.linearVelocity; authoritative.AngularVelocity = body.angularVelocity;
        authoritative.Throttle = piloting.Throttle; authoritative.Rudder = piloting.RudderDegrees;
        authoritative.NavigationPosition = piloting.NavigationPosition;
        authoritative.NavigationVelocity = piloting.NavigationVelocity;
        authoritative.Heading = piloting.HeadingDegrees;
        authoritative.NavigationAngularVelocity = piloting.AngularVelocityDegrees;
        authoritative.Power = power.CurrentPower;
        ulong doorBits = 0, engineBits = 0;
        for (int i = 0; i < doors.Length; i++) if (doors[i].IsOpen) doorBits |= 1UL << i;
        for (int i = 0; i < engines.Length; i++) if (engines[i].IsOn) engineBits |= 1UL << i;
        if (authoritative.OpenDoors != doorBits) authoritative.DoorRevision++;
        authoritative.OpenDoors = doorBits; authoritative.RunningEngines = engineBits;
        for (int i = 0; i < compartments.Length; i++)
        {
            var value = new SharedCompartmentFluid { Water = compartments[i].WaterArea, Air = compartments[i].airIntegrity };
            if (!fluids[i].Equals(value)) fluids[i] = value;
        }
        state.Value = authoritative;
    }

    private void Changed(SharedBoatState previous, SharedBoatState value) => history.Add(value.Pose);
    private void LateUpdate()
    {
        if (!IsSpawned || IsServer || state.Value.Incarnation == 0 || Boat == null) return;
        var pose = history.Sample(NetworkManager.ServerTime.Time - .1);
        Boat.transform.SetPositionAndRotation(pose.Position, Quaternion.Euler(0, 0, pose.Rotation));
        ApplyReplica(state.Value);
    }
    private void ApplyReplica(SharedBoatState value)
    {
        if (value.Incarnation == 0) return;
        piloting.SetControlPositions(value.Throttle, value.Rudder);
        piloting.SetNavigationState(value.NavigationPosition, value.NavigationVelocity, value.Heading, value.NavigationAngularVelocity);
        power.SetCurrentPower(value.Power);
        for (int i = 0; i < engines.Length; i++)
        {
            bool on = (value.RunningEngines & (1UL << i)) != 0;
            if (engines[i].IsOn != on) engines[i].RestorePersistentState(on, null, null);
        }
        for (int i = 0; i < doors.Length; i++)
        {
            bool open = (value.OpenDoors & (1UL << i)) != 0;
            if (doors[i].IsOpen != open) doors[i].RestoreOpenState(open);
        }
        for (int i = 0; i < compartments.Length && i < fluids.Count; i++)
            compartments[i].RestorePersistentFluidState(fluids[i].Water, fluids[i].Air);
    }

    public void Request(NetworkHelmCommand command, float value = 0, int target = 0)
    {
        var session = DontSinkNetworkSession.Instance;
        if (!IsSpawned || pending != 0 || !session.HasLocalParticipant) return;
        pending = ++nextRequest; if (pending == 0) pending = ++nextRequest;
        feedback = "Waiting for host...";
        uint revision = command == NetworkHelmCommand.ToggleDoor
            ? Newer(state.Value.DoorRevision, acknowledgedDoorRevision) : Newer(state.Value.HelmRevision, acknowledgedHelmRevision);
        SubmitCommandRpc(pending, new FixedString64Bytes(session.LocalCharacterIdentity), state.Value.Incarnation,
            revision, command, value, target);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone, Delivery = RpcDelivery.Reliable)]
    public void SubmitCommandRpc(uint id, FixedString64Bytes character, uint incarnation, uint revision,
        NetworkHelmCommand command, float value, int target, RpcParams rpc = default)
    {
        ulong sender = rpc.Receive.SenderClientId;
        var session = DontSinkNetworkSession.Instance;
        if (!session.Registry.TryResolveConnectedSender(sender, out var participant) ||
            !session.TryGetParticipantActor(sender, out var actor) || actor.OwnerClientId != sender ||
            participant.CharacterIdentity != character.ToString() || actor.CharacterIdentity != character.ToString())
        { Reply(sender, id, "Invalid participant."); return; }
        if (!gates.TryGetValue(sender, out var gate)) gates[sender] = gate = new NetworkInteractionRequestGate();
        var decision = gate.Begin(id, Time.realtimeSinceStartupAsDouble);
        if (decision == NetworkInteractionRequestDisposition.Replay) { Reply(sender, id, gate.LastReason); return; }
        if (decision != NetworkInteractionRequestDisposition.New) { Reply(sender, id, "Stale or excessive request."); return; }
        string reason = "Done.";
        bool ok = false;
        if (incarnation != authoritative.Incarnation) reason = "Boat incarnation changed.";
        else if (!WorldTopology.IsFinite(value)) reason = "Invalid control value.";
        else if (revision != (command == NetworkHelmCommand.ToggleDoor ? authoritative.DoorRevision : authoritative.HelmRevision))
            reason = "Boat state changed; try again.";
        else if (command == NetworkHelmCommand.ToggleDoor)
        {
            if (target < 0 || target >= doors.Length) reason = "Unknown door.";
            else if (Vector2.Distance(actor.AuthoritativePosition, PhysicsFrame2D.Point(doors[target].transform)) > 2.5f)
                reason = "Move closer to the door.";
            else { ok = doors[target].SetOpen(!doors[target].IsOpen); if (!ok) reason = "Door is blocked."; }
        }
        else if (helmOwner != actor || !simulation.HasControlAuthority(actor)) reason = "You do not control the helm.";
        else
        {
            switch (command)
            {
                case NetworkHelmCommand.EnginesOn:
                case NetworkHelmCommand.EnginesOff:
                    ok = simulation.TrySetEnginesRunning(actor, command == NetworkHelmCommand.EnginesOn, out int installed, out int successful)
                        && installed > 0 && successful == installed; break;
                case NetworkHelmCommand.Throttle: ok = Mathf.Abs(value) <= 1 && simulation.TrySetThrottleTarget(actor, value); break;
                case NetworkHelmCommand.Rudder: ok = Mathf.Abs(value) <= 1 && simulation.TrySetRudderTarget(actor, value); break;
                default: reason = "Unknown boat command."; break;
            }
            if (ok) authoritative.HelmRevision++;
            else if (reason == "Done.") reason = "Boat cannot accept that order.";
        }
        gate.Complete(ok, reason);
        if (ok) Publish();
        Reply(sender, id, reason);
    }
    private static uint Newer(uint snapshot, uint acknowledged)
        => unchecked((int)(acknowledged - snapshot)) > 0 ? acknowledged : snapshot;
    private void Reply(ulong sender, uint id, string reason) => ResultRpc(id, new FixedString128Bytes(reason),
        authoritative.HelmRevision, authoritative.DoorRevision,
        RpcTarget.Single(sender, RpcTargetUse.Temp));
    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void ResultRpc(uint id, FixedString128Bytes reason, uint helmRevision, uint doorRevision, RpcParams rpc = default)
    {
        if (rpc.Receive.SenderClientId != NetworkManager.ServerClientId || id != pending) return;
        pending = 0; feedback = reason.ToString();
        acknowledgedHelmRevision = helmRevision; acknowledgedDoorRevision = doorRevision;
    }
    public void ReleaseParticipant(ulong sender)
    {
        if (!IsServer) return;
        if (helmOwner != null && helmOwner.OwnerClientId == sender)
        { simulation.ReleaseControl(helmOwner); helmOwner = null; authoritative.HelmRevision++; }
        gates.Remove(sender);
    }
    private void OnGUI()
    {
        if (!IsSpawned) return;
        var interactions = DontSinkNetworkSession.Instance.InteractionWorld;
        bool ownsHelm = interactions != null && interactions.CurrentState.SeatOccupied &&
            interactions.CurrentState.SeatOwner == NetworkManager.LocalClientId;
        GUILayout.BeginArea(new Rect(10, 340, 285, 360), GUI.skin.box);
        GUILayout.Label("MP.4 — Authored sailing boat");
        GUILayout.Label($"Speed {state.Value.Velocity.x:F1} • Throttle {state.Value.Throttle:P0}");
        GUILayout.Label($"Heading {state.Value.Heading:F0}° • Rudder {state.Value.Rudder:F1}°");
        GUILayout.Label(feedback);
        bool previous = GUI.enabled;
        GUI.enabled = previous && pending == 0 && ownsHelm;
        if (GUILayout.Button("Start engines")) Request(NetworkHelmCommand.EnginesOn);
        if (GUILayout.Button("Stop engines")) Request(NetworkHelmCommand.EnginesOff);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Full ahead")) Request(NetworkHelmCommand.Throttle, 1);
        if (GUILayout.Button("Half ahead")) Request(NetworkHelmCommand.Throttle, .5f);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Full stop")) Request(NetworkHelmCommand.Throttle, 0);
        if (GUILayout.Button("Reverse")) Request(NetworkHelmCommand.Throttle, -.25f);
        GUILayout.EndHorizontal();
        GUI.enabled = previous && pending == 0 && ownsHelm && simulation.HasSteering;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Left")) Request(NetworkHelmCommand.Rudder, -1);
        if (GUILayout.Button("Straight")) Request(NetworkHelmCommand.Rudder, 0);
        if (GUILayout.Button("Right")) Request(NetworkHelmCommand.Rudder, 1);
        GUILayout.EndHorizontal();
        GUI.enabled = previous && pending == 0;
        if (DontSinkNetworkSession.Instance.TryGetParticipantActor(NetworkManager.LocalClientId, out var actor))
            for (int i = 0; i < doors.Length; i++)
                if (Vector2.Distance(actor.transform.position, doors[i].transform.position) <= 2.5f && GUILayout.Button("Toggle " + doors[i].name))
                    Request(NetworkHelmCommand.ToggleDoor, 0, i);
        GUI.enabled = previous;
        GUILayout.EndArea();
    }
    public override void OnNetworkDespawn()
    {
        state.OnValueChanged -= Changed;
        if (helmOwner != null) simulation.ReleaseControl(helmOwner);
        if (DontSinkNetworkSession.Instance != null && DontSinkNetworkSession.Instance.BoatWorld == this)
            DontSinkNetworkSession.Instance.BindBoatWorld(null);
        if (content != null) Destroy(content);
        if (unlit != null) Destroy(unlit);
    }
}
