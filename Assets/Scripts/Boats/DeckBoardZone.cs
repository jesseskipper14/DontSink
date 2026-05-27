using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class DeckBoardZone : MonoBehaviour, IInteractable, IInteractPromptProvider
{
    [Header("Interaction")]
    [SerializeField] private int priority = 60;

    [Tooltip("How long the player must hold the board key while inside the zone.")]
    [SerializeField, Min(0f)] private float holdSeconds = 0.35f;

    [SerializeField] private KeyCode primaryHoldKey = KeyCode.E;

    [Tooltip("Optional second key. Useful if you want 'hold W to climb aboard' behavior.")]
    [SerializeField] private bool allowSecondaryHoldKey = false;

    [SerializeField] private KeyCode secondaryHoldKey = KeyCode.W;

    [Header("Board Target")]
    [Tooltip("Optional explicit boat root. If unset, resolves from parent Boat.")]
    [SerializeField] private Transform boatRootOverride;

    [Tooltip("Optional point to place the player when boarding succeeds. Recommended.")]
    [SerializeField] private Transform boardPoint;

    [SerializeField] private bool snapToBoardPoint = true;
    [SerializeField] private bool zeroVelocityOnBoard = true;

    [Header("Rules")]
    [Tooltip("If true, only players who are currently unboarded can use this zone.")]
    [SerializeField] private bool requireUnboarded = true;

    [Tooltip("If true, only the closest eligible player in the zone progresses the hold timer.")]
    [SerializeField] private bool onlyBoardClosestEligiblePlayer = true;

    [Header("Prompt")]
    [SerializeField] private string promptText = "Board Deck";
    [SerializeField] private bool includeHoldKeyInPrompt = true;
    [SerializeField] private bool includeProgressInPrompt = false;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    public int InteractionPriority => priority;

    private readonly Dictionary<PlayerBoardingState, int> _overlapCounts = new();
    private readonly Dictionary<PlayerBoardingState, float> _holdTimers = new();
    private readonly List<PlayerBoardingState> _scratchPlayers = new();

    private Collider2D _trigger;
    private Boat _cachedBoat;
    private bool _missingBoatLogged;

    private void Awake()
    {
        _trigger = GetComponent<Collider2D>();

        if (_trigger != null && !_trigger.isTrigger)
        {
            Debug.LogWarning(
                $"{name}: DeckBoardZone expects its Collider2D to be set as Is Trigger.",
                this);
        }

        CacheBoat();
    }

    private void OnValidate()
    {
        holdSeconds = Mathf.Max(0f, holdSeconds);

        if (_trigger == null)
            _trigger = GetComponent<Collider2D>();
    }

    private void OnDisable()
    {
        _overlapCounts.Clear();
        _holdTimers.Clear();
        _scratchPlayers.Clear();
    }

    private void Update()
    {
        if (_overlapCounts.Count == 0)
            return;

        bool holdPressed = IsBoardHoldPressed();

        PlayerBoardingState closestEligible = null;

        if (onlyBoardClosestEligiblePlayer)
            closestEligible = FindClosestEligiblePlayer();

        _scratchPlayers.Clear();

        foreach (var pair in _overlapCounts)
            _scratchPlayers.Add(pair.Key);

        for (int i = 0; i < _scratchPlayers.Count; i++)
        {
            PlayerBoardingState boarding = _scratchPlayers[i];

            if (boarding == null)
                continue;

            if (!CanBoard(boarding))
            {
                _holdTimers[boarding] = 0f;
                continue;
            }

            if (onlyBoardClosestEligiblePlayer && boarding != closestEligible)
            {
                _holdTimers[boarding] = 0f;
                continue;
            }

            if (!holdPressed)
            {
                _holdTimers[boarding] = 0f;
                continue;
            }

            float timer = GetHoldTimer(boarding) + Time.deltaTime;
            _holdTimers[boarding] = timer;

            if (timer >= holdSeconds)
            {
                TryBoard(boarding);
                _holdTimers[boarding] = 0f;
            }
        }

        CleanupNullPlayers();
    }

    public bool CanInteract(in InteractContext context)
    {
        PlayerBoardingState boarding = FindBoardingState(context);
        if (boarding == null)
            return false;

        if (!IsInsideZone(boarding))
            return false;

        return CanBoard(boarding);
    }

    public void Interact(in InteractContext context)
    {
        // Boarding is intentionally hold-driven in Update().
        // This remains implemented so the existing prompt/interaction scanner can discover this zone.
    }

    public string GetPromptVerb(in InteractContext context)
    {
        PlayerBoardingState boarding = FindBoardingState(context);

        if (boarding == null || !CanBoard(boarding))
            return promptText;

        if (!includeHoldKeyInPrompt)
            return promptText;

        string keyText = GetKeyPromptText();

        if (!includeProgressInPrompt)
            return $"Hold {keyText} - {promptText}";

        float progress = Mathf.Clamp01(GetHoldTimer(boarding) / Mathf.Max(0.01f, holdSeconds));
        return $"Hold {keyText} - {promptText} ({Mathf.RoundToInt(progress * 100f)}%)";
    }

    public Transform GetPromptAnchor()
    {
        if (boardPoint != null)
            return boardPoint;

        return transform;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerBoardingState boarding = other.GetComponentInParent<PlayerBoardingState>();
        if (boarding == null)
            return;

        if (!_overlapCounts.TryGetValue(boarding, out int count))
        {
            _overlapCounts[boarding] = 1;
            _holdTimers[boarding] = 0f;
        }
        else
        {
            _overlapCounts[boarding] = count + 1;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerBoardingState boarding = other.GetComponentInParent<PlayerBoardingState>();
        if (boarding == null)
            return;

        if (!_overlapCounts.TryGetValue(boarding, out int count))
            return;

        count--;

        if (count <= 0)
        {
            _overlapCounts.Remove(boarding);
            _holdTimers.Remove(boarding);
        }
        else
        {
            _overlapCounts[boarding] = count;
        }
    }

    private bool TryBoard(PlayerBoardingState boarding)
    {
        if (boarding == null)
            return false;

        if (!CanBoard(boarding))
            return false;

        Transform boatRoot = ResolveBoatRoot();
        if (boatRoot == null)
            return false;

        if (snapToBoardPoint && boardPoint != null)
            SnapPlayerToBoardPoint(boarding);

        boarding.Board(boatRoot);

        if (debugLog)
        {
            Debug.Log(
                $"[DeckBoardZone:{name}] Boarded '{boarding.name}' onto boat root '{boatRoot.name}'.",
                this);
        }

        return true;
    }

    private bool CanBoard(PlayerBoardingState boarding)
    {
        if (boarding == null)
            return false;

        Transform boatRoot = ResolveBoatRoot();
        if (boatRoot == null)
            return false;

        if (requireUnboarded && boarding.IsBoarded)
            return false;

        // Already on this boat. Do nothing.
        if (boarding.IsBoarded && boarding.CurrentBoatRoot == boatRoot)
            return false;

        return true;
    }

    private void SnapPlayerToBoardPoint(PlayerBoardingState boarding)
    {
        if (boarding == null || boardPoint == null)
            return;

        Vector2 target = boardPoint.position;

        Rigidbody2D rb = boarding.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            if (zeroVelocityOnBoard)
                rb.linearVelocity = Vector2.zero;

            rb.position = target;
            return;
        }

        Transform t = boarding.transform;
        Vector3 pos = t.position;
        pos.x = target.x;
        pos.y = target.y;
        t.position = pos;
    }

    private PlayerBoardingState FindClosestEligiblePlayer()
    {
        PlayerBoardingState best = null;
        float bestDistSq = float.PositiveInfinity;

        Vector2 reference = boardPoint != null ? (Vector2)boardPoint.position : (Vector2)transform.position;

        foreach (var pair in _overlapCounts)
        {
            PlayerBoardingState boarding = pair.Key;

            if (!CanBoard(boarding))
                continue;

            float distSq = ((Vector2)boarding.transform.position - reference).sqrMagnitude;

            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = boarding;
            }
        }

        return best;
    }

    private bool IsBoardHoldPressed()
    {
        if (primaryHoldKey != KeyCode.None && Input.GetKey(primaryHoldKey))
            return true;

        if (allowSecondaryHoldKey &&
            secondaryHoldKey != KeyCode.None &&
            Input.GetKey(secondaryHoldKey))
        {
            return true;
        }

        return false;
    }

    private bool IsInsideZone(PlayerBoardingState boarding)
    {
        return boarding != null &&
               _overlapCounts.TryGetValue(boarding, out int count) &&
               count > 0;
    }

    private float GetHoldTimer(PlayerBoardingState boarding)
    {
        if (boarding == null)
            return 0f;

        return _holdTimers.TryGetValue(boarding, out float timer) ? timer : 0f;
    }

    private string GetKeyPromptText()
    {
        if (allowSecondaryHoldKey && secondaryHoldKey != KeyCode.None)
            return $"{primaryHoldKey}/{secondaryHoldKey}";

        return primaryHoldKey.ToString();
    }

    private PlayerBoardingState FindBoardingState(in InteractContext context)
    {
        if (context.InteractorGO != null)
        {
            PlayerBoardingState fromGO =
                context.InteractorGO.GetComponentInParent<PlayerBoardingState>();

            if (fromGO != null)
                return fromGO;

            fromGO =
                context.InteractorGO.GetComponentInChildren<PlayerBoardingState>(true);

            if (fromGO != null)
                return fromGO;
        }

        if (context.InteractorTransform != null)
        {
            PlayerBoardingState fromTransform =
                context.InteractorTransform.GetComponentInParent<PlayerBoardingState>();

            if (fromTransform != null)
                return fromTransform;

            fromTransform =
                context.InteractorTransform.GetComponentInChildren<PlayerBoardingState>(true);

            if (fromTransform != null)
                return fromTransform;
        }

        return null;
    }

    private Transform ResolveBoatRoot()
    {
        if (boatRootOverride != null)
            return boatRootOverride;

        CacheBoat();

        if (_cachedBoat != null)
            return _cachedBoat.transform;

        if (!_missingBoatLogged)
        {
            _missingBoatLogged = true;

            Debug.LogWarning(
                $"{name}: DeckBoardZone could not resolve a Boat parent. " +
                $"Assign Boat Root Override or place this zone under a Boat.",
                this);
        }

        return null;
    }

    private void CacheBoat()
    {
        if (_cachedBoat == null)
            _cachedBoat = GetComponentInParent<Boat>();
    }

    private void CleanupNullPlayers()
    {
        _scratchPlayers.Clear();

        foreach (var pair in _overlapCounts)
        {
            if (pair.Key == null)
                _scratchPlayers.Add(pair.Key);
        }

        for (int i = 0; i < _scratchPlayers.Count; i++)
        {
            _overlapCounts.Remove(_scratchPlayers[i]);
            _holdTimers.Remove(_scratchPlayers[i]);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;

        Transform anchor = boardPoint != null ? boardPoint : transform;
        Gizmos.DrawSphere(anchor.position, 0.08f);

        if (boardPoint != null)
            Gizmos.DrawLine(transform.position, boardPoint.position);
    }
#endif
}