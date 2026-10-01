using UnityEngine;

/// <summary>
/// Scene-local helper for custom background SpriteRenderers that expose a
/// _Brightness material property. It registers the renderer with the existing
/// persistent GlobalBrightnessManager so brightness survives scene transitions
/// without giving the background its own time-of-day logic.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BackgroundBrightnessBinder : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField, Min(0.05f)] private float retrySeconds = 0.25f;

    private GlobalBrightnessManager _brightnessManager;
    private bool _registered;
    private float _nextRetryTime;

    private void Reset()
    {
        targetRenderer = GetComponent<SpriteRenderer>();
    }

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        TryBind();
    }

    private void Update()
    {
        if (_registered)
            return;

        if (Time.unscaledTime < _nextRetryTime)
            return;

        _nextRetryTime = Time.unscaledTime + retrySeconds;
        TryBind();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void TryBind()
    {
        if (_registered || targetRenderer == null)
            return;

        if (ServiceRoot.Instance == null)
            return;

        _brightnessManager = ServiceRoot.Instance.Brightness as GlobalBrightnessManager;
        if (_brightnessManager == null)
            return;

        _brightnessManager.Register(targetRenderer);
        _registered = true;
    }

    private void Unbind()
    {
        if (!_registered)
            return;

        if (_brightnessManager != null && targetRenderer != null)
            _brightnessManager.Unregister(targetRenderer);

        _registered = false;
        _brightnessManager = null;
    }
}
