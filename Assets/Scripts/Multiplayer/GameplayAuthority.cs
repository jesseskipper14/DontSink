using UnityEngine;

public static class GameplayAuthority
{
    private static object sessionOwner;
    public static bool IsAuthoritative { get; private set; } = true;

    public static bool IsMultiplayerClientOnly => !IsAuthoritative;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSessionRole()
    {
        sessionOwner = null;
        IsAuthoritative = true;
    }

    public static bool TryBindSessionRole(object owner, bool authoritative)
    {
        if (owner == null || (sessionOwner != null && !ReferenceEquals(sessionOwner, owner))) return false;
        sessionOwner = owner;
        IsAuthoritative = authoritative;
        return true;
    }

    public static void ReleaseSessionRole(object owner)
    {
        if (!ReferenceEquals(sessionOwner, owner)) return;
        sessionOwner = null;
        IsAuthoritative = true;
    }

    public static void SetAuthoritativeForDebug(
        bool authoritative,
        bool logEvenIfUnchanged = false)
    {
        if (sessionOwner != null)
        {
            Debug.LogWarning("[GameplayAuthority] Debug role override ignored while a network session owns the role.");
            return;
        }
        if (IsAuthoritative == authoritative && !logEvenIfUnchanged)
            return;

        IsAuthoritative = authoritative;
        Debug.Log($"[GameplayAuthority] IsAuthoritative={IsAuthoritative}");
    }

    public static bool CanRun(GameplayAuthorityMode mode)
    {
        switch (mode)
        {
            case GameplayAuthorityMode.SinglePlayerOrAuthoritative:
            case GameplayAuthorityMode.AuthoritativeOnly:
                return IsAuthoritative;

            case GameplayAuthorityMode.VisualOnlyClient:
                return !IsAuthoritative;

            default:
                return IsAuthoritative;
        }
    }
}
