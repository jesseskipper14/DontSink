using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class BoatBoardingInteractable :
    MonoBehaviour,
    IInteractable,
    IInteractPromptProvider,
    IInteractPromptActionProvider,
    IInteractionLabelProvider,
    IHoldInteractable
{
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
    [SerializeField, Min(0.05f)]
    private float boardHoldSeconds = 0.35f;

    [SerializeField] private bool showBoardHoldProgressInPrompt = true;

    [Tooltip("If true, pressing interact while unboarded does not instantly board. Holding handles boarding.")]
    [SerializeField] private bool suppressInstantBoardInteract = true;

    [Header("Unboard Hold")]
    [SerializeField] private bool requireHoldToUnboard = true;
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

    public int InteractionPriority => priority;

    public float GetInteractionHoldDuration(
        in InteractContext context)
    {
        PlayerBoardingState boarding =
            FindBoardingState(
                context);

        if (boarding == null)
            return 0f;

        if (boarding.IsBoarded)
        {
            if (boarding.CurrentBoatRoot != boatRoot)
                return 0f;

            return
                requireHoldToUnboard &&
                suppressInstantUnboardInteract
                    ? Mathf.Max(
                        0.05f,
                        unboardHoldSeconds)
                    : 0f;
        }

        return
            requireHoldToBoard &&
            suppressInstantBoardInteract
                ? Mathf.Max(
                    0.05f,
                    boardHoldSeconds)
                : 0f;
    }

    public string GetPromptVerb(in InteractContext context)
    {
        PlayerBoardingState boarding = FindBoardingState(context);

        if (boarding != null &&
            boarding.IsBoarded &&
            boarding.CurrentBoatRoot == boatRoot)
        {
            return "Leave Boat";
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

    public bool CanInteract(in InteractContext context)
    {
        if (context.InteractorGO == null)
            return false;

        float dist =
            Vector2.Distance(
                context.Origin,
                transform.position);

        if (dist > maxUseDistance)
            return false;

        PlayerBoardingState boarding =
            FindBoardingState(context);

        if (boarding == null)
            return true;

        Vector2 target = PhysicsFrame2D.Point(boarding.IsBoarded ? unboardPoint : boardPoint);
        Rigidbody2D body = boarding.GetComponent<Rigidbody2D>();
        Vector2 start = body != null ? body.position : (Vector2)boarding.transform.position;
        if (boatRoot != null && BoatRailTraversalBlocker.IsCrossingBlocked(boatRoot, start, target,
            BoatRailTraversalBlocker.ActorHalfSize(boarding, boatRoot))) return false;

        if (boarding.IsBoarded)
            return boarding.CurrentBoatRoot == boatRoot;

        return true;
    }

    public void Interact(in InteractContext context)
    {
        GameObject go = context.InteractorGO;
        Transform t = context.InteractorTransform;

        if (go == null || t == null)
            return;

        PlayerBoardingState boarding =
            FindBoardingState(context);

        if (boarding == null)
        {
            Debug.LogWarning(
                $"'{go.name}' missing PlayerBoardingState.",
                this);
            return;
        }

        if (!CanInteract(context))
            return;

        if (!boarding.IsBoarded)
        {
            Board(
                go,
                t,
                boarding);

            return;
        }

        if (boarding.CurrentBoatRoot != boatRoot)
            return;

        Unboard(
            go,
            t,
            boarding);
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

        if (debugHold)
            Debug.Log($"[BoatBoardingInteractable:{name}] Boarded '{go.name}'.", this);
    }

    private void Unboard(GameObject go, Transform t, PlayerBoardingState boarding)
    {
        t.SetParent(null, worldPositionStays: true);
        SnapTo(t, unboardPoint);

        ZeroVelocity(go);
        boarding.Unboard();

        if (debugHold)
            Debug.Log($"[BoatBoardingInteractable:{name}] Unboarded '{go.name}'.", this);
    }

    private void LateUpdate()
    {
        if (!fadeWhenPlayerFar)
            return;

        PlayerBoardingState player = ResolvePresentationPlayer();

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

    private PlayerBoardingState ResolvePresentationPlayer()
    {
        if (CameraManager.Instance != null)
        {
            PlayerBoardingState viewed =
                CameraManager.Instance.ViewingPlayer;

            if (viewed != null)
                return viewed;
        }

        return null;
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

    private float GetGenericHoldProgress(
        in InteractContext context)
    {
        Interactor2D interactor =
            ResolveInteractor(
                context);

        if (interactor == null ||
            !ReferenceEquals(
                interactor.ActiveHoldInteractTarget,
                this))
        {
            return 0f;
        }

        return
            interactor.ActiveHoldInteractProgress;
    }

    private static Interactor2D ResolveInteractor(
        in InteractContext context)
    {
        if (context.InteractorGO != null)
        {
            Interactor2D direct =
                context.InteractorGO.GetComponent<Interactor2D>();

            if (direct != null)
                return direct;

            Interactor2D parent =
                context.InteractorGO.GetComponentInParent<Interactor2D>();

            if (parent != null)
                return parent;

            Interactor2D child =
                context.InteractorGO.GetComponentInChildren<Interactor2D>(
                    true);

            if (child != null)
                return child;
        }

        if (context.InteractorTransform != null)
        {
            Interactor2D parent =
                context.InteractorTransform.GetComponentInParent<Interactor2D>();

            if (parent != null)
                return parent;

            return
                context.InteractorTransform.GetComponentInChildren<Interactor2D>(
                    true);
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

    public string GetInteractionLabel(in InteractContext context)
    {
        return "Boarding Door";
    }

    public void GetPromptActions(
        in InteractContext context,
        System.Collections.Generic.List<PromptAction> actions)
    {
        PlayerBoardingState boarding =
            FindBoardingState(context);

        if (boarding == null ||
            !CanInteract(context))
        {
            return;
        }

        if (boarding.IsBoarded &&
            boarding.CurrentBoatRoot == boatRoot)
        {
            if (!requireHoldToUnboard)
            {
                actions.Add(
                    new PromptAction(
                        "Press E to Leave Boat",
                        priority: 100));
                return;
            }

            float progress =
                GetGenericHoldProgress(
                    context);

            actions.Add(
                new PromptAction(
                    "Hold E to Leave Boat",
                    priority: 100,
                    showProgress:
                        showUnboardHoldProgressInPrompt,
                    progress01:
                        showUnboardHoldProgressInPrompt
                            ? progress
                            : 0f));

            return;
        }

        if (!boarding.IsBoarded)
        {
            if (!requireHoldToBoard)
            {
                actions.Add(
                    new PromptAction(
                        "Press E to Board",
                        priority: 100));
                return;
            }

            float progress =
                GetGenericHoldProgress(
                    context);

            actions.Add(
                new PromptAction(
                    "Hold E to Board",
                    priority: 100,
                    showProgress:
                        showBoardHoldProgressInPrompt,
                    progress01:
                        showBoardHoldProgressInPrompt
                            ? progress
                            : 0f));
        }
    }
}
