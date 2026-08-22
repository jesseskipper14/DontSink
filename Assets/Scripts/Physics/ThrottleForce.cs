using System.Collections.Generic;
using UnityEngine;

public class ThrottleForce : MonoBehaviour, IOrderedForceProvider, IThrottleReceiver
{
    public bool Enabled => enabledFlag;
    public int Priority => priority;

    public float CurrentThrottle => throttle01;
    public float MaxForce => maxForce;
    public Vector2 LastAppliedForce => lastAppliedForce;
    public int LastActiveEngineCount => lastActiveEngineCount;

    [SerializeField] private bool enabledFlag = true;
    [SerializeField] private int priority = 250;
    [SerializeField] private float maxForce = 25f;

    [Header("Engine Gating")]
    [SerializeField] private Boat boat;
    [SerializeField] private bool multiplyForceByActiveEngineCount = false;

    [Header("Diagnostics")]
    [SerializeField] private bool verboseDiagnostics = true;
    [SerializeField, Min(0.1f)] private float diagnosticInterval = 0.5f;

    private float throttle01; // [-1..1]
    private readonly List<EngineModule> engines = new();

    private Vector2 lastAppliedForce;
    private int lastActiveEngineCount;
    private float _nextDiagnosticTime;

    private void Awake()
    {
        ResolveBoat();
        RefreshEngines();
    }

    private void OnEnable()
    {
        ResolveBoat();
        RefreshEngines();
    }

    public void SetThrottle(float value)
    {
        throttle01 = Mathf.Clamp(value, -1f, 1f);
    }

    public void GetPropulsionStatus(
        out int installedSourceCount,
        out int activeSourceCount)
    {
        if (boat == null)
            ResolveBoat();

        RefreshEngines();

        installedSourceCount = engines.Count;
        activeSourceCount = 0;

        for (int i = 0; i < engines.Count; i++)
        {
            EngineModule engine = engines[i];

            if (engine != null &&
                engine.CanProduceThrust())
            {
                activeSourceCount++;
            }
        }
    }

    public bool HasAvailablePropulsion
    {
        get
        {
            GetPropulsionStatus(
                out _,
                out int activeSourceCount);

            return activeSourceCount > 0;
        }
    }

    public void ApplyForces(IForceBody body)
    {
        lastAppliedForce = Vector2.zero;
        lastActiveEngineCount = 0;

        if (!enabledFlag)
        {
            LogDiagnosticsIfNeeded(
                body,
                "DISABLED");
            return;
        }

        if (boat == null)
            ResolveBoat();

        if (boat == null)
        {
            LogDiagnosticsIfNeeded(
                body,
                "NO_BOAT");
            return;
        }

        RefreshEngines();

        float absThrottle =
            Mathf.Abs(throttle01);

        int activeEngineCount = 0;

        for (int i = 0; i < engines.Count; i++)
        {
            EngineModule engine = engines[i];

            if (engine == null)
                continue;

            engine.SetThrottleLoad(
                absThrottle);

            if (engine.CanProduceThrust())
                activeEngineCount++;
        }

        lastActiveEngineCount =
            activeEngineCount;

        if (activeEngineCount <= 0)
        {
            LogDiagnosticsIfNeeded(
                body,
                "NO_ACTIVE_ENGINE");
            return;
        }

        Vector2 dir =
            (Vector2)transform.right;

        float engineMultiplier =
            multiplyForceByActiveEngineCount
                ? activeEngineCount
                : 1f;

        Vector2 f =
            dir *
            (throttle01 *
             maxForce *
             engineMultiplier);

        lastAppliedForce = f;

        body.AddForce(f);

        LogDiagnosticsIfNeeded(
            body,
            "APPLIED");
    }

    private void LogDiagnosticsIfNeeded(
        IForceBody body,
        string status)
    {
        if (!verboseDiagnostics)
            return;

        if (Time.unscaledTime <
            _nextDiagnosticTime)
        {
            return;
        }

        _nextDiagnosticTime =
            Time.unscaledTime +
            diagnosticInterval;

        Rigidbody2D rb =
            body != null
                ? body.rb
                : null;

        string rbText =
            rb != null
                ? $"rbVel=({rb.linearVelocity.x:+0.000;-0.000;0.000}," +
                  $"{rb.linearVelocity.y:+0.000;-0.000;0.000}) " +
                  $"mass={rb.mass:0.###} " +
                  $"linearDamping={rb.linearDamping:0.###} " +
                  $"rotation={rb.rotation:+0.0;-0.0;0.0}"
                : "rb=NULL";

        float expectedAccel =
            rb != null &&
            rb.mass > 0.0001f
                ? lastAppliedForce.magnitude /
                  rb.mass
                : 0f;

        Debug.Log(
            $"[ThrottleForce] DIAG status={status} " +
            $"bodyType={(body != null ? body.GetType().Name : "NULL")} " +
            $"throttle={throttle01:+0.000;-0.000;0.000} " +
            $"maxForce={maxForce:0.###} " +
            $"engines={activeEngineCountText()}/{engines.Count} " +
            $"multiplyByCount={multiplyForceByActiveEngineCount} | " +
            $"force=({lastAppliedForce.x:+0.00;-0.00;0.00}," +
                    $"{lastAppliedForce.y:+0.00;-0.00;0.00}) " +
            $"forceMag={lastAppliedForce.magnitude:0.00} " +
            $"expectedAccel~={expectedAccel:0.00} | " +
            $"forceTransformRot={transform.eulerAngles.z:+0.0;-0.0;0.0}deg " +
            $"{rbText}",
            this);

        int activeEngineCountText()
        {
            return lastActiveEngineCount;
        }
    }

    private void ResolveBoat()
    {
        if (boat != null)
            return;

        boat =
            GetComponentInParent<Boat>();

        if (boat != null)
            return;

        GameObject playerBoatGo =
            GameObject.FindGameObjectWithTag(
                "PlayerBoat");

        if (playerBoatGo != null)
        {
            boat =
                playerBoatGo.GetComponent<Boat>();
        }
    }

    private void RefreshEngines()
    {
        engines.Clear();

        if (boat == null)
            return;

        Hardpoint[] hardpoints =
            boat.GetComponentsInChildren<Hardpoint>(
                true);

        for (int i = 0;
             i < hardpoints.Length;
             i++)
        {
            Hardpoint hp =
                hardpoints[i];

            if (hp == null ||
                !hp.HasInstalledModule ||
                hp.InstalledModule == null)
            {
                continue;
            }

            EngineModule engine =
                hp.InstalledModule
                  .GetComponent<EngineModule>();

            if (engine != null)
                engines.Add(engine);
        }
    }
}