using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class BoatBoardingInteractable : MonoBehaviour, IInteractable, IInteractPromptProvider
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

    private float _currentAlpha = 1f;
    private PlayerBoardingState _cachedPlayer;

    public int InteractionPriority => priority;
    public string GetPromptVerb(in InteractContext context) => "Board";
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
        float dist = Vector2.Distance(context.Origin, transform.position);
        if (dist > maxUseDistance) return false;

        // If boarded, only allow toggling off if they are boarded to THIS boat.
        var boarding = context.InteractorGO.GetComponent<PlayerBoardingState>();
        if (boarding != null && boarding.IsBoarded)
            return boarding.CurrentBoatRoot == boatRoot;

        return true;
    }

    public void Interact(in InteractContext context)
    {
        var go = context.InteractorGO;
        var t = context.InteractorTransform;
        if (go == null || t == null) return;

        var boarding = go.GetComponent<PlayerBoardingState>();
        if (boarding == null)
        {
            Debug.LogWarning($"'{go.name}' missing PlayerBoardingState.");
            return;
        }

        if (!boarding.IsBoarded)
        {
            // BOARD
            SnapTo(t, boardPoint);
            if (parentPlayerToBoat && boatRoot != null)
                t.SetParent(boatRoot, worldPositionStays: true);

            ZeroVelocity(go);
            boarding.Board(boatRoot);
        }
        else
        {
            // UNBOARD
            // (only allowed for same boat via CanInteract)
            t.SetParent(null, worldPositionStays: true);
            SnapTo(t, unboardPoint);

            ZeroVelocity(go);
            boarding.Unboard();
        }
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
        var rb = go.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    private PlayerBoardingState ResolvePlayer()
    {
        if (_cachedPlayer != null)
            return _cachedPlayer;

        _cachedPlayer = FindFirstObjectByType<PlayerBoardingState>();
        return _cachedPlayer;
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
