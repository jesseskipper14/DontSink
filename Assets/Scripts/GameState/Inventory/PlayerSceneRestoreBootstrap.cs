using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerSceneRestoreBootstrap : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("[PlayerSceneRestoreBootstrap] Start called. Attempting restore.", this);

        var owner = GetComponentInParent<PlayerLoadoutPersistence>()
            ?? GetComponentInChildren<PlayerLoadoutPersistence>(true);
        if (owner != null)
        {
            if (owner.TryBindPersistenceIdentity(out string key) &&
                GameState.I != null && GameState.I.GetPlayerLoadout(key) != null)
                owner.RestoreFromGameState();
            return;
        }

        if (SceneTransitionController.I == null)
        {
            Debug.LogWarning("[PlayerSceneRestoreBootstrap] SceneTransitionController missing.", this);
            return;
        }

        SceneTransitionController.I.RestoreCurrentPlayerLoadout();
    }
}
