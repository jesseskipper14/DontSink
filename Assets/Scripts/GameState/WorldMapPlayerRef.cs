using UnityEngine;

public sealed class WorldMapPlayerRef : MonoBehaviour
{
    public WorldMapPlayerState State
    {
        get
        {
            if (GameState.I == null)
                return null; // keep it safe for now
            return GameState.I.player;
        }
    }
}

