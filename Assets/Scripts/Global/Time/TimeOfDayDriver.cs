using UnityEngine;

public class TimeOfDayDriver : MonoBehaviour
{
    [Header("Fallback")]
    [Tooltip("Legacy/local fallback only. The authoritative time service is resolved from ServiceRoot when available.")]
    [SerializeField] private TimeOfDayManager timeOfDay;

    [Min(0f)] public float timeScale = 1f; // 1 = normal, 10 = fast

    private ITimeOfDayService resolvedTimeService;
    private bool warnedUsingFallback;
    private bool warnedMissingTimeService;

    private void Reset()
    {
        timeOfDay = FindAnyObjectByType<TimeOfDayManager>();
    }

    private void Awake()
    {
        // Awake ordering between scene objects is undefined. ServiceRoot may not have
        // published its Time service yet, so resolve silently during startup.
        ResolveTimeService(logWarnings: false);
    }

    private void OnEnable()
    {
        ResolveTimeService(logWarnings: false);
    }

    private void Start()
    {
        // By Start, every Awake in the initial scene has completed. If ServiceRoot
        // is part of the scene, its authoritative Time service should now exist.
        ResolveTimeService(logWarnings: true);
    }

    private void Update()
    {
        // Scene transitions can change which objects exist. Always prefer the
        // persistent ServiceRoot time service if it becomes available.
        if (ServiceRoot.Instance != null &&
            ServiceRoot.Instance.Time != null &&
            !ReferenceEquals(resolvedTimeService, ServiceRoot.Instance.Time))
        {
            resolvedTimeService = ServiceRoot.Instance.Time;
            warnedMissingTimeService = false;
        }

        if (resolvedTimeService == null)
            ResolveTimeService(logWarnings: true);

        if (resolvedTimeService == null)
            return;

        resolvedTimeService.Tick(Time.deltaTime * timeScale);
    }

    [ContextMenu("Log Resolved Time Service")]
    private void LogResolvedTimeService()
    {
        ResolveTimeService(logWarnings: true);

        if (resolvedTimeService is TimeOfDayManager manager)
        {
            Debug.Log(
                $"[TimeOfDayDriver] Resolved={manager.name} " +
                $"InstanceId={manager.GetInstanceID()} " +
                $"Time={manager.CurrentTime:0.00} " +
                $"Phase={manager.CurrentPhase} " +
                $"ServiceRoot={(ServiceRoot.Instance != null ? ServiceRoot.Instance.name : "<none>")}",
                this);
        }
        else
        {
            Debug.Log(
                $"[TimeOfDayDriver] Resolved={(resolvedTimeService != null ? resolvedTimeService.GetType().Name : "<none>")} " +
                $"ServiceRoot={(ServiceRoot.Instance != null ? ServiceRoot.Instance.name : "<none>")}",
                this);
        }
    }

    private void ResolveTimeService(bool logWarnings)
    {
        // The persistent ServiceRoot owns the authoritative time service across
        // scene transitions. Never prefer an arbitrary scene-local manager over it.
        if (ServiceRoot.Instance != null && ServiceRoot.Instance.Time != null)
        {
            resolvedTimeService = ServiceRoot.Instance.Time;
            warnedMissingTimeService = false;
            return;
        }

        // Compatibility fallback for scenes that are intentionally run without
        // ServiceRoot (for example isolated editor/debug scenes).
        if (timeOfDay == null)
            timeOfDay = FindAnyObjectByType<TimeOfDayManager>();

        if (timeOfDay != null)
        {
            resolvedTimeService = timeOfDay;

            if (logWarnings && !warnedUsingFallback)
            {
                warnedUsingFallback = true;
                Debug.LogWarning(
                    "[TimeOfDayDriver] ServiceRoot time service was unavailable after startup; " +
                    "using a scene-local TimeOfDayManager fallback. " +
                    "Normal game scenes should resolve time through ServiceRoot.",
                    this);
            }

            return;
        }

        resolvedTimeService = null;

        if (logWarnings && !warnedMissingTimeService)
        {
            warnedMissingTimeService = true;
            Debug.LogWarning(
                "[TimeOfDayDriver] No time service is available. Time will not advance.",
                this);
        }
    }
}
