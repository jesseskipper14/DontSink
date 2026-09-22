using System;

[Serializable]
public sealed class PlayerPersistenceStateSnapshot
{
    public int version = 1;

    /// <summary>
    /// Stable persistence key for one player. This is a save/runtime routing key,
    /// not a trusted network identity. A future multiplayer transport/bootstrap
    /// should authenticate the sender/player and assign the appropriate key.
    /// </summary>
    public string playerKey;

    public PlayerLoadoutSnapshot loadout;
    public PlayerSceneContextSnapshot sceneContext;
}
