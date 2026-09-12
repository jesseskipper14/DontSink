using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent runtime authority for player-facing message history.
/// Gameplay systems post messages here; HUDs observe the history.
///
/// The component attaches itself to GameState when possible so message history
/// survives NodeScene <-> BoatScene transitions without making UI objects authoritative.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-9000)]
public sealed class GameMessageService : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHistory = 100;
    [SerializeField] private bool autoCreateHud = true;
    [SerializeField] private bool autoCreateWorldObserver = true;

    private static GameMessageService _instance;
    private readonly List<GameMessage> _history = new List<GameMessage>();
    private long _nextSequence = 1;

    public static GameMessageService I => EnsureExists();
    public IReadOnlyList<GameMessage> History => _history;

    public event Action<GameMessage> MessagePosted;
    public event Action HistoryCleared;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        GameMessageService service = EnsureExists();
        service?.EnsurePresentationAndObservers();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        TrimHistory();
        EnsurePresentationAndObservers();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    public static GameMessage Post(
        string text,
        GameMessageType type = GameMessageType.Info,
        string sender = null)
    {
        GameMessageService service = EnsureExists();
        return service != null
            ? service.PostInternal(text, type, sender)
            : null;
    }

    public static GameMessage PostSystem(string text) =>
        Post(text, GameMessageType.System, "SYSTEM");

    public static GameMessage PostInfo(string text) =>
        Post(text, GameMessageType.Info, "SYSTEM");

    public static GameMessage PostWarning(string text) =>
        Post(text, GameMessageType.Warning, "SYSTEM");

    public static GameMessage PostError(string text) =>
        Post(text, GameMessageType.Error, "SYSTEM");

    public static GameMessage PostPlayerChat(
        string sender,
        string text) =>
        Post(text, GameMessageType.PlayerChat, sender);

    public void ClearHistory()
    {
        _history.Clear();
        HistoryCleared?.Invoke();
    }

    private GameMessage PostInternal(
        string text,
        GameMessageType type,
        string sender)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        GameMessage message = new GameMessage(
            _nextSequence++,
            text.Trim(),
            type,
            string.IsNullOrWhiteSpace(sender) ? null : sender.Trim(),
            Time.realtimeSinceStartupAsDouble);

        _history.Add(message);
        TrimHistory();

        MessagePosted?.Invoke(message);
        return message;
    }

    private void TrimHistory()
    {
        int limit = Mathf.Max(1, maxHistory);
        int overflow = _history.Count - limit;

        if (overflow > 0)
            _history.RemoveRange(0, overflow);
    }

    private void EnsurePresentationAndObservers()
    {
        EnsureHud();
        EnsureWorldObserver();
    }

    private void EnsureHud()
    {
        if (!autoCreateHud)
            return;

        GameMessageHUD existing =
            GetComponent<GameMessageHUD>();

        if (existing == null)
            gameObject.AddComponent<GameMessageHUD>();
    }

    private void EnsureWorldObserver()
    {
        if (!autoCreateWorldObserver)
            return;

        GameMessageWorldObserver existing =
            GetComponent<GameMessageWorldObserver>();

        if (existing == null)
            gameObject.AddComponent<GameMessageWorldObserver>();
    }

    private static GameMessageService EnsureExists()
    {
        if (_instance != null)
            return _instance;

        _instance = FindAnyObjectByType<GameMessageService>(FindObjectsInactive.Include);
        if (_instance != null)
            return _instance;

        GameObject host = null;

        if (GameState.I != null)
            host = GameState.I.gameObject;

        if (host == null)
        {
            host = new GameObject("[GameMessageService]");
            DontDestroyOnLoad(host);
        }

        _instance = host.GetComponent<GameMessageService>();
        if (_instance == null)
            _instance = host.AddComponent<GameMessageService>();

        return _instance;
    }
}
