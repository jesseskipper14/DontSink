using UnityEngine;

[DisallowMultipleComponent]
public sealed class OceanWaterPresentationController : MonoBehaviour
{
    [Header("Ocean Renderers")]
    [SerializeField] private Renderer[] backgroundOceanRenderers;
    [SerializeField] private Renderer[] foregroundOceanRenderers;

    [Header("Mode Visibility")]
    [SerializeField] private bool showBackgroundInInterior = true;
    [SerializeField] private bool showForegroundInInterior = false;

    [SerializeField] private bool showBackgroundWhenBoardedExterior = false;
    [SerializeField] private bool showForegroundWhenBoardedExterior = true;

    [SerializeField] private bool showBackgroundWhenUnboarded = false;
    [SerializeField] private bool showForegroundWhenUnboarded = true;

    [SerializeField] private bool showBackgroundInTransition = true;
    [SerializeField] private bool showForegroundInTransition = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = false;

    public void ApplyMode(BoatVisibilityMode mode)
    {
        bool backgroundVisible;
        bool foregroundVisible;

        switch (mode)
        {
            case BoatVisibilityMode.BoardedInterior:
                backgroundVisible = showBackgroundInInterior;
                foregroundVisible = showForegroundInInterior;
                break;

            case BoatVisibilityMode.BoardedExteriorDeck:
                backgroundVisible = showBackgroundWhenBoardedExterior;
                foregroundVisible = showForegroundWhenBoardedExterior;
                break;

            case BoatVisibilityMode.UnboardedExterior:
                backgroundVisible = showBackgroundWhenUnboarded;
                foregroundVisible = showForegroundWhenUnboarded;
                break;

            case BoatVisibilityMode.Transition:
                backgroundVisible = showBackgroundInTransition;
                foregroundVisible = showForegroundInTransition;
                break;

            default:
                backgroundVisible = false;
                foregroundVisible = true;
                break;
        }

        SetRenderers(backgroundOceanRenderers, backgroundVisible);
        SetRenderers(foregroundOceanRenderers, foregroundVisible);

        Log($"ApplyMode {mode} | back={backgroundVisible} front={foregroundVisible}");
    }

    private static void SetRenderers(Renderer[] renderers, bool visible)
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = visible;
        }
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[OceanWaterPresentationController:{name}] {msg}", this);
    }
}