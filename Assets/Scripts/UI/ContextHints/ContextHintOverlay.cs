using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared one-line contextual control hint used by gameplay tools.
///
/// Providers register themselves and supply only semantic hint text + a world
/// anchor. The highest-priority active provider wins. This keeps individual
/// tools from inventing their own little hint canvases every time somebody adds
/// another keybind.
///
/// A scene-authored instance may be added later for styling/tuning. If none
/// exists, the first provider creates a default runtime instance automatically.
/// </summary>
[DisallowMultipleComponent]
public sealed class ContextHintOverlay : MonoBehaviour
{
    private static readonly List<IContextHintProvider> Providers =
        new List<IContextHintProvider>();

    private static ContextHintOverlay _instance;

    [Header("World Follow")]
    [SerializeField] private Camera worldCamera;

    [Tooltip("World-space offset above the winning provider's anchor.")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 3.6f, 0f);

    [SerializeField] private bool clampToScreen = true;
    [SerializeField, Min(0f)] private float screenEdgePadding = 8f;

    [Tooltip("Additional downward screen-space offset in pixels after world-follow projection. Useful for keeping contextual key hints clear of the ordinary interaction/action prompt.")]
    [SerializeField] private float downwardScreenOffset = 15f;

    [Header("Layout")]
    [SerializeField, Min(120f)] private float width = 300f;
    [SerializeField, Min(20f)] private float height = 30f;

    private GUIStyle _hintStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Providers.Clear();
        _instance = null;
    }

    public static void Register(IContextHintProvider provider)
    {
        if (provider == null)
            return;

        CleanupDeadProviders();

        if (!Providers.Contains(provider))
            Providers.Add(provider);

        EnsureInstance();
    }

    public static void Unregister(IContextHintProvider provider)
    {
        if (provider == null)
            return;

        Providers.Remove(provider);
    }

    private static void EnsureInstance()
    {
        if (_instance != null)
            return;

        _instance =
            FindAnyObjectByType<ContextHintOverlay>(
                FindObjectsInactive.Include);

        if (_instance != null)
            return;

        GameObject go = new GameObject("_ContextHintOverlay");
        _instance = go.AddComponent<ContextHintOverlay>();
    }

    private static void CleanupDeadProviders()
    {
        for (int i = Providers.Count - 1; i >= 0; i--)
        {
            IContextHintProvider provider = Providers[i];

            if (provider == null ||
                (provider is Object unityObject && unityObject == null))
            {
                Providers.RemoveAt(i);
            }
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void OnGUI()
    {
        CleanupDeadProviders();

        if (!TryGetWinningHint(
                out string text,
                out Transform anchor))
        {
            return;
        }

        Camera cam =
            worldCamera != null
                ? worldCamera
                : Camera.main;

        if (cam == null || anchor == null)
            return;

        Vector3 screenPoint =
            cam.WorldToScreenPoint(
                anchor.position + worldOffset);

        if (screenPoint.z < 0f)
            return;

        EnsureStyle();

        float x = screenPoint.x - width * 0.5f;
        float y =
            Screen.height -
            screenPoint.y -
            height * 0.5f +
            downwardScreenOffset;

        if (clampToScreen)
        {
            x = Mathf.Clamp(
                x,
                screenEdgePadding,
                Mathf.Max(
                    screenEdgePadding,
                    Screen.width - width - screenEdgePadding));

            y = Mathf.Clamp(
                y,
                screenEdgePadding,
                Mathf.Max(
                    screenEdgePadding,
                    Screen.height - height - screenEdgePadding));
        }

        Rect panel = new Rect(x, y, width, height);

        GUI.Box(panel, GUIContent.none);

        GUI.Label(
            new Rect(
                panel.x + 8f,
                panel.y + 3f,
                panel.width - 16f,
                panel.height - 6f),
            text,
            _hintStyle);
    }

    private static bool TryGetWinningHint(
        out string text,
        out Transform anchor)
    {
        text = null;
        anchor = null;

        int bestPriority = int.MinValue;
        int bestIndex = -1;

        for (int i = 0; i < Providers.Count; i++)
        {
            IContextHintProvider provider = Providers[i];
            if (provider == null)
                continue;

            if (!provider.TryGetContextHint(
                    out string candidateText,
                    out Transform candidateAnchor) ||
                string.IsNullOrWhiteSpace(candidateText) ||
                candidateAnchor == null)
            {
                continue;
            }

            int priority = provider.ContextHintPriority;

            if (bestIndex < 0 ||
                priority > bestPriority ||
                (priority == bestPriority && i > bestIndex))
            {
                text = candidateText;
                anchor = candidateAnchor;
                bestPriority = priority;
                bestIndex = i;
            }
        }

        return bestIndex >= 0;
    }

    private void EnsureStyle()
    {
        if (_hintStyle != null)
            return;

        _hintStyle =
            new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        screenEdgePadding = Mathf.Max(0f, screenEdgePadding);
        width = Mathf.Max(120f, width);
        height = Mathf.Max(20f, height);
    }
#endif
}
