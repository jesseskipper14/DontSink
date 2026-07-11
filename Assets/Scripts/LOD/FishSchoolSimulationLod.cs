using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(FishSchoolController))]
public sealed class FishSchoolSimulationLod : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("LOD")]
    [SerializeField]
    private SimulationLodDistanceBands bands = new SimulationLodDistanceBands
    {
        fullRadius = 25f,
        reducedRadius = 35f,
        frozenRadius = 50f,
        hysteresis = 5f
    };

    [SerializeField, Min(0.05f)] private float checkInterval = 0.5f;

    [Header("Gameplay Authority")]
    [Tooltip("Controls whether this component is allowed to change real simulation state.")]
    [SerializeField]
    private GameplayAuthorityMode gameplayAuthorityMode =
        GameplayAuthorityMode.SinglePlayerOrAuthoritative;

    [SerializeField] private bool logSkippedForAuthority = false;

    [Header("LOD Authority")]
    [Tooltip("Passed through to AgentController when applying the LOD state.")]
    [SerializeField]
    private SimulationLodAuthorityMode lodAuthorityMode =
        SimulationLodAuthorityMode.SinglePlayerOrAuthoritative;

    [Header("Frozen Behavior")]
    [SerializeField] private bool freezeRigidbodies = true;
    [SerializeField] private bool disableRigidbodiesSimulation = false;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging;
    [SerializeField] private bool drawGizmos;

    public SimulationLodState CurrentState => state;
    public Transform Target => target;
    public float LastDistanceToTarget { get; private set; } = -1f;
    public float CheckInterval => checkInterval;
    public SimulationLodDistanceBands Bands => bands;

    private FishSchoolController school;
    private SimulationLodState state = SimulationLodState.Full;
    private float nextCheckTime;

    private void Awake()
    {
        school = GetComponent<FishSchoolController>();

        if (target == null)
            target = SimulationLodTargetResolver.ResolveDefaultTarget();
    }

    private void OnEnable()
    {
        nextCheckTime = 0f;
    }

    private void Update()
    {
        if (Time.time < nextCheckTime)
            return;

        nextCheckTime = Time.time + checkInterval;

        if (!GameplayAuthority.CanRun(gameplayAuthorityMode))
        {
            if (logSkippedForAuthority)
            {
                Debug.Log(
                    $"[FishSchoolSimulationLod] Skipped LOD update for '{name}' because " +
                    $"gameplayAuthorityMode={gameplayAuthorityMode} and " +
                    $"IsAuthoritative={GameplayAuthority.IsAuthoritative}.",
                    this);
            }

            return;
        }

        if (target == null)
            target = SimulationLodTargetResolver.ResolveDefaultTarget();

        if (target == null || school == null)
            return;

        float distance = Vector2.Distance(transform.position, target.position);
        LastDistanceToTarget = distance;

        SimulationLodState nextState = bands.Evaluate(distance, state);

        if (nextState != state)
            ApplyState(nextState, distance);
    }

    private void ApplyState(SimulationLodState nextState, float distance)
    {
        state = nextState;

        IReadOnlyList<FishSchoolMember2D> members = school.Members;

        if (members != null)
        {
            for (int i = 0; i < members.Count; i++)
            {
                FishSchoolMember2D member = members[i];

                if (member == null)
                    continue;

                AgentController agent = member.GetComponent<AgentController>();

                if (agent != null)
                    agent.SetSimulationLodState(state, lodAuthorityMode);

                Rigidbody2D rb = member.GetComponent<Rigidbody2D>();

                if (rb != null)
                {
                    if (state == SimulationLodState.Frozen)
                    {
                        if (freezeRigidbodies)
                            rb.linearVelocity = Vector2.zero;

                        if (disableRigidbodiesSimulation)
                            rb.simulated = false;
                    }
                    else
                    {
                        if (disableRigidbodiesSimulation)
                            rb.simulated = true;
                    }
                }
            }
        }

        Log($"School '{name}' LOD={state} distance={distance:0.0} members={(members != null ? members.Count : 0)}");
    }

    private void Log(string message)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[FishSchoolSimulationLod] {message}", this);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || bands == null)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, bands.fullRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, bands.reducedRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, bands.frozenRadius);
    }
}