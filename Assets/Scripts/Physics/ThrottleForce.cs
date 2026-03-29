using System.Collections.Generic;
using UnityEngine;

public class ThrottleForce : MonoBehaviour, IOrderedForceProvider, IThrottleReceiver
{
    public bool Enabled => enabledFlag;
    public int Priority => priority;

    [SerializeField] private bool enabledFlag = true;
    [SerializeField] private int priority = 250;
    [SerializeField] private float maxForce = 25f;

    [Header("Engine Gating")]
    [SerializeField] private Boat boat;
    [SerializeField] private bool multiplyForceByActiveEngineCount = false;

    private float throttle01; // [-1..1]
    private readonly List<EngineModule> engines = new();

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

    public void ApplyForces(IForceBody body)
    {
        if (!enabledFlag)
            return;

        if (boat == null)
            ResolveBoat();

        if (boat == null)
            return;

        RefreshEngines();

        float absThrottle = Mathf.Abs(throttle01);
        int activeEngineCount = 0;

        for (int i = 0; i < engines.Count; i++)
        {
            EngineModule engine = engines[i];
            if (engine == null)
                continue;

            engine.SetThrottleLoad(absThrottle);

            if (engine.CanProduceThrust())
                activeEngineCount++;
        }

        if (activeEngineCount <= 0)
            return;

        Vector2 dir = (Vector2)transform.right;
        float engineMultiplier = multiplyForceByActiveEngineCount ? activeEngineCount : 1f;
        Vector2 f = dir * (throttle01 * maxForce * engineMultiplier);

        body.AddForce(f);
    }

    private void ResolveBoat()
    {
        if (boat != null)
            return;

        boat = GetComponentInParent<Boat>();
        if (boat != null)
            return;

        GameObject playerBoatGo = GameObject.FindGameObjectWithTag("PlayerBoat");
        if (playerBoatGo != null)
            boat = playerBoatGo.GetComponent<Boat>();
    }

    private void RefreshEngines()
    {
        engines.Clear();

        if (boat == null)
            return;

        Hardpoint[] hardpoints = boat.GetComponentsInChildren<Hardpoint>(true);
        for (int i = 0; i < hardpoints.Length; i++)
        {
            Hardpoint hp = hardpoints[i];
            if (hp == null || !hp.HasInstalledModule || hp.InstalledModule == null)
                continue;

            EngineModule engine = hp.InstalledModule.GetComponent<EngineModule>();
            if (engine != null)
                engines.Add(engine);
        }
    }
}