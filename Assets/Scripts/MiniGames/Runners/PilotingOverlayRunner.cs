using UnityEngine;
using MiniGames;

[DisallowMultipleComponent]
public sealed class PilotingOverlayRunner : MonoBehaviour
{
    [SerializeField] private MiniGameOverlayHost overlay;

    [Header("Piloting View")]
    [Tooltip("Primary piloting zoom control. This is the navigation-world height visible in the helm mini-game. " +
             "Width is derived from the actual play-area aspect ratio.")]
    [SerializeField, Min(12f)] private float visibleWorldHeight = 90f;

    [Tooltip("When true, runtime systems cannot change the piloting zoom. " +
             "Keep this locked for now; later equipment/progression can provide different allowed views.")]
    [SerializeField] private bool lockZoom = true;

    [Header("Wave Presentation")]
    [Tooltip("Fraction of each trough-to-crest half-wave that stays completely dark before the wave gradient begins. " +
             "0.30 means roughly the first 30% is treated as a broad trough.")]
    [SerializeField, Range(0f, 0.75f)] private float troughFlatFraction = 0.30f;

    [Header("Wave Rendering Performance")]
    [Tooltip("How often the procedural ocean texture is regenerated. " +
             "The cached texture still draws every repaint. 20 Hz is a good starting point.")]
    [SerializeField, Range(5f, 60f)] private float waveTextureRefreshHz = 20f;

    private PilotChairInteractable _activeHelm;
    private PilotingCartridge _activeCartridge;

    private void Reset()
    {
        overlay =
            FindAnyObjectByType<MiniGameOverlayHost>();
    }

    private void Awake()
    {
        ResolveOverlay();
    }

    public bool OpenForHelm(
        PilotChairInteractable helm)
    {
        if (helm == null)
        {
            Debug.LogWarning(
                "[PilotingOverlayRunner] OpenForHelm called with null helm.");
            return false;
        }

        if (!ResolveOverlay())
            return false;

        BoatPilotingSimulation simulation =
            helm.PilotingSimulation;

        BoatPilotingState state =
            simulation != null
                ? simulation.State
                : null;

        if (simulation == null ||
            state == null)
        {
            Debug.LogError(
                "[PilotingOverlayRunner] Helm has no BoatPilotingSimulation/State.",
                helm);

            return false;
        }

        Boat boat =
            helm.OwningBoat;

        string targetId =
            boat != null &&
            !string.IsNullOrWhiteSpace(
                boat.BoatInstanceId)
                ? $"helm:{boat.BoatInstanceId}"
                : $"helm:{helm.GetInstanceID()}";

        var ctx =
            new MiniGameContext
            {
                targetId =
                    targetId,
                difficulty =
                    1f,
                pressure =
                    0f,
                seed =
                    0
            };

        IWaveService waveService =
            ServiceRoot.Instance != null
                ? ServiceRoot.Instance.WaveManager
                : null;

        if (waveService == null)
        {
            Debug.LogWarning(
                "[PilotingOverlayRunner] IWaveService unavailable. " +
                "Piloting wave visuals will not advance from ocean wave propagation.",
                this);
        }

        float physicalBoatWorldX =
            boat != null &&
            boat.rb != null
                ? boat.rb.position.x
                : 0f;

        bool hasPhysicalBoatWorldX =
            boat != null &&
            boat.rb != null;

        _activeHelm =
            helm;

        _activeCartridge =
            new PilotingCartridge(
                state,
                simulation,
                waveService,
                physicalBoatWorldX,
                hasPhysicalBoatWorldX,
                visibleWorldHeight,
                lockZoom,
                troughFlatFraction,
                waveTextureRefreshHz,
                helm);

        overlay.Open(
            _activeCartridge,
            ctx);

        return true;
    }

    public bool IsOpenFor(
        PilotChairInteractable helm)
    {
        bool isOpen =
            helm != null &&
            overlay != null &&
            overlay.IsOpen &&
            _activeHelm == helm &&
            ReferenceEquals(
                overlay.ActiveCartridge,
                _activeCartridge);

        if (!isOpen &&
            _activeHelm == helm)
        {
            _activeHelm =
                null;

            _activeCartridge =
                null;
        }

        return isOpen;
    }

    public void CloseForHelm(
        PilotChairInteractable helm,
        string reason)
    {
        if (helm == null ||
            _activeHelm != helm)
        {
            return;
        }

        if (overlay != null &&
            overlay.IsOpen &&
            ReferenceEquals(
                overlay.ActiveCartridge,
                _activeCartridge))
        {
            overlay.Interrupt(
                string.IsNullOrWhiteSpace(
                    reason)
                    ? "Left helm"
                    : reason);
        }

        _activeHelm =
            null;

        _activeCartridge =
            null;
    }

    private bool ResolveOverlay()
    {
        if (overlay == null)
        {
            overlay =
                FindAnyObjectByType<
                    MiniGameOverlayHost>();
        }

        if (overlay != null)
            return true;

        Debug.LogError(
            "[PilotingOverlayRunner] Missing MiniGameOverlayHost.",
            this);

        return false;
    }
}