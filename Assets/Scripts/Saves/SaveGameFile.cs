using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SaveGameFile
{
    public int schemaVersion = SaveSchema.CurrentVersion;

    public string gameVersion;
    public string saveId;
    public string profileId;
    public string displayName;

    public string createdUtc;
    public string updatedUtc;

    public SaveGamePayload payload;
}

[Serializable]
public sealed class SaveGamePayload
{
    public string currentSceneName;

    public WorldMapPlayerState player;
    public WorldMapSimState worldMap;

    public WorldMapSaveSnapshot worldMapSnapshot;

    // v1 is NodeScene-only, so activeTravel should normally be null.
    // Kept here deliberately so the schema has an obvious home for later migrations.
    public TravelPayload activeTravel;

    // Legacy local-player mirrors retained so current/older builds can continue
    // reading schema-v1 saves without migration.
    public PlayerLoadoutSnapshot playerLoadout;
    public PlayerSceneContextSnapshot playerSceneContext;

    // Additive player-aware persistence seam. Old schema-v1 saves simply deserialize
    // this as null; new saves continue writing the legacy mirrors above as well.
    public List<PlayerPersistenceStateSnapshot> playerPersistenceStates;

    public BoatSaveState boat;

    public MoneyChestTreasurySnapshot moneyChestTreasury;
}

public readonly struct SaveGameResult
{
    public readonly bool success;
    public readonly string message;
    public readonly string path;

    public SaveGameResult(bool success, string message, string path = null)
    {
        this.success = success;
        this.message = message;
        this.path = path;
    }

    public override string ToString()
    {
        return success
            ? $"SUCCESS: {message} | path='{path}'"
            : $"FAILED: {message} | path='{path}'";
    }
}