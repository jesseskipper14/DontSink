using UnityEngine;
using System;

public class WindManager : MonoBehaviour, IWindService
{
    [Header("Wind Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float windStrength01 = 0f;

    [SerializeField] private float maxWindSpeed = 0.3f;

    [SerializeField]
    private Vector2 windDirection = Vector2.right;

    [Header("Transition")]
    [SerializeField] private float transitionDuration = 5f;

    // Linear transition state
    private float startStrength;
    private float targetStrength;
    private float elapsed;
    private bool transitioning;

    private IWeatherService weatherService;

    // =========================
    // IWindService
    // =========================
    public float WindStrength01 => windStrength01;
    public float MaxWindSpeed => maxWindSpeed;
    public Vector2 WindDirection => windDirection.normalized;
    public Vector2 WindVelocity => windDirection.normalized * (windStrength01 * maxWindSpeed);

    public event Action<float> OnWindChanged;
    public event Action<Vector2> OnWindDirectionChanged;
    public event Action OnWindTransitionStarted;

    // =========================
    // Initialization
    // =========================
    public void Initialize(IWeatherService weather)
    {
        weatherService = weather;
        weatherService.OnWindChanged += OnWeatherWindChanged;

        // Sync initial state
        SetWindImmediate(weatherService.Wind);
    }

    private void OnDestroy()
    {
        if (weatherService != null)
            weatherService.OnWindChanged -= OnWeatherWindChanged;
    }

    private void Awake()
    {
        targetStrength = windStrength01;
    }

    private void Update()
    {
        if (!transitioning)
            return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / transitionDuration);

        float previous = windStrength01;
        windStrength01 = Mathf.Lerp(startStrength, targetStrength, t);

        if (!Mathf.Approximately(previous, windStrength01))
            OnWindChanged?.Invoke(windStrength01);

        if (t >= 1f)
            transitioning = false;
    }

    // =========================
    // Weather hook
    // =========================
    private void OnWeatherWindChanged(float newWindStrength, float duration)
    {
        SetWind(newWindStrength, duration);
    }

    // =========================
    // API
    // =========================
    public void SetWind(float strength01, float timeToTarget)
    {
        startStrength = windStrength01;
        targetStrength = Mathf.Clamp01(strength01);

        transitionDuration = Mathf.Max(0.001f, timeToTarget);
        elapsed = 0f;
        transitioning = true;

        OnWindTransitionStarted?.Invoke(); // 🔑 once
    }

    public void SetWindImmediate(float strength01)
    {
        windStrength01 = targetStrength = Mathf.Clamp01(strength01);
        transitioning = false;
        elapsed = 0f;

        OnWindChanged?.Invoke(windStrength01);
    }

    public void SetWindDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;

        direction.Normalize();
        if (direction == windDirection) return;

        windDirection = direction;
        OnWindDirectionChanged?.Invoke(windDirection);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        windDirection = windDirection.sqrMagnitude < 0.0001f
            ? Vector2.right
            : windDirection.normalized;
    }
#endif
}
