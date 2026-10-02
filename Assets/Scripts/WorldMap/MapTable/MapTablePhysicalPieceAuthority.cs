using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct MapTablePhysicalPieceEditHandle
{
    public readonly string pieceId;
    public readonly string requesterPlayerKey;
    public readonly string token;
    public readonly int expectedRevision;

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(pieceId) &&
        !string.IsNullOrWhiteSpace(requesterPlayerKey) &&
        !string.IsNullOrWhiteSpace(token) &&
        expectedRevision > 0;

    public MapTablePhysicalPieceEditHandle(
        string pieceId,
        string requesterPlayerKey,
        string token,
        int expectedRevision)
    {
        this.pieceId = pieceId;
        this.requesterPlayerKey = requesterPlayerKey;
        this.token = token;
        this.expectedRevision = expectedRevision;
    }
}

/// <summary>
/// Authority seam for generic physical markers placed on the shared map table.
/// Pieces are belief/tools, not truth markers, and never auto-follow authoritative world
/// position after initial creation. Prototype stock blocks intentionally carry only color.
/// </summary>
public static class MapTablePhysicalPieceAuthority
{
    private sealed class EditLock
    {
        public string requesterPlayerKey;
        public string token;
        public int expectedRevision;
        public double expiresAtRealtime;
    }

    private const double EditLeaseSeconds = 30.0;
    private static readonly Dictionary<string, EditLock> LocksByPiece = new(StringComparer.Ordinal);

    public static bool EnsurePlayerBoatPiece(
        GameObject requester,
        Vector2 initialWorldPosition,
        out MapTablePhysicalPieceSnapshot piece,
        out string reason)
    {
        piece = null;
        reason = null;

        if (!TryResolveMutableState(requester, out GameState gameState, out CelestialChartStateSnapshot state, out string requesterKey, out reason))
            return false;

        piece = state.GetFirstTablePiece(MapTablePhysicalPieceKind.PlayerBoat);
        if (piece != null)
            return true;

        piece = new MapTablePhysicalPieceSnapshot
        {
            pieceId = "player_boat",
            kind = MapTablePhysicalPieceKind.PlayerBoat,
            color = MapTablePhysicalPieceColor.Blue,
            label = "Player Boat",
            boardWorldPosition = initialWorldPosition,
            rotationDegrees = 0f,
            pinned = false,
            layerOrder = state.GetNextTablePieceLayerOrder(),
            revision = 1,
            lastEditedByPlayerKey = requesterKey
        };

        if (!state.TryAddTablePiece(piece))
        {
            piece = null;
            reason = "The player boat table piece could not be created.";
            return false;
        }

        state.tablePieceRevision++;
        gameState.EnsureCelestialChartDefaults();
        return true;
    }

    public static bool EnsureColorBlockPieces(
        GameObject requester,
        Vector2 initialAnchorWorldPosition,
        int greenCount,
        int redCount,
        int yellowCount,
        int whiteCount,
        int blackCount,
        float spacingWorldUnits,
        out int createdCount,
        out string reason)
    {
        createdCount = 0;
        reason = null;

        if (!TryResolveMutableState(requester, out GameState gameState, out CelestialChartStateSnapshot state, out string requesterKey, out reason))
            return false;

        float spacing = Mathf.Max(1f, spacingWorldUnits);
        int[] counts =
        {
            Mathf.Max(0, greenCount),
            Mathf.Max(0, redCount),
            Mathf.Max(0, yellowCount),
            Mathf.Max(0, whiteCount),
            Mathf.Max(0, blackCount)
        };

        MapTablePhysicalPieceColor[] colors =
        {
            MapTablePhysicalPieceColor.Green,
            MapTablePhysicalPieceColor.Red,
            MapTablePhysicalPieceColor.Yellow,
            MapTablePhysicalPieceColor.White,
            MapTablePhysicalPieceColor.Black
        };

        for (int colorIndex = 0; colorIndex < colors.Length; colorIndex++)
        {
            int count = counts[colorIndex];
            MapTablePhysicalPieceColor color = colors[colorIndex];

            for (int pieceIndex = 0; pieceIndex < count; pieceIndex++)
            {
                string pieceId = BuildStockBlockId(color, pieceIndex);
                if (state.TryGetTablePiece(pieceId, out _))
                    continue;

                float centeredRow = pieceIndex - (count - 1) * 0.5f;
                Vector2 initialPosition = initialAnchorWorldPosition + new Vector2(
                    spacing * (1.5f + colorIndex),
                    spacing * centeredRow);

                var piece = new MapTablePhysicalPieceSnapshot
                {
                    pieceId = pieceId,
                    kind = MapTablePhysicalPieceKind.Custom,
                    color = color,
                    label = $"{color} Block {pieceIndex + 1}",
                    boardWorldPosition = initialPosition,
                    rotationDegrees = 0f,
                    pinned = false,
                    layerOrder = state.GetNextTablePieceLayerOrder(),
                    revision = 1,
                    lastEditedByPlayerKey = requesterKey
                };

                if (!state.TryAddTablePiece(piece))
                {
                    reason = $"The {color} table block #{pieceIndex + 1} could not be created.";
                    return false;
                }

                createdCount++;
            }
        }

        if (createdCount > 0)
        {
            state.tablePieceRevision++;
            gameState.EnsureCelestialChartDefaults();
        }

        return true;
    }

    private static string BuildStockBlockId(MapTablePhysicalPieceColor color, int zeroBasedIndex)
    {
        return $"table_block_{color.ToString().ToLowerInvariant()}_{zeroBasedIndex + 1:00}";
    }

    public static bool TryBeginEdit(
        GameObject requester,
        string pieceId,
        out MapTablePhysicalPieceEditHandle handle,
        out MapTablePhysicalPieceSnapshot piece,
        out string reason)
    {
        handle = default;
        piece = null;
        reason = null;
        CleanupExpiredLocks();

        if (!TryResolveMutableState(requester, out _, out CelestialChartStateSnapshot state, out string requesterKey, out reason))
            return false;

        if (!state.TryGetTablePiece(pieceId, out piece) || piece == null)
        {
            reason = "That table piece is no longer available.";
            return false;
        }

        if (piece.pinned)
        {
            reason = "Unpin that table piece before moving it.";
            return false;
        }

        if (LocksByPiece.TryGetValue(pieceId, out EditLock existing) && existing != null && existing.requesterPlayerKey != requesterKey)
        {
            reason = "Another navigator is currently moving that table piece.";
            return false;
        }

        string token = Guid.NewGuid().ToString("N");
        LocksByPiece[pieceId] = new EditLock
        {
            requesterPlayerKey = requesterKey,
            token = token,
            expectedRevision = Mathf.Max(1, piece.revision),
            expiresAtRealtime = Time.realtimeSinceStartupAsDouble + EditLeaseSeconds
        };

        handle = new MapTablePhysicalPieceEditHandle(pieceId, requesterKey, token, Mathf.Max(1, piece.revision));
        return true;
    }

    public static bool TryCommitEdit(
        GameObject requester,
        MapTablePhysicalPieceEditHandle handle,
        Vector2 worldPosition,
        float rotationDegrees,
        out MapTablePhysicalPieceSnapshot piece,
        out string reason)
    {
        piece = null;
        reason = null;
        CleanupExpiredLocks();

        if (!handle.IsValid)
        {
            reason = "The table-piece edit handle is invalid.";
            return false;
        }

        if (!TryResolveMutableState(requester, out GameState gameState, out CelestialChartStateSnapshot state, out string requesterKey, out reason))
            return false;

        if (requesterKey != handle.requesterPlayerKey)
        {
            reason = "The table-piece requester no longer matches the edit lock.";
            return false;
        }

        if (!LocksByPiece.TryGetValue(handle.pieceId, out EditLock editLock) ||
            editLock == null || editLock.token != handle.token || editLock.requesterPlayerKey != requesterKey)
        {
            reason = "The table-piece edit lock expired or changed.";
            return false;
        }

        if (!state.TryGetTablePiece(handle.pieceId, out piece) || piece == null ||
            piece.revision != handle.expectedRevision || piece.revision != editLock.expectedRevision)
        {
            LocksByPiece.Remove(handle.pieceId);
            reason = "That table piece changed before your edit could be committed.";
            return false;
        }

        if (piece.pinned)
        {
            LocksByPiece.Remove(handle.pieceId);
            reason = "That table piece was pinned before your edit could be committed.";
            return false;
        }

        piece.boardWorldPosition = worldPosition;
        piece.rotationDegrees = Mathf.DeltaAngle(0f, rotationDegrees);
        piece.layerOrder = state.GetNextTablePieceLayerOrder();
        piece.lastEditedByPlayerKey = requesterKey;
        piece.revision++;
        state.tablePieceRevision++;

        LocksByPiece.Remove(handle.pieceId);
        gameState.EnsureCelestialChartDefaults();
        return true;
    }

    public static void CancelEdit(GameObject requester, MapTablePhysicalPieceEditHandle handle)
    {
        if (!handle.IsValid)
            return;

        string requesterKey = ResolvePlayerPersistenceKey(requester, GameState.I);
        if (requesterKey != handle.requesterPlayerKey)
            return;

        if (LocksByPiece.TryGetValue(handle.pieceId, out EditLock editLock) &&
            editLock != null && editLock.token == handle.token && editLock.requesterPlayerKey == requesterKey)
        {
            LocksByPiece.Remove(handle.pieceId);
        }
    }

    public static void ReleaseAllEditsForRequester(GameObject requester)
    {
        string requesterKey = ResolvePlayerPersistenceKey(requester, GameState.I);
        if (string.IsNullOrWhiteSpace(requesterKey))
            return;

        var remove = new List<string>();
        foreach (KeyValuePair<string, EditLock> pair in LocksByPiece)
        {
            if (pair.Value != null && pair.Value.requesterPlayerKey == requesterKey)
                remove.Add(pair.Key);
        }

        for (int i = 0; i < remove.Count; i++)
            LocksByPiece.Remove(remove[i]);
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
            reason = "Map-table piece changes require gameplay authority.";
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

    private static void CleanupExpiredLocks()
    {
        if (LocksByPiece.Count == 0)
            return;

        double now = Time.realtimeSinceStartupAsDouble;
        var remove = new List<string>();
        foreach (KeyValuePair<string, EditLock> pair in LocksByPiece)
        {
            if (pair.Value == null || pair.Value.expiresAtRealtime <= now)
                remove.Add(pair.Key);
        }

        for (int i = 0; i < remove.Count; i++)
            LocksByPiece.Remove(remove[i]);
    }
}
