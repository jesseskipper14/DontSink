using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct CelestialChartBoardEditHandle
{
    public readonly string fragmentId;
    public readonly string requesterPlayerKey;
    public readonly string token;
    public readonly int expectedRevision;

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(fragmentId) &&
        !string.IsNullOrWhiteSpace(requesterPlayerKey) &&
        !string.IsNullOrWhiteSpace(token) &&
        expectedRevision > 0;

    public CelestialChartBoardEditHandle(
        string fragmentId,
        string requesterPlayerKey,
        string token,
        int expectedRevision)
    {
        this.fragmentId = fragmentId;
        this.requesterPlayerKey = requesterPlayerKey;
        this.token = token;
        this.expectedRevision = expectedRevision;
    }
}

/// <summary>
/// Authority seam for mutable shared chart-board belief state.
///
/// Opening/viewing the map table is deliberately local presentation. Moving, rotating,
/// pinning, layering, and returning scraps to the folio mutate shared crew state and must
/// cross this boundary. The requester is the exact interactor GameObject, not a client-
/// authored player id.
///
/// Long drags acquire a short-lived edit lock, preview locally, then commit once on release.
/// That keeps future networking from sending one mutation per mouse pixel and provides an
/// explicit conflict/revision seam before a transport exists.
/// </summary>
public static class CelestialChartBoardAuthority
{
    private sealed class EditLock
    {
        public string requesterPlayerKey;
        public string token;
        public int expectedRevision;
        public double expiresAtRealtime;
    }

    private const double EditLeaseSeconds = 30.0;
    private static readonly Dictionary<string, EditLock> LocksByFragment =
        new(StringComparer.Ordinal);

    public static bool TryPlaceFragment(
        GameObject requester,
        string fragmentId,
        Vector2 boardCenterWorld,
        float rotationDegrees,
        out CelestialChartBoardPlacementSnapshot placement,
        out string reason)
    {
        placement = null;
        reason = null;

        if (!TryResolveMutableState(requester, out GameState gameState, out CelestialChartStateSnapshot state, out string requesterKey, out reason))
            return false;

        if (!TryResolveFragment(state, fragmentId, out _))
        {
            reason = "That chart fragment no longer exists.";
            return false;
        }

        if (state.TryGetPlacement(fragmentId, out CelestialChartBoardPlacementSnapshot existing))
        {
            placement = existing;
            reason = "That chart fragment is already on the table.";
            return false;
        }

        placement = new CelestialChartBoardPlacementSnapshot
        {
            fragmentId = fragmentId,
            boardCenterWorld = boardCenterWorld,
            rotationDegrees = NormalizeDegrees(rotationDegrees),
            pinned = false,
            layerOrder = state.GetNextLayerOrder(),
            revision = 1,
            lastEditedByPlayerKey = requesterKey
        };

        if (!state.TryAddPlacement(placement))
        {
            placement = null;
            reason = "The chart fragment could not be placed on the shared board.";
            return false;
        }

        state.boardRevision++;
        gameState.EnsureCelestialChartDefaults();
        return true;
    }

    public static bool TryBeginEdit(
        GameObject requester,
        string fragmentId,
        out CelestialChartBoardEditHandle handle,
        out CelestialChartBoardPlacementSnapshot placement,
        out string reason)
    {
        handle = default;
        placement = null;
        reason = null;

        CleanupExpiredLocks();

        if (!TryResolveMutableState(requester, out _, out CelestialChartStateSnapshot state, out string requesterKey, out reason))
            return false;

        if (!state.TryGetPlacement(fragmentId, out placement) || placement == null)
        {
            reason = "That chart fragment is not currently on the table.";
            return false;
        }

        if (placement.pinned)
        {
            reason = "Unpin the chart fragment before moving or rotating it.";
            return false;
        }

        if (LocksByFragment.TryGetValue(fragmentId, out EditLock existing))
        {
            if (existing.requesterPlayerKey != requesterKey)
            {
                reason = "Another navigator is currently editing that chart fragment.";
                return false;
            }

            LocksByFragment.Remove(fragmentId);
        }

        string token = Guid.NewGuid().ToString("N");
        LocksByFragment[fragmentId] = new EditLock
        {
            requesterPlayerKey = requesterKey,
            token = token,
            expectedRevision = Mathf.Max(1, placement.revision),
            expiresAtRealtime = Time.realtimeSinceStartupAsDouble + EditLeaseSeconds
        };

        handle = new CelestialChartBoardEditHandle(
            fragmentId,
            requesterKey,
            token,
            Mathf.Max(1, placement.revision));

        return true;
    }

    public static bool TryCommitEdit(
        GameObject requester,
        CelestialChartBoardEditHandle handle,
        Vector2 boardCenterWorld,
        float rotationDegrees,
        bool bringToFront,
        out CelestialChartBoardPlacementSnapshot placement,
        out string reason)
    {
        placement = null;
        reason = null;

        CleanupExpiredLocks();

        if (!handle.IsValid)
        {
            reason = "The chart edit handle is invalid.";
            return false;
        }

        if (!TryResolveMutableState(requester, out GameState gameState, out CelestialChartStateSnapshot state, out string requesterKey, out reason))
            return false;

        if (requesterKey != handle.requesterPlayerKey)
        {
            reason = "The chart edit requester no longer matches the edit lock.";
            return false;
        }

        if (!LocksByFragment.TryGetValue(handle.fragmentId, out EditLock editLock) ||
            editLock.token != handle.token ||
            editLock.requesterPlayerKey != requesterKey)
        {
            reason = "The chart fragment edit lock expired or changed.";
            return false;
        }

        if (!state.TryGetPlacement(handle.fragmentId, out placement) || placement == null)
        {
            LocksByFragment.Remove(handle.fragmentId);
            reason = "That chart fragment is no longer on the table.";
            return false;
        }

        if (placement.revision != handle.expectedRevision || placement.revision != editLock.expectedRevision)
        {
            LocksByFragment.Remove(handle.fragmentId);
            reason = "That chart fragment changed before your edit could be committed.";
            return false;
        }

        if (placement.pinned)
        {
            LocksByFragment.Remove(handle.fragmentId);
            reason = "That chart fragment was pinned before your edit could be committed.";
            return false;
        }

        placement.boardCenterWorld = boardCenterWorld;
        placement.rotationDegrees = NormalizeDegrees(rotationDegrees);
        if (bringToFront)
            placement.layerOrder = state.GetNextLayerOrder();
        placement.lastEditedByPlayerKey = requesterKey;
        placement.revision++;
        state.boardRevision++;

        LocksByFragment.Remove(handle.fragmentId);
        gameState.EnsureCelestialChartDefaults();
        return true;
    }

    public static void CancelEdit(GameObject requester, CelestialChartBoardEditHandle handle)
    {
        if (!handle.IsValid)
            return;

        CleanupExpiredLocks();

        string requesterKey = ResolvePlayerPersistenceKey(requester, GameState.I);
        if (requesterKey != handle.requesterPlayerKey)
            return;

        if (LocksByFragment.TryGetValue(handle.fragmentId, out EditLock editLock) &&
            editLock.token == handle.token &&
            editLock.requesterPlayerKey == requesterKey)
        {
            LocksByFragment.Remove(handle.fragmentId);
        }
    }

    public static void ReleaseAllEditsForRequester(GameObject requester)
    {
        if (requester == null)
            return;

        string requesterKey = ResolvePlayerPersistenceKey(requester, GameState.I);
        if (string.IsNullOrWhiteSpace(requesterKey))
            return;

        var toRemove = new List<string>();
        foreach (KeyValuePair<string, EditLock> pair in LocksByFragment)
        {
            if (pair.Value != null && pair.Value.requesterPlayerKey == requesterKey)
                toRemove.Add(pair.Key);
        }

        for (int i = 0; i < toRemove.Count; i++)
            LocksByFragment.Remove(toRemove[i]);
    }

    public static bool TrySetPinned(
        GameObject requester,
        string fragmentId,
        int expectedRevision,
        bool pinned,
        out CelestialChartBoardPlacementSnapshot placement,
        out string reason)
    {
        placement = null;
        reason = null;

        if (!TryResolveMutableState(requester, out GameState gameState, out CelestialChartStateSnapshot state, out string requesterKey, out reason))
            return false;

        if (!ValidateOneShotPlacementMutation(state, fragmentId, expectedRevision, requesterKey, out placement, out reason))
            return false;

        placement.pinned = pinned;
        placement.lastEditedByPlayerKey = requesterKey;
        placement.revision++;
        state.boardRevision++;
        gameState.EnsureCelestialChartDefaults();
        return true;
    }

    public static bool TryRotateBy(
        GameObject requester,
        string fragmentId,
        int expectedRevision,
        float deltaDegrees,
        out CelestialChartBoardPlacementSnapshot placement,
        out string reason)
    {
        placement = null;
        reason = null;

        if (!TryResolveMutableState(requester, out GameState gameState, out CelestialChartStateSnapshot state, out string requesterKey, out reason))
            return false;

        if (!ValidateOneShotPlacementMutation(state, fragmentId, expectedRevision, requesterKey, out placement, out reason))
            return false;

        if (placement.pinned)
        {
            reason = "Unpin the chart fragment before rotating it.";
            return false;
        }

        placement.rotationDegrees = NormalizeDegrees(placement.rotationDegrees + deltaDegrees);
        placement.lastEditedByPlayerKey = requesterKey;
        placement.revision++;
        state.boardRevision++;
        gameState.EnsureCelestialChartDefaults();
        return true;
    }

    public static bool TryBringToFront(
        GameObject requester,
        string fragmentId,
        int expectedRevision,
        out CelestialChartBoardPlacementSnapshot placement,
        out string reason)
    {
        placement = null;
        reason = null;

        if (!TryResolveMutableState(requester, out GameState gameState, out CelestialChartStateSnapshot state, out string requesterKey, out reason))
            return false;

        if (!ValidateOneShotPlacementMutation(state, fragmentId, expectedRevision, requesterKey, out placement, out reason))
            return false;

        placement.layerOrder = state.GetNextLayerOrder();
        placement.lastEditedByPlayerKey = requesterKey;
        placement.revision++;
        state.boardRevision++;
        gameState.EnsureCelestialChartDefaults();
        return true;
    }

    public static bool TryReturnToFolio(
        GameObject requester,
        string fragmentId,
        int expectedRevision,
        out string reason)
    {
        reason = null;

        if (!TryResolveMutableState(requester, out GameState gameState, out CelestialChartStateSnapshot state, out string requesterKey, out reason))
            return false;

        if (!ValidateOneShotPlacementMutation(state, fragmentId, expectedRevision, requesterKey, out CelestialChartBoardPlacementSnapshot placement, out reason))
            return false;

        if (placement.pinned)
        {
            reason = "Unpin the chart fragment before returning it to the folio.";
            return false;
        }

        if (!state.RemovePlacement(fragmentId))
        {
            reason = "The chart fragment could not be removed from the board.";
            return false;
        }

        state.boardRevision++;
        gameState.EnsureCelestialChartDefaults();
        LocksByFragment.Remove(fragmentId);
        return true;
    }

    private static bool ValidateOneShotPlacementMutation(
        CelestialChartStateSnapshot state,
        string fragmentId,
        int expectedRevision,
        string requesterKey,
        out CelestialChartBoardPlacementSnapshot placement,
        out string reason)
    {
        placement = null;
        reason = null;
        CleanupExpiredLocks();

        if (!state.TryGetPlacement(fragmentId, out placement) || placement == null)
        {
            reason = "That chart fragment is not currently on the table.";
            return false;
        }

        if (placement.revision != expectedRevision)
        {
            reason = "That chart fragment changed before your action could be applied.";
            return false;
        }

        if (LocksByFragment.TryGetValue(fragmentId, out EditLock editLock) && editLock != null)
        {
            reason = editLock.requesterPlayerKey == requesterKey
                ? "Finish the current fragment edit before applying another board action."
                : "Another navigator is currently editing that chart fragment.";
            return false;
        }

        return true;
    }

    private static bool TryResolveMutableState(
        GameObject requester,
        out GameState gameState,
        out CelestialChartStateSnapshot state,
        out string requesterKey,
        out string reason)
    {
        gameState = null;
        state = null;
        requesterKey = null;
        reason = null;

        if (!GameplayAuthority.IsAuthoritative)
        {
            reason = "Chart-board changes require gameplay authority.";
            return false;
        }

        if (requester == null)
        {
            reason = "No map-table requester is available.";
            return false;
        }

        gameState = GameState.I;
        if (gameState == null)
        {
            reason = "GameState is unavailable.";
            return false;
        }

        gameState.EnsureCelestialChartDefaults();
        state = gameState.celestialCharts;
        requesterKey = ResolvePlayerPersistenceKey(requester, gameState);

        if (string.IsNullOrWhiteSpace(requesterKey))
        {
            reason = "The map-table requester has no persistence identity.";
            return false;
        }

        return true;
    }

    private static bool TryResolveFragment(
        CelestialChartStateSnapshot state,
        string fragmentId,
        out CelestialChartFragmentSnapshot fragment)
    {
        fragment = null;
        if (state == null || string.IsNullOrWhiteSpace(fragmentId))
            return false;

        state.EnsureDefaults();
        for (int i = 0; i < state.fragments.Count; i++)
        {
            CelestialChartFragmentSnapshot candidate = state.fragments[i];
            if (candidate != null && candidate.fragmentId == fragmentId)
            {
                fragment = candidate;
                return true;
            }
        }

        return false;
    }

    private static string ResolvePlayerPersistenceKey(GameObject requester, GameState gameState)
    {
        if (requester == null)
            return gameState != null ? gameState.LocalPlayerPersistenceKey : null;

        PlayerLoadoutPersistence persistence =
            requester.GetComponent<PlayerLoadoutPersistence>() ??
            requester.GetComponentInParent<PlayerLoadoutPersistence>(true) ??
            requester.GetComponentInChildren<PlayerLoadoutPersistence>(true);

        return persistence != null
            ? persistence.PersistenceKey
            : gameState != null
                ? gameState.LocalPlayerPersistenceKey
                : null;
    }

    private static float NormalizeDegrees(float degrees)
    {
        if (float.IsNaN(degrees) || float.IsInfinity(degrees))
            return 0f;

        return Mathf.DeltaAngle(0f, degrees);
    }

    private static void CleanupExpiredLocks()
    {
        if (LocksByFragment.Count == 0)
            return;

        double now = Time.realtimeSinceStartupAsDouble;
        var expired = new List<string>();

        foreach (KeyValuePair<string, EditLock> pair in LocksByFragment)
        {
            if (pair.Value == null || pair.Value.expiresAtRealtime <= now)
                expired.Add(pair.Key);
        }

        for (int i = 0; i < expired.Count; i++)
            LocksByFragment.Remove(expired[i]);
    }
}
