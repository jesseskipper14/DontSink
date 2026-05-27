using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class BoatBoardingInteractable : MonoBehaviour, IInteractable, IInteractPromptProvider
{
    private enum HoldAction
    {
        None,
        Board,
        Unboard
    }

    [Header("Interaction")]
    [SerializeField] private int priority = 60;
    [SerializeField] private float maxUseDistance = 1.8f;

    [Header("Boarding")]
    [SerializeField] private Transform boatRoot;
    [SerializeField] private Transform boardPoint;
    [SerializeField] private Transform unboardPoint;
    [SerializeField] private bool parentPlayerToBoat = true;

    [SerializeField] private Vector2 postSnapNudge = Vector2.zero;

    [Header("Board Hold")]
    [SerializeField] private bool requireHoldToBoard = true;
    [SerializeField] private KeyCode boardHoldKey = KeyCode.E;

    [SerializeField, Min(0.05f)]
    private float boardHoldSeconds = 0.35f;

    [SerializeField] private bool showBoardHoldProgressInPrompt = true;

    [Tooltip("If true, pressing interact while unboarded does not instantly board. Holding handles boarding.")]
    [SerializeField] private bool suppressInstantBoardInteract = true;

    [Header("Unboard Hold")]
    [SerializeField] private bool requireHoldToUnboard = true;
    [SerializeField] private KeyCode unboardHoldKey = KeyCode.E;

    [SerializeField, Min(0.05f)]
    private float unboardHoldSeconds = 0.65f;

    [SerializeField] private bool showUnboardHoldProgressInPrompt = true;

    [Tooltip("If true, pressing interact while boarded does not instantly unboard. Holding handles unboarding.")]
    [SerializeField] private bool suppressInstantUnboardInteract = true;

    [Header("Visual Fade")]
    [SerializeField] private bool fadeWhenPlayerFar = true;
    [SerializeField] private SpriteRenderer[] fadeRenderers;

    [SerializeField] private bool fadeOnlyWhenBoardedInterior = true;
    [SerializeField] private BoatVisualStateController visualStateController;

    [Tooltip("Distance at or below this counts as close.")]
    [SerializeField] private float fadeNearDistance = 1.8f;

    [Range(0f, 1f)]
    [SerializeField] private float nearAlpha = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float farAlpha = 0.35f;

    [SerializeField] private float fadeSpeed = 12f;

    [Tooltip("If true, uses maxUseDistance as fadeNearDistance.")]
    [SerializeField] private bool useInteractDistanceForFade = true;

    [Header("Debug")]
    [SerializeField] private bool debugHold = false;

    private float _currentAlpha = 1f;
    private PlayerBoardingState _cachedPlayer;

    private PlayerBoardingState _holdPlayer;
    private HoldAction _holdAction;
    private float _holdTimer;

    public int InteractionPriority => priority;

    public string GetPromptVerb(in InteractContext context)
    {
        PlayerBoardingState boarding = FindBoardingState(context);

        if (boarding != null &&
            boarding.IsBoarded &&
            boarding.CurrentBoatRoot == boatRoot)
        {
            if (!requireHoldToUnboard)
                return "Leave Boat";

            if (!showUnboardHoldProgressInPrompt)
                return $"Hold {unboardHoldKey} - Leave Boat";

            float progress = GetPromptProgress(boarding, HoldAction.Unboard, unboardHoldSeconds);
            return $"Hold {unboardHoldKey} - Leave Boat ({Mathf.RoundToInt(progress * 100f)}%)";
        }

        if (boarding != null && !boarding.IsBoarded)
        {
            if (!requireHoldToBoard)
                return "Board";

            if (!showBoardHoldProgressInPrompt)
                return $"Hold {boardHoldKey} - Board";

            float progress = GetPromptProgress(boarding, HoldAction.Board, boardHoldSeconds);
            return $"Hold {boardHoldKey} - Board ({Mathf.RoundToInt(progress * 100f)}%)";
        }

        return "Board";
    }

    public Transform GetPromptAnchor() => boardPoint != null ? boardPoint : transform;

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        if (boatRoot == null) boatRoot = transform.root;
        if (boardPoint == null) boardPoint = transform;
        if (unboardPoint == null) unboardPoint = transform;

        if (fadeRenderers == null || fadeRenderers.Length == 0)
            fadeRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        if (visualStateController == null && boatRoot != null)
        {
            visualStateController =
                boatRoot.GetComponent<BoatVisualStateController>() ??
                boatRoot.GetComponentInChildren<BoatVisualStateController>(true);
        }

        _currentAlpha = nearAlpha;
        ApplyAlpha(_currentAlpha);
    }

    private void Update()
    {
        TickBoardingHold();
    }

    public bool CanInteract(in InteractContext context)
    {
        if (context.InteractorGO == null)
            return false;

        float dist = Vector2.Distance(context.Origin, transform.position);
        if (dist > maxUseDistance)
            return false;

        PlayerBoardingState boarding = FindBoardingState(context);
        if (boarding == null)
            return true;

        if (boarding.IsBoarded)
            return boarding.CurrentBoatRoot == boatRoot;

        return true;
    }

    public void Interact(in InteractContext context)
    {
        GameObject go = context.InteractorGO;
        Transform t = context.InteractorTransform;
        if (go == null || t == null) return;

        PlayerBoardingState boarding = FindBoardingState(context);
        if (boarding == null)
        {
            Debug.LogWarning($"'{go.name}' missing PlayerBoardingState.");
            return;
        }

        if (!boarding.IsBoarded)
        {
            if (requireHoldToBoard && suppressInstantBoardInteract)
            {
                BeginOrContinueHold(boarding, HoldAction.Board);

                if (debugHold)
                {
                    Debug.Log(
                        $"[BoatBoardingInteractable:{name}] Instant board suppressed; hold {boardHoldKey} to board.",
                        this);
                }

                return;
            }

            Board(go, t, boarding);
            return;
        }

        if (boarding.CurrentBoatRoot != boatRoot)
            return;

        if (requireHoldToUnboard && suppressInstantUnboardInteract)
        {
            BeginOrContinueHold(boarding, HoldAction.Unboard);

            if (debugHold)
            {
                Debug.Log(
                    $"[BoatBoardingInteractable:{name}] Instant unboard suppressed; hold {unboardHoldKey} to leave.",
                    this);
            }

            return;
        }

        Unboard(go, t, boarding);
    }

    private void TickBoardingHold()
    {
        PlayerBoardingState player = ResolvePlayer();
        if (player == null)
        {
            ResetHold();
            return;
        }

        HoldAction action = GetAvailableHoldAction(player);
        if (action == HoldAction.None)
        {
            ResetHold();
            return;
        }

        KeyCode key = GetHoldKey(action);
        if (key == KeyCode.None || !Input.GetKey(key))
        {
            ResetHold();
            return;
        }

        BeginOrContinueHold(player, action);

        _holdTimer += Time.deltaTime;

        float requiredSeconds = GetHoldSeconds(action);
        if (_holdTimer < requiredSeconds)
            return;

        GameObject go = player.gameObject;
        Transform t = player.transform;

        ResetHold();

        if (action == HoldAction.Board)
            Board(go, t, player);
        else if (action == HoldAction.Unboard)
            Unboard(go, t, player);
    }

    private HoldAction GetAvailableHoldAction(PlayerBoardingState player)
    {
        if (player == null)
            return HoldAction.None;

        if (!IsPlayerInRange(player))
            return HoldAction.None;

        if (player.IsBoarded)
        {
            if (!requireHoldToUnboard)
                return HoldAction.None;

            if (boatRoot == null || player.CurrentBoatRoot != boatRoot)
                return HoldAction.None;

            return HoldAction.Unboard;
        }

        if (!requireHoldToBoard)
            return HoldAction.None;

        return HoldAction.Board;
    }

    private void BeginOrContinueHold(PlayerBoardingState player, HoldAction action)
    {
        if (_holdPlayer != player || _holdAction != action)
        {
            _holdPlayer = player;
            _holdAction = action;
            _holdTimer = 0f;
        }
    }

    private void ResetHold()
    {
        _holdPlayer = null;
        _holdAction = HoldAction.None;
        _holdTimer = 0f;
    }

    private float GetPromptProgress(PlayerBoardingState player, HoldAction action, float requiredSeconds)
    {
        if (_holdPlayer != player || _holdAction != action)
            return 0f;

        return Mathf.Clamp01(_holdTimer / Mathf.Max(0.05f, requiredSeconds));
    }

    private KeyCode GetHoldKey(HoldAction action)
    {
        return action switch
        {
            HoldAction.Board => boardHoldKey,
            HoldAction.Unboard => unboardHoldKey,
            _ => KeyCode.None
        };
    }

    private float GetHoldSeconds(HoldAction action)
    {
        return action switch
        {
            HoldAction.Board => Mathf.Max(0.05f, boardHoldSeconds),
            HoldAction.Unboard => Mathf.Max(0.05f, unboardHoldSeconds),
            _ => 0f
        };
    }

    private bool IsPlayerInRange(PlayerBoardingState player)
    {
        if (player == null)
            return false;

        float dist = Vector2.Distance(player.transform.position, transform.position);
        return dist <= maxUseDistance;
    }

    private void Board(GameObject go, Transform t, PlayerBoardingState boarding)
    {
        SnapTo(t, boardPoint);

        if (parentPlayerToBoat && boatRoot != null)
            t.SetParent(boatRoot, worldPositionStays: true);

        ZeroVelocity(go);
        boarding.Board(boatRoot);

        ResetHold();

        if (debugHold)
            Debug.Log($"[BoatBoardingInteractable:{name}] Boarded '{go.name}'.", this);
    }

    private void Unboard(GameObject go, Transform t, PlayerBoardingState boarding)
    {
        t.SetParent(null, worldPositionStays: true);
        SnapTo(t, unboardPoint);

        ZeroVelocity(go);
        boarding.Unboard();

        ResetHold();

        if (debugHold)
            Debug.Log($"[BoatBoardingInteractable:{name}] Unboarded '{go.name}'.", this);
    }

    private void LateUpdate()
    {
        if (!fadeWhenPlayerFar)
            return;

        PlayerBoardingState player = ResolvePlayer();

        float targetAlpha = nearAlpha;

        if (player != null && ShouldApplyInteriorFade(player))
        {
            float nearDist = useInteractDistanceForFade
                ? maxUseDistance
                : fadeNearDistance;

            float dist = Vector2.Distance(player.transform.position, transform.position);
            bool close = dist <= nearDist;

            targetAlpha = close ? nearAlpha : farAlpha;
        }

        float t = fadeSpeed <= 0f
            ? 1f
            : 1f - Mathf.Exp(-fadeSpeed * Time.deltaTime);

        _currentAlpha = Mathf.Lerp(_currentAlpha, targetAlpha, t);
        ApplyAlpha(_currentAlpha);
    }

    private bool ShouldApplyInteriorFade(PlayerBoardingState player)
    {
        if (!fadeOnlyWhenBoardedInterior)
            return true;

        if (player == null || !player.IsBoarded)
            return false;

        if (boatRoot == null || player.CurrentBoatRoot != boatRoot)
            return false;

        if (visualStateController == null && boatRoot != null)
        {
            visualStateController =
                boatRoot.GetComponent<BoatVisualStateController>() ??
                boatRoot.GetComponentInChildren<BoatVisualStateController>(true);
        }

        if (visualStateController == null)
            return false;

        return visualStateController.CurrentMode == BoatVisibilityMode.BoardedInterior;
    }

    private void SnapTo(Transform player, Transform point)
    {
        Vector3 p = point != null ? point.position : transform.position;
        player.position = p + (Vector3)postSnapNudge;
    }

    private static void ZeroVelocity(GameObject go)
    {
        Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    private PlayerBoardingState ResolvePlayer()
    {
        if (_cachedPlayer != null)
            return _cachedPlayer;

        _cachedPlayer = FindFirstObjectByType<PlayerBoardingState>();
        return _cachedPlayer;
    }

    private PlayerBoardingState FindBoardingState(in InteractContext context)
    {
        if (context.InteractorGO != null)
        {
            PlayerBoardingState direct = context.InteractorGO.GetComponent<PlayerBoardingState>();
            if (direct != null)
                return direct;

            PlayerBoardingState parent = context.InteractorGO.GetComponentInParent<PlayerBoardingState>();
            if (parent != null)
                return parent;

            PlayerBoardingState child = context.InteractorGO.GetComponentInChildren<PlayerBoardingState>(true);
            if (child != null)
                return child;
        }

        if (context.InteractorTransform != null)
        {
            PlayerBoardingState parent = context.InteractorTransform.GetComponentInParent<PlayerBoardingState>();
            if (parent != null)
                return parent;

            PlayerBoardingState child = context.InteractorTransform.GetComponentInChildren<PlayerBoardingState>(true);
            if (child != null)
                return child;
        }

        return null;
    }

    private void ApplyAlpha(float alpha)
    {
        if (fadeRenderers == null)
            return;

        alpha = Mathf.Clamp01(alpha);

        for (int i = 0; i < fadeRenderers.Length; i++)
        {
            SpriteRenderer sr = fadeRenderers[i];
            if (sr == null)
                continue;

            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }
}