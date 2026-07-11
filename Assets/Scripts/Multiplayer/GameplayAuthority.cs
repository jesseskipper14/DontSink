using UnityEngine;

public static class GameplayAuthority
{
    public static bool IsAuthoritative { get; private set; } = true;

    public static bool IsMultiplayerClientOnly => !IsAuthoritative;

    public static void SetAuthoritativeForDebug(
        bool authoritative,
        bool logEvenIfUnchanged = false)
    {
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