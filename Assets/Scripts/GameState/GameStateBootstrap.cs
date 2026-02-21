using UnityEngine;

public sealed class GameStateBootstrap : MonoBehaviour
{
    [SerializeField] private GameState prefab; // optional; can be empty

    private void Awake()
    {
        if (GameState.I != null) return;

        if (prefab != null)
            Instantiate(prefab);
        else
            new GameObject("GameState").AddComponent<GameState>();
    }
}