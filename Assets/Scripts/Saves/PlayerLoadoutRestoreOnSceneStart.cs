using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerLoadoutRestoreOnSceneStart : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField, Min(0f)] private float restoreDelaySeconds = 0.05f;

    [Header("Behavior")]
    [SerializeField] private bool restoreOnlyIfGameStateHasLoadout = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    private bool _restored;

    private void Start()
    {
        StartCoroutine(RestoreRoutine());
    }

    private IEnumerator RestoreRoutine()
    {
        if (_restored)
            yield break;

        if (restoreDelaySeconds > 0f)
            yield return new WaitForSeconds(restoreDelaySeconds);

        GameState gs = GameState.I;
        if (gs == null)
        {
            LogWarning("Skipped restore: GameState.I is null.");
            yield break;
        }

        if (restoreOnlyIfGameStateHasLoadout && gs.playerLoadout == null)
        {
            Log("Skipped restore: GameState.playerLoadout is null.");
            yield break;
        }

        SceneTransitionController transition = SceneTransitionController.I;
        if (transition != null)
        {
            transition.RestoreCurrentPlayerLoadout();
            _restored = true;
            yield break;
        }

        PlayerLoadoutPersistence persistence = GetComponent<PlayerLoadoutPersistence>();
        if (persistence == null)
            persistence = Object.FindAnyObjectByType<PlayerLoadoutPersistence>();

        if (persistence == null)
        {
            LogWarning("Skipped restore: no PlayerLoadoutPersistence found.");
            yield break;
        }

        persistence.RestoreFromGameState();
        _restored = true;

        Log("Restored player loadout from GameState.");
    }

    private void Log(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.Log($"[PlayerLoadoutRestoreOnSceneStart:{name}] {msg}", this);
    }

    private void LogWarning(string msg)
    {
        if (!verboseLogging)
            return;

        Debug.LogWarning($"[PlayerLoadoutRestoreOnSceneStart:{name}] {msg}", this);
    }
}