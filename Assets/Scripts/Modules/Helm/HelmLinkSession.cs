using UnityEngine;

/// <summary>
/// Per-interactor, transient two-step Helm linking state.
///
/// Added to the player's GameObject on demand, so there is no prefab setup.
/// This is deliberately NOT persistence. The completed chair <-> hardpoint
/// relationship is stored on those runtime objects; save/load persistence can
/// serialize that relationship in a later pass.
/// </summary>
[DisallowMultipleComponent]
public sealed class HelmLinkSession : MonoBehaviour
{
    private Hardpoint _pendingHelmHardpoint;

    public Hardpoint PendingHelmHardpoint =>
        _pendingHelmHardpoint;

    public bool HasPendingHelm =>
        _pendingHelmHardpoint != null;

    public static HelmLinkSession Get(
        GameObject interactor)
    {
        return interactor != null
            ? interactor.GetComponent<HelmLinkSession>()
            : null;
    }

    public static HelmLinkSession GetOrCreate(
        GameObject interactor)
    {
        if (interactor == null)
            return null;

        HelmLinkSession session =
            interactor.GetComponent<HelmLinkSession>();

        if (session == null)
            session = interactor.AddComponent<HelmLinkSession>();

        return session;
    }

    public void Begin(
        Hardpoint helmHardpoint)
    {
        _pendingHelmHardpoint =
            helmHardpoint;
    }

    public void Cancel()
    {
        _pendingHelmHardpoint =
            null;
    }
}