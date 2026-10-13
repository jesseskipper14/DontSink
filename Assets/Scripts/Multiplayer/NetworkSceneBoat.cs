using System;
using Unity.Netcode;
using UnityEngine;

// Adopts the normal BoatSpawner boat on each peer. Never instantiates a second
// boat, creates development water, changes materials or provisions test power.
[RequireComponent(typeof(NetworkObject))]
public sealed partial class NetworkSceneBoat : NetworkBehaviour
{
    private readonly NetworkVariable<SharedBoatState> state = new();
    private NetworkList<SharedCompartmentFluid> fluids;
    private readonly NetworkPlayerPoseHistory history = new();
    private Boat boat;
    private BoatPowerState power;
    private BoatPilotingState piloting;
    private EngineModule[] engines;
    private DoorRuntime[] doors;
    private Compartment[] compartments;
    private double next;
    private void Awake() { fluids = new NetworkList<SharedCompartmentFluid>(); InitializeStations(); InitializeDebugSpawning(); }
    public Boat Boat => boat;
    public override void OnNetworkSpawn()
    {
        DontDestroyOnLoad(gameObject);
        state.OnValueChanged += Changed;
        history.Reset(state.Value.Pose);
        var scene = DontSinkNetworkSession.Instance.GameplayScene;
        if (scene.Replication != null && scene.Replication != this) throw new InvalidOperationException("Duplicate shared scene boat.");
        scene.Replication = this;
        Bind();
    }
    private bool Bind()
    {
        if (boat != null) return true;
        boat = DontSinkNetworkSession.Instance?.GameplayScene?.SceneBoat;
        if (boat == null) return false;
        power = boat.GetComponent<BoatPowerState>(); piloting = boat.GetComponent<BoatPilotingState>();
        engines = boat.GetComponentsInChildren<EngineModule>(true); doors = boat.GetComponentsInChildren<DoorRuntime>(true);
        compartments = boat.GetComponentsInChildren<Compartment>(true);
        BindStations();
        BindModules();
        BindStorage();
        PublishStationLinks();
        if (doors.Length > 64 || engines.Length > 64 || compartments.Length > 64)
            throw new InvalidOperationException("Normal boat exceeds the shared snapshot capacity.");
        if (IsServer)
        {
            foreach (var compartment in compartments) fluids.Add(new SharedCompartmentFluid { Water = compartment.WaterArea, Air = compartment.airIntegrity });
            Publish();
        }
        return true;
    }
    private void FixedUpdate()
    {
        if (!IsSpawned || !IsServer || !Bind()) return;
        TickStations();
        TickModules();
        TickStorage();
        TickModuleRemoval();
        ExpireThrowHolds();
        TickLooseItems();
        if (NetworkManager.ServerTime.Time < next) return;
        Publish(); next = NetworkManager.ServerTime.Time + .05;
    }
    private void Publish()
    {
        var value = new SharedBoatState { Incarnation = 1, Pose = new NetworkPlayerPose {
            Position = boat.rb.position, Rotation = boat.rb.rotation, Time = NetworkManager.ServerTime.Time, Facing = 1 },
            Velocity = boat.rb.linearVelocity, AngularVelocity = boat.rb.angularVelocity,
            Power = power != null ? power.CurrentPower : 0 };
        if (piloting != null)
        {
            value.Throttle = piloting.Throttle; value.Rudder = piloting.RudderDegrees; value.Heading = piloting.HeadingDegrees;
            value.NavigationPosition = piloting.NavigationPosition; value.NavigationVelocity = piloting.NavigationVelocity;
            value.NavigationAngularVelocity = piloting.AngularVelocityDegrees;
        }
        for (int i = 0; i < doors.Length; i++) if (doors[i].IsOpen) value.OpenDoors |= 1UL << i;
        for (int i = 0; i < engines.Length; i++) if (engines[i].IsOn) value.RunningEngines |= 1UL << i;
        for (int i = 0; i < compartments.Length; i++)
        {
            var fluid = new SharedCompartmentFluid { Water = compartments[i].WaterArea, Air = compartments[i].airIntegrity };
            if (!fluids[i].Equals(fluid)) fluids[i] = fluid;
        }
        state.Value = value;
    }
    private void Changed(SharedBoatState previous, SharedBoatState value) => history.Add(value.Pose);
    private void LateUpdate()
    {
        SampleLocalHelmInput();
        if (!IsSpawned || IsServer || !Bind() || state.Value.Incarnation == 0) return;
        ApplyDebugModules();
        ApplyStationLinks();
        var pose = history.Sample(NetworkManager.ServerTime.Time - .1);
        ApplyInteractionState();
        boat.transform.SetPositionAndRotation(pose.Position, Quaternion.Euler(0, 0, pose.Rotation));
        if (power != null) power.SetCurrentPower(state.Value.Power);
        if (piloting != null)
        {
            piloting.SetControlPositions(state.Value.Throttle, state.Value.Rudder);
            piloting.SetNavigationState(state.Value.NavigationPosition, state.Value.NavigationVelocity,
                state.Value.Heading, state.Value.NavigationAngularVelocity);
        }
        if (fluids.Count != compartments.Length) return;
        for (int i = 0; i < compartments.Length; i++) compartments[i].RestorePersistentFluidState(fluids[i].Water, fluids[i].Air);
        for (int i = 0; i < doors.Length; i++)
        {
            bool open = (state.Value.OpenDoors & (1UL << i)) != 0;
            if (doors[i].IsOpen != open) doors[i].RestoreOpenState(open);
        }
        for (int i = 0; i < engines.Length; i++)
        {
            if (sharedModuleEngines.Contains(engines[i])) continue;
            bool running = (state.Value.RunningEngines & (1UL << i)) != 0;
            if (engines[i].IsOn != running) engines[i].RestorePersistentState(running, engines[i].CaptureFuelContainerSnapshot(), DontSinkNetworkSession.Instance.GameplayScene);
        }
        ApplyModules();
        ApplyStorage();
        ApplyLooseItems();
    }
    public static void ConfigureReplica(GameObject instance)
    {
        foreach (var component in instance.GetComponentsInChildren<MonoBehaviour>(true))
            if (component is Boat || component is ForceSystem || component is IForceProvider ||
                component is BoatPilotingSimulation || component is BoatHandlingAggregator || component is ExternalWaterSourceSystem)
                component.enabled = false;
        foreach (var body in instance.GetComponentsInChildren<Rigidbody2D>(true))
        { body.simulated = false; body.bodyType = RigidbodyType2D.Kinematic; body.interpolation = RigidbodyInterpolation2D.None; }
    }
    public override void OnNetworkDespawn()
    {
        state.OnValueChanged -= Changed;
        ReleaseStations();
        ReleaseModules();
        ReleaseLooseItems();
        var scene = DontSinkNetworkSession.Instance?.GameplayScene;
        if (scene?.Replication == this) scene.Replication = null;
    }
}
