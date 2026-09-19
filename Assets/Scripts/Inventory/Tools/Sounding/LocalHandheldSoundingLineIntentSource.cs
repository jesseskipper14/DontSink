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
    MonoBehaviour
{
    [SerializeField] private HandheldSoundingLineController controller;

    [Tooltip(
        "Prototype use key. Stowed = deploy. Deployed = retrieve. " +
        "Press again during retrieval = release/free-payout again.")]
    [SerializeField]
    private KeyCode useKey =
        KeyCode.G;

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
