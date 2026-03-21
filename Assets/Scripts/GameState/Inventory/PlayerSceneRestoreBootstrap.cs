using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerSceneRestoreBootstrap : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("[PlayerSceneRestoreBootstrap] Start called. Attempting restore.", this);

        if (SceneTransitionController.I == null)
        {
            Debug.LogWarning("[PlayerSceneRestoreBootstrap] SceneTransitionController missing.", this);
            return;
        }

        SceneTransitionController.I.RestoreCurrentPlayerLoadout();
    }
}