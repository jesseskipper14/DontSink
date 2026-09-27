using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Generic presentation component that writes text supplied by a
/// DynamicTextSourceBehaviour into any TMP_Text target.
///
/// Works with both world-space TextMeshPro and UI TextMeshProUGUI because both
/// derive from TMP_Text. The binder intentionally knows nothing about nodes,
/// shops, quests, depth, etc.; those meanings belong to source components.
/// </summary>
[DisallowMultipleComponent]
public sealed class DynamicTextBinder : MonoBehaviour
{
    [Header("Binding")]
    [Tooltip("TextMeshPro or TextMeshProUGUI target. Auto-resolves from this hierarchy when blank.")]
    [SerializeField] private TMP_Text targetText;

    [Tooltip("Component that supplies the value. Auto-resolves from this GameObject when blank.")]
    [SerializeField] private DynamicTextSourceBehaviour source;

    [Header("Presentation")]
    [Tooltip("Optional string.Format template. {0} is replaced by the source value.")]
    [SerializeField] private string format = "{0}";

    [Tooltip("Displayed when the source is unavailable or cannot currently resolve a value.")]
    [SerializeField] private string unavailableText = "";

    [Header("Refresh")]
    [Tooltip("Optional polling interval for values that change without raising TextChanged. 0 disables polling.")]
    [SerializeField, Min(0f)] private float pollingIntervalSeconds = 0f;

    private DynamicTextSourceBehaviour _subscribedSource;
    private float _nextPollTime;

    public TMP_Text TargetText => targetText;
    public DynamicTextSourceBehaviour Source => source;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToSource();
        RefreshNow();
        ScheduleNextPoll();
    }

    private void Start()
    {
        // A second refresh at Start is intentional. Scene bootstrap objects such
        // as GameState/runtime map binders may finish their own Awake/OnEnable
        // work after this object first becomes enabled.
        RefreshNow();
    }

    private void OnDisable()
    {
        UnsubscribeFromSource();
    }

    private void Update()
    {
        if (pollingIntervalSeconds <= 0f || Time.unscaledTime < _nextPollTime)
            return;

        RefreshNow();
        ScheduleNextPoll();
    }

    private void OnValidate()
    {
        pollingIntervalSeconds = Mathf.Max(0f, pollingIntervalSeconds);

        if (!Application.isPlaying)
            ResolveReferences();
    }

    public void RefreshNow()
    {
        ResolveReferences();

        if (targetText == null)
            return;

        if (source == null || !source.TryGetText(out string value))
        {
            targetText.text = unavailableText ?? string.Empty;
            return;
        }

        targetText.text = FormatValue(value ?? string.Empty);
    }

    public void SetSource(DynamicTextSourceBehaviour nextSource)
    {
        if (ReferenceEquals(source, nextSource))
            return;

        UnsubscribeFromSource();
        source = nextSource;

        if (isActiveAndEnabled)
            SubscribeToSource();

        RefreshNow();
    }

    public void SetTarget(TMP_Text nextTarget)
    {
        targetText = nextTarget;
        RefreshNow();
    }

    private void ResolveReferences()
    {
        if (targetText == null)
            targetText = GetComponentInChildren<TMP_Text>(true);

        if (source == null)
            source = GetComponent<DynamicTextSourceBehaviour>();
    }

    private void SubscribeToSource()
    {
        if (_subscribedSource == source)
            return;

        UnsubscribeFromSource();

        _subscribedSource = source;
        if (_subscribedSource != null)
            _subscribedSource.TextChanged += HandleSourceTextChanged;
    }

    private void UnsubscribeFromSource()
    {
        if (_subscribedSource != null)
            _subscribedSource.TextChanged -= HandleSourceTextChanged;

        _subscribedSource = null;
    }

    private void HandleSourceTextChanged()
    {
        RefreshNow();
    }

    private string FormatValue(string value)
    {
        if (string.IsNullOrEmpty(format) || format == "{0}")
            return value;

        try
        {
            return string.Format(format, value);
        }
        catch (FormatException)
        {
            Debug.LogWarning(
                $"[DynamicTextBinder:{name}] Invalid format string '{format}'. " +
                "Displaying the raw source value instead.",
                this);

            return value;
        }
    }

    private void ScheduleNextPoll()
    {
        _nextPollTime = pollingIntervalSeconds > 0f
            ? Time.unscaledTime + pollingIntervalSeconds
            : float.PositiveInfinity;
    }
}
