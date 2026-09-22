using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class DeckBoardZone :
    MonoBehaviour,
    IInteractable,
    IInteractPromptProvider,
    IInteractPromptActionProvider,
    IInteractionPromptDisplayPolicyProvider
{
    private enum ZoneAction
    {
        None = 0,
        Board = 1,
        Unboard = 2
    }

    [Header("Interaction")]
    [SerializeField] private int priority = 60;

    [Tooltip("How long the player must hold the board/unboard intent while inside the zone.")]
    [SerializeField, Min(0f)] private float holdSeconds = 0.35f;

    [Tooltip(
        "If true, ClimbUpHeld may also board the player. " +
        "The primary board action always uses InteractionIntent.InteractHeld.")]
    [SerializeField] private bool allowSecondaryHoldKey = false;

    [Header("Board Target")]
    [Tooltip("Optional explicit boat root. If unset, resolves from parent Boat.")]
    [SerializeField] private Transform boatRootOverride;

    [Tooltip("Optional point to place the player when boarding succeeds. Recommended.")]
    [SerializeField] private Transform boardPoint;

    [SerializeField] private bool snapToBoardPoint = true;
    [SerializeField] private bool zeroVelocityOnBoard = true;

    [Header("Rules")]
    [Tooltip(
        "If true, only currently-unboarded players may BOARD through this zone. " +
        "A player already boarded to this boat may still UNBOARD here.")]
    [SerializeField] private bool requireUnboarded = true;

    [Tooltip("If true, only the closest eligible player in the zone progresses a hold timer.")]
    [SerializeField] private bool onlyBoardClosestEligiblePlayer = true;

    [Header("Prompt")]
    [SerializeField] private string promptText = "Board Deck";
    [SerializeField] private string unboardPromptText = "Leave Deck";
    [SerializeField] private bool includeHoldKeyInPrompt = true;
    [SerializeField] private bool includeProgressInPrompt = false;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    public int InteractionPriority => priority;

    private readonly Dictionary<PlayerBoardingState, int> _overlapCounts = new();
    private readonly Dictionary<PlayerBoardingState, float> _holdTimers = new();
    private readonly Dictionary<PlayerBoardingState, ZoneAction> _holdActions = new();
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
        _holdActions.Clear();
        _scratchPlayers.Clear();
    }

    private void Update()
    {
        if (_overlapCounts.Count == 0)
            return;

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

            ZoneAction action = GetAvailableAction(boarding);

            if (action == ZoneAction.None)
            {
                ResetHold(boarding);
                continue;
            }

            if (onlyBoardClosestEligiblePlayer &&
                boarding != closestEligible)
            {
                ResetHold(boarding);
                continue;
            }

            ZoneAction previousAction =
                _holdActions.TryGetValue(
                    boarding,
                    out ZoneAction storedAction)
                    ? storedAction
                    : ZoneAction.None;

            if (previousAction != action)
            {
                _holdTimers[boarding] = 0f;
                _holdActions[boarding] = action;
            }

            if (!IsHoldIntentActive(
                    boarding,
                    action))
            {
                _holdTimers[boarding] = 0f;
                continue;
            }

            float timer =
                GetHoldTimer(boarding) +
                Time.deltaTime;

            _holdTimers[boarding] = timer;

            if (timer < holdSeconds)
                continue;

            bool succeeded =
                action == ZoneAction.Board
                    ? TryBoard(boarding)
                    : TryUnboard(boarding);

            _holdTimers[boarding] = 0f;

            if (succeeded)
                _holdActions[boarding] = ZoneAction.None;
        }

        CleanupNullPlayers();
    }

    public bool CanInteract(in InteractContext context)
    {
        PlayerBoardingState boarding =
            FindBoardingState(context);

        if (boarding == null)
            return false;

        if (!IsInsideZone(boarding))
            return false;

        return
            GetAvailableAction(boarding) !=
            ZoneAction.None;
    }

    public void Interact(in InteractContext context)
    {
        // Boarding/unboarding is intentionally hold-driven in Update().
        // This remains implemented so the existing prompt/interaction scanner
        // can discover the zone.
    }

    public string GetPromptVerb(in InteractContext context)
    {
        PlayerBoardingState boarding =
            FindBoardingState(context);

        ZoneAction action =
            boarding != null
                ? GetAvailableAction(boarding)
                : ZoneAction.None;

        string verb =
            action == ZoneAction.Unboard
                ? unboardPromptText
                : promptText;

        if (boarding == null ||
            action == ZoneAction.None ||
            !includeHoldKeyInPrompt)
        {
            return verb;
        }

        string inputText =
            GetInputPromptText(
                boarding,
                action);

        if (!includeProgressInPrompt)
            return $"Hold {inputText} - {verb}";

        float progress =
            Mathf.Clamp01(
                GetHoldTimer(boarding) /
                Mathf.Max(
                    0.01f,
                    holdSeconds));

        return
            $"Hold {inputText} - {verb} " +
            $"({Mathf.RoundToInt(progress * 100f)}%)";
    }

    public Transform GetPromptAnchor()
    {
        if (boardPoint != null)
            return boardPoint;

        return transform;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerBoardingState boarding =
            other.GetComponentInParent<PlayerBoardingState>();

        if (boarding == null)
            return;

        if (!_overlapCounts.TryGetValue(
                boarding,
                out int count))
        {
            _overlapCounts[boarding] = 1;
            _holdTimers[boarding] = 0f;
            _holdActions[boarding] = ZoneAction.None;
        }
        else
        {
            _overlapCounts[boarding] =
                count + 1;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerBoardingState boarding =
            other.GetComponentInParent<PlayerBoardingState>();

        if (boarding == null)
            return;

        if (!_overlapCounts.TryGetValue(
                boarding,
                out int count))
        {
            return;
        }

        count--;

        if (count <= 0)
        {
            _overlapCounts.Remove(boarding);
            _holdTimers.Remove(boarding);
            _holdActions.Remove(boarding);
        }
        else
        {
            _overlapCounts[boarding] = count;
        }
    }

    /// <summary>
    /// Authority-side board application seam.
    ///
    /// Future networking should authenticate the requesting player and invoke this
    /// method on authority. The client must not be trusted to choose an arbitrary
    /// PlayerBoardingState.
    /// </summary>
    public bool TryBoard(
        PlayerBoardingState boarding)
    {
        if (boarding == null)
            return false;

        if (!GameplayAuthority.CanRun(
                GameplayAuthorityMode.SinglePlayerOrAuthoritative))
        {
            return false;
        }

        if (!CanBoard(boarding))
            return false;

        Transform boatRoot =
            ResolveBoatRoot();

        if (boatRoot == null)
            return false;

        // BOARDING TRANSACTION:
        //
        // 1) Apply authoritative boarded physics/state without resolving boat
        //    visibility zones at the player's pre-snap position.
        // 2) Move to the authored deck point.
        // 3) Sync transforms.
        // 4) Wait for the next 2D physics update before asking visibility zones
        //    which presentation actually contains the player.
        //
        // Collider2D.IsTouching() reports the LAST physics-system contact state,
        // so an immediate post-teleport zone scan can still see the player's old
        // exterior/interior overlap for one frame.
        boarding.BoardDeferredPresentation(
            boatRoot);

        if (snapToBoardPoint &&
            boardPoint != null)
        {
            SnapPlayerToBoardPoint(
                boarding);
        }

        Physics2D.SyncTransforms();

        StartCoroutine(
            RefreshBoardPresentationAfterPhysics(
                boarding,
                boatRoot));

        if (debugLog)
        {
            Debug.Log(
                $"[DeckBoardZone:{name}] Boarded '{boarding.name}' onto boat root '{boatRoot.name}'.",
                this);
        }

        return true;
    }

    private IEnumerator RefreshBoardPresentationAfterPhysics(
        PlayerBoardingState boarding,
        Transform expectedBoatRoot)
    {
        // Wait until Unity has completed a 2D physics step at the snapped pose.
        // Keeping the previous UnboardedExterior boat presentation during this
        // tiny transaction is intentional: it is visually safe, whereas resolving
        // the stale pre-snap Interior contact is not.
        yield return new WaitForFixedUpdate();

        if (boarding == null ||
            !boarding.IsBoarded ||
            boarding.CurrentBoatRoot != expectedBoatRoot)
        {
            yield break;
        }

        boarding.RefreshCurrentBoatVisualState();
    }

    /// <summary>
    /// Authority-side unboard application seam.
    ///
    /// Current local input reaches this through the player's intent sources.
    /// A future network transport can route the same semantic request here after
    /// authenticating the requester.
    /// </summary>
    public bool TryUnboard(
        PlayerBoardingState boarding)
    {
        if (boarding == null)
            return false;

        if (!GameplayAuthority.CanRun(
                GameplayAuthorityMode.SinglePlayerOrAuthoritative))
        {
            return false;
        }

        if (!CanUnboard(boarding))
            return false;

        Transform boatRoot =
            ResolveBoatRoot();

        boarding.Unboard();

        // DeckBoardZone itself does not normally parent the player, but older/
        // alternate boarding paths may. Do not leave a logically-unboarded player
        // transform-parented to the boat.
        if (boatRoot != null &&
            boarding.transform.IsChildOf(boatRoot))
        {
            boarding.transform.SetParent(
                null,
                worldPositionStays: true);
        }

        Physics2D.SyncTransforms();

        if (debugLog)
        {
            Debug.Log(
                $"[DeckBoardZone:{name}] Unboarded '{boarding.name}' from boat root " +
                $"'{(boatRoot != null ? boatRoot.name : "<missing>")}'.",
                this);
        }

        return true;
    }

    private ZoneAction GetAvailableAction(
        PlayerBoardingState boarding)
    {
        if (boarding == null)
            return ZoneAction.None;

        if (CanUnboard(boarding))
            return ZoneAction.Unboard;

        if (CanBoard(boarding))
            return ZoneAction.Board;

        return ZoneAction.None;
    }

    private bool CanBoard(
        PlayerBoardingState boarding)
    {
        if (boarding == null)
            return false;

        Transform boatRoot =
            ResolveBoatRoot();

        if (boatRoot == null)
            return false;

        if (requireUnboarded &&
            boarding.IsBoarded)
        {
            return false;
        }

        // Already aboard this boat is an UNBOARD action, never another board.
        if (boarding.IsBoarded &&
            boarding.CurrentBoatRoot == boatRoot)
        {
            return false;
        }

        return true;
    }

    private bool CanUnboard(
        PlayerBoardingState boarding)
    {
        if (boarding == null ||
            !boarding.IsBoarded)
        {
            return false;
        }

        Transform boatRoot =
            ResolveBoatRoot();

        return
            boatRoot != null &&
            boarding.CurrentBoatRoot == boatRoot;
    }

    private void SnapPlayerToBoardPoint(
        PlayerBoardingState boarding)
    {
        if (boarding == null ||
            boardPoint == null)
        {
            return;
        }

        Vector2 target =
            boardPoint.position;

        Rigidbody2D rb =
            boarding.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            if (zeroVelocityOnBoard)
            {
                rb.linearVelocity =
                    Vector2.zero;

                rb.angularVelocity =
                    0f;
            }

            rb.position =
                target;

            return;
        }

        Transform t =
            boarding.transform;

        Vector3 pos =
            t.position;

        pos.x = target.x;
        pos.y = target.y;

        t.position =
            pos;
    }

    private PlayerBoardingState FindClosestEligiblePlayer()
    {
        PlayerBoardingState best = null;
        float bestDistSq =
            float.PositiveInfinity;

        Vector2 reference =
            boardPoint != null
                ? (Vector2)boardPoint.position
                : (Vector2)transform.position;

        foreach (var pair in _overlapCounts)
        {
            PlayerBoardingState boarding =
                pair.Key;

            if (GetAvailableAction(boarding) ==
                ZoneAction.None)
            {
                continue;
            }

            float distSq =
                ((Vector2)boarding.transform.position -
                 reference).sqrMagnitude;

            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = boarding;
            }
        }

        return best;
    }

    private bool IsHoldIntentActive(
        PlayerBoardingState boarding,
        ZoneAction action)
    {
        if (boarding == null)
            return false;

        if (action == ZoneAction.Board)
        {
            IInteractionIntentSource interaction =
                ResolveInteractionIntentSource(
                    boarding);

            if (interaction != null &&
                interaction.Current.InteractHeld)
            {
                return true;
            }

            if (allowSecondaryHoldKey)
            {
                ICharacterIntentSource character =
                    ResolveCharacterIntentSource(
                        boarding);

                if (character != null &&
                    character.Current.ClimbUpHeld)
                {
                    return true;
                }
            }

            return false;
        }

        if (action == ZoneAction.Unboard)
        {
            ICharacterIntentSource character =
                ResolveCharacterIntentSource(
                    boarding);

            return
                character != null &&
                character.Current.ClimbDownHeld;
        }

        return false;
    }

    private IInteractionIntentSource ResolveInteractionIntentSource(
        PlayerBoardingState boarding)
    {
        if (boarding == null)
            return null;

        MonoBehaviour[] direct =
            boarding.GetComponents<MonoBehaviour>();

        for (int i = 0;
             direct != null && i < direct.Length;
             i++)
        {
            if (direct[i] is IInteractionIntentSource source)
                return source;
        }

        MonoBehaviour[] children =
            boarding.GetComponentsInChildren<MonoBehaviour>(
                true);

        for (int i = 0;
             children != null && i < children.Length;
             i++)
        {
            if (children[i] is IInteractionIntentSource source)
                return source;
        }

        return null;
    }

    private ICharacterIntentSource ResolveCharacterIntentSource(
        PlayerBoardingState boarding)
    {
        if (boarding == null)
            return null;

        MonoBehaviour[] direct =
            boarding.GetComponents<MonoBehaviour>();

        for (int i = 0;
             direct != null && i < direct.Length;
             i++)
        {
            if (direct[i] is ICharacterIntentSource source)
                return source;
        }

        MonoBehaviour[] children =
            boarding.GetComponentsInChildren<MonoBehaviour>(
                true);

        for (int i = 0;
             children != null && i < children.Length;
             i++)
        {
            if (children[i] is ICharacterIntentSource source)
                return source;
        }

        return null;
    }

    private bool IsInsideZone(
        PlayerBoardingState boarding)
    {
        return
            boarding != null &&
            _overlapCounts.TryGetValue(
                boarding,
                out int count) &&
            count > 0;
    }

    private float GetHoldTimer(
        PlayerBoardingState boarding)
    {
        if (boarding == null)
            return 0f;

        return
            _holdTimers.TryGetValue(
                boarding,
                out float timer)
                ? timer
                : 0f;
    }

    private void ResetHold(
        PlayerBoardingState boarding)
    {
        if (boarding == null)
            return;

        _holdTimers[boarding] = 0f;
        _holdActions[boarding] = ZoneAction.None;
    }

    private string GetInputPromptText(
        PlayerBoardingState boarding,
        ZoneAction action)
    {
        if (action == ZoneAction.Unboard)
        {
            ICharacterIntentSource character =
                ResolveCharacterIntentSource(
                    boarding);

            if (character is LocalCharacterIntentSource localCharacter)
                return localCharacter.ClimbDownBindingLabel;

            return "Down";
        }

        IInteractionIntentSource interaction =
            ResolveInteractionIntentSource(
                boarding);

        string primary =
            interaction is LocalInteractionIntentSource localInteraction
                ? localInteraction.InteractBindingLabel
                : "Interact";

        if (!allowSecondaryHoldKey)
            return primary;

        ICharacterIntentSource secondary =
            ResolveCharacterIntentSource(
                boarding);

        string secondaryText =
            secondary is LocalCharacterIntentSource localCharacterSource
                ? localCharacterSource.ClimbUpBindingLabel
                : "Up";

        return
            $"{primary}/{secondaryText}";
    }

    private PlayerBoardingState FindBoardingState(
        in InteractContext context)
    {
        if (context.InteractorGO != null)
        {
            PlayerBoardingState fromGO =
                context.InteractorGO
                    .GetComponentInParent<PlayerBoardingState>();

            if (fromGO != null)
                return fromGO;

            fromGO =
                context.InteractorGO
                    .GetComponentInChildren<PlayerBoardingState>(
                        true);

            if (fromGO != null)
                return fromGO;
        }

        if (context.InteractorTransform != null)
        {
            PlayerBoardingState fromTransform =
                context.InteractorTransform
                    .GetComponentInParent<PlayerBoardingState>();

            if (fromTransform != null)
                return fromTransform;

            fromTransform =
                context.InteractorTransform
                    .GetComponentInChildren<PlayerBoardingState>(
                        true);

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

    public bool ShouldShowHoverLabel(
        in InteractContext context)
    {
        return CanInteract(context);
    }

    public void GetPromptActions(
        in InteractContext context,
        List<PromptAction> actions)
    {
        if (!CanInteract(context))
            return;

        PlayerBoardingState boarding =
            FindBoardingState(context);

        if (boarding == null)
            return;

        ZoneAction action =
            GetAvailableAction(boarding);

        if (action == ZoneAction.None)
            return;

        string inputText =
            GetInputPromptText(
                boarding,
                action);

        string verb =
            action == ZoneAction.Unboard
                ? unboardPromptText
                : promptText;

        float progress =
            Mathf.Clamp01(
                GetHoldTimer(boarding) /
                Mathf.Max(
                    0.01f,
                    holdSeconds));

        actions.Add(
            new PromptAction(
                $"Hold {inputText} to {verb}",
                priority: 100,
                showProgress: includeProgressInPrompt,
                progress01: progress));
    }

    private void CacheBoat()
    {
        if (_cachedBoat == null)
            _cachedBoat =
                GetComponentInParent<Boat>();
    }

    private void CleanupNullPlayers()
    {
        _scratchPlayers.Clear();

        foreach (var pair in _overlapCounts)
        {
            if (pair.Key == null)
                _scratchPlayers.Add(pair.Key);
        }

        for (int i = 0;
             i < _scratchPlayers.Count;
             i++)
        {
            PlayerBoardingState key =
                _scratchPlayers[i];

            _overlapCounts.Remove(key);
            _holdTimers.Remove(key);
            _holdActions.Remove(key);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color =
            Color.green;

        Transform anchor =
            boardPoint != null
                ? boardPoint
                : transform;

        Gizmos.DrawSphere(
            anchor.position,
            0.08f);

        if (boardPoint != null)
        {
            Gizmos.DrawLine(
                transform.position,
                boardPoint.position);
        }
    }
#endif
}
