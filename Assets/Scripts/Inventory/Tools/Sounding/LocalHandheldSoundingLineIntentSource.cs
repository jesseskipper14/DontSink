using UnityEngine;

/// <summary>
/// Local singleplayer input -> sounding-line intent bridge.
///
/// This component owns no sounding-line state. It only requests actions from
/// HandheldSoundingLineController. Later, a multiplayer client can replace this
/// local hop with network delivery to host authority.
/// </summary>
[DisallowMultipleComponent]
public sealed class LocalHandheldSoundingLineIntentSource :
    MonoBehaviour,
    IContextHintProvider
{
    [SerializeField] private HandheldSoundingLineController controller;

    [Tooltip(
        "Prototype use key. Stowed = deploy. Deployed = retrieve. " +
        "Press again during retrieval = release/free-payout again.")]
    [SerializeField]
    private KeyCode useKey =
        KeyCode.G;

    [Header("Context Hint")]
    [SerializeField] private int contextHintPriority = 200;

    [Header("Debug")]
    [SerializeField] private bool logIntentResults;

    private void Reset()
    {
        ResolveController();
    }

    private void Awake()
    {
        ResolveController();
    }

    private void OnEnable()
    {
        ContextHintOverlay.Register(this);
    }

    private void OnDisable()
    {
        ContextHintOverlay.Unregister(this);
    }

    public int ContextHintPriority => contextHintPriority;

    public bool TryGetContextHint(
        out string text,
        out Transform worldAnchor)
    {
        ResolveController();

        text = null;
        worldAnchor = controller != null ? controller.transform : transform;

        if (controller == null ||
            !controller.ShouldShowReadout ||
            GameplayInputBlocker.IsBlocked)
        {
            return false;
        }

        string key =
            useKey == KeyCode.None
                ? "USE"
                : useKey.ToString().ToUpperInvariant();

        text =
            controller.Status switch
            {
                HandheldSoundingLineController.RuntimeStatus.Ready =>
                    $"PRESS {key} TO DEPLOY",

                HandheldSoundingLineController.RuntimeStatus.Sinking =>
                    $"PRESS {key} TO RETRIEVE",

                HandheldSoundingLineController.RuntimeStatus.Bottom =>
                    $"PRESS {key} TO RETRIEVE",

                HandheldSoundingLineController.RuntimeStatus.FullyExtended =>
                    $"PRESS {key} TO RETRIEVE",

                HandheldSoundingLineController.RuntimeStatus.Retrieving =>
                    $"PRESS {key} TO RELEASE",

                HandheldSoundingLineController.RuntimeStatus.BoardedBlocked =>
                    "MOVE TO EXTERIOR DECK",

                HandheldSoundingLineController.RuntimeStatus.NoLine =>
                    "LOAD LINE TO DEPLOY",

                _ => string.Empty
            };

        return !string.IsNullOrWhiteSpace(text);
    }

    private void Update()
    {
        if (controller == null)
        {
            ResolveController();
            return;
        }

        if (!Input.GetKeyDown(
                useKey))
        {
            return;
        }

        HandheldSoundingLineIntent intent;

        if (!controller.IsDeployed)
        {
            intent =
                HandheldSoundingLineIntent.Deploy;
        }
        else if (controller.IsRetrieving)
        {
            intent =
                HandheldSoundingLineIntent.Release;
        }
        else
        {
            intent =
                HandheldSoundingLineIntent.Retrieve;
        }

        HandheldSoundingLineIntentRequest request =
            HandheldSoundingLineIntentRequest.Create(
                intent);

        bool ok =
            controller.TryApplyIntent(
                request,
                out string message);

        if (logIntentResults)
        {
            Debug.Log(
                $"[SoundingLineIntent:{name}] {intent} -> {(ok ? "ACCEPTED" : "REJECTED")} | {message}",
                this);
        }
    }

    private void ResolveController()
    {
        if (controller != null)
            return;

        controller =
            GetComponent<HandheldSoundingLineController>();

        if (controller == null)
        {
            controller =
                GetComponentInChildren<HandheldSoundingLineController>(
                    true);
        }

        if (controller == null)
        {
            controller =
                GetComponentInParent<HandheldSoundingLineController>();
        }
    }
}
