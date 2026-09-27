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

    [Tooltip("How long the player must hold the BOARD intent while inside the zone.")]
    [SerializeField, Min(0f)] private float holdSeconds = 0.35f;

    [Tooltip(
        "How long Down must be held before a NEW Jump press confirms intentional unboarding. " +
        "Down alone never unboards.")]
    [SerializeField, Min(0f)] private float unboardArmSeconds = 0.18f;

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

    [Header("Board Safety")]
    [Tooltip(
        "Physics layer used by the boat shell. A deck-board snap is rejected if " +
        "the direct path to the authored board point crosses this boat's hull.")]
    [SerializeField] private string blockingHullLayerName = "Hull";

    [Tooltip(
        "Small distance ignored at the player's current position so merely touching " +
        "the outside of the hull does not count as teleporting through it.")]
    [SerializeField, Min(0f)] private float boardPathStartAllowance = 0.03f;

    [Tooltip(
        "Small distance ignored immediately before the authored board point so the " +
        "destination may sit directly on top of deck collision geometry.")]
    [SerializeField, Min(0f)] private float boardPathEndAllowance = 0.08f;

    [Tooltip(
        "If true, the player's actual collider footprint at the authored board point " +
        "must not overlap any BoardedInterior visibility zone on this boat.")]
    [SerializeField] private bool rejectInteriorBoardDestination = true;

    [Tooltip(
        "If true, a DeckBoardZone is invalid without an authored boardPoint. " +
        "This prevents a successful deck-board operation from leaving the player in place.")]
    [SerializeField] private bool requireAuthoredBoardPoint = true;

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
    private readonly Dictionary<PlayerBoardingState, bool> _previousJumpHeld = new();
    private readonly List<PlayerBoardingState> _scratchPlayers = new();

    private Collider2D _trigger;
    private Boat _cachedBoat;
    private bool _missingBoatLogged;

    private int _blockingHullLayer = -1;
    private int _blockingHullMask;
    private bool _missingHullLayerLogged;

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
        CacheBlockingHullLayer();
    }

    private void OnValidate()
    {
        holdSeconds = Mathf.Max(0f, holdSeconds);
        unboardArmSeconds = Mathf.Max(0f, unboardArmSeconds);
        boardPathStartAllowance = Mathf.Max(0f, boardPathStartAllowance);
        boardPathEndAllowance = Mathf.Max(0f, boardPathEndAllowance);

        if (_trigger == null)
            _trigger = GetComponent<Collider2D>();
    }

    private void OnDisable()
    {
        _overlapCounts.Clear();
        _holdTimers.Clear();
        _holdActions.Clear();
        _previousJumpHeld.Clear();
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
                CaptureCurrentJumpHeld(boarding);
            }

            if (action == ZoneAction.Unboard)
            {
                TickUnboardGesture(boarding);
                continue;
            }

            if (!IsBoardHoldIntentActive(boarding))
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
                TryBoard(boarding);

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
        {
            return
                action == ZoneAction.Unboard
                    ? $"{inputText} - {verb}"
                    : $"Hold {inputText} - {verb}";
        }

        float requiredSeconds =
            action == ZoneAction.Unboard
                ? unboardArmSeconds
                : holdSeconds;

        float progress =
            Mathf.Clamp01(
                GetHoldTimer(boarding) /
                Mathf.Max(
                    0.01f,
                    requiredSeconds));

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
            CaptureCurrentJumpHeld(boarding);
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
            _previousJumpHeld.Remove(boarding);
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

        // A DeckBoardZone is an EXTERIOR-DECK transition, never a generic
        // teleport-inside-the-boat button.
        //
        // Safety is validated while the player is still unboarded:
        //   1) direct route to boardPoint must not cross this boat's Hull;
        //   2) the player's real collider footprint at boardPoint must not overlap
        //      a BoardedInterior visibility zone.
        //
        // Only after those invariants pass do we mutate PlayerBoardingState.
        if (!TryPlaceAtSafeDeckDestination(
                boarding,
                boatRoot,
                out string safetyFailure))
        {
            if (debugLog)
            {
                Debug.Log(
                    $"[DeckBoardZone:{name}] Rejected board for '{boarding.name}': {safetyFailure}",
                    this);
            }

            return false;
        }

        boarding.BoardDeferredPresentation(
            boatRoot);

        if (zeroVelocityOnBoard)
            ZeroPlayerVelocity(boarding);

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

        // Defensive postcondition. The pre-board check should already guarantee
        // this, but never allow a DeckBoardZone transition to settle as Interior
        // if boat motion or another physics interaction changed the result.
        if (rejectInteriorBoardDestination &&
            IsPlayerOverlappingInteriorZone(
                boarding,
                expectedBoatRoot,
                out BoatVisibilityZone interiorZone))
        {
            if (debugLog)
            {
                Debug.LogWarning(
                    $"[DeckBoardZone:{name}] Post-board safety rejected Interior zone " +
                    $"'{interiorZone.name}' for '{boarding.name}'. Unboarding immediately.",
                    this);
            }

            boarding.Unboard();
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

    private bool TryPlaceAtSafeDeckDestination(
        PlayerBoardingState boarding,
        Transform boatRoot,
        out string failureReason)
    {
        failureReason = null;

        if (boarding == null)
        {
            failureReason = "Missing player boarding state.";
            return false;
        }

        if (requireAuthoredBoardPoint &&
            (!snapToBoardPoint || boardPoint == null))
        {
            failureReason =
                "Deck boarding requires an authored boardPoint and snapping enabled.";
            return false;
        }

        if (!snapToBoardPoint ||
            boardPoint == null)
        {
            // Legacy opt-out. Kept only for explicitly-authored old content.
            return true;
        }

        if (IsBoardPathBlockedByOwnHull(
                boarding,
                boatRoot,
                out Collider2D blockingHull))
        {
            failureReason =
                blockingHull != null
                    ? $"Path to deck crosses Hull collider '{blockingHull.name}'."
                    : "Path to deck crosses the boat hull.";

            return false;
        }

        Rigidbody2D rb =
            boarding.GetComponent<Rigidbody2D>();

        Vector3 originalTransformPosition =
            boarding.transform.position;

        Vector2 originalBodyPosition =
            rb != null
                ? rb.position
                : (Vector2)originalTransformPosition;

        // Move while still UNBOARDED. BoatVisibilityZone ignores unboarded players,
        // so we can validate the exact destination geometry without ever exposing an
        // Interior boarded state.
        SetPlayerPosition(
            boarding,
            boardPoint.position);

        Physics2D.SyncTransforms();

        if (rejectInteriorBoardDestination &&
            IsPlayerOverlappingInteriorZone(
                boarding,
                boatRoot,
                out BoatVisibilityZone interiorZone))
        {
            // Roll back the temporary validation placement.
            if (rb != null)
                rb.position = originalBodyPosition;
            else
                boarding.transform.position = originalTransformPosition;

            Physics2D.SyncTransforms();

            failureReason =
                interiorZone != null
                    ? $"Authored deck destination overlaps Interior zone '{interiorZone.name}'."
                    : "Authored deck destination overlaps an Interior zone.";

            return false;
        }

        // Destination is valid. Leave the player at the authored deck point and let
        // the caller atomically promote that placement to boarded state.
        return true;
    }

    private bool IsBoardPathBlockedByOwnHull(
        PlayerBoardingState boarding,
        Transform boatRoot,
        out Collider2D blockingHull)
    {
        blockingHull = null;

        if (boarding == null ||
            boatRoot == null ||
            boardPoint == null)
        {
            return false;
        }

        CacheBlockingHullLayer();

        if (_blockingHullLayer < 0 ||
            _blockingHullMask == 0)
        {
            // Fail closed. A missing safety layer should never silently turn this
            // back into a wall-teleport path.
            return true;
        }

        Vector2 start =
            ResolvePlayerReferencePoint(
                boarding);

        Vector2 target =
            boardPoint.position;

        Vector2 delta =
            target - start;

        float distance =
            delta.magnitude;

        if (distance <= 0.0001f)
            return false;

        Vector2 direction =
            delta / distance;

        float startAllowance =
            Mathf.Min(
                boardPathStartAllowance,
                distance);

        float endAllowance =
            Mathf.Min(
                boardPathEndAllowance,
                Mathf.Max(
                    0f,
                    distance - startAllowance));

        float castDistance =
            distance -
            startAllowance -
            endAllowance;

        if (castDistance <= 0.0001f)
            return false;

        Vector2 castStart =
            start +
            direction * startAllowance;

        RaycastHit2D[] hits =
            Physics2D.RaycastAll(
                castStart,
                direction,
                castDistance,
                _blockingHullMask);

        for (int i = 0;
             hits != null && i < hits.Length;
             i++)
        {
            Collider2D hit =
                hits[i].collider;

            if (hit == null ||
                !hit.enabled ||
                hit.isTrigger)
            {
                continue;
            }

            if (!BelongsToBoat(
                    hit.transform,
                    boatRoot))
            {
                continue;
            }

            blockingHull = hit;
            return true;
        }

        return false;
    }

    private bool IsPlayerOverlappingInteriorZone(
        PlayerBoardingState boarding,
        Transform boatRoot,
        out BoatVisibilityZone interiorZone)
    {
        interiorZone = null;

        if (boarding == null ||
            boatRoot == null)
        {
            return false;
        }

        BoatVisibilityZone[] zones =
            boatRoot.GetComponentsInChildren<BoatVisibilityZone>(
                true);

        if (zones == null ||
            zones.Length == 0)
        {
            return false;
        }

        Collider2D[] playerColliders =
            boarding.GetComponentsInChildren<Collider2D>(
                true);

        if (playerColliders == null ||
            playerColliders.Length == 0)
        {
            return false;
        }

        for (int z = 0;
             z < zones.Length;
             z++)
        {
            BoatVisibilityZone zone =
                zones[z];

            if (zone == null ||
                !zone.isActiveAndEnabled ||
                zone.Mode != BoatVisibilityMode.BoardedInterior)
            {
                continue;
            }

            Collider2D zoneCollider =
                zone.GetComponent<Collider2D>();

            if (zoneCollider == null ||
                !zoneCollider.enabled)
            {
                continue;
            }

            for (int p = 0;
                 p < playerColliders.Length;
                 p++)
            {
                Collider2D playerCollider =
                    playerColliders[p];

                if (playerCollider == null ||
                    !playerCollider.enabled)
                {
                    continue;
                }

                ColliderDistance2D distance =
                    Physics2D.Distance(
                        zoneCollider,
                        playerCollider);

                // BoatVisualStateController resolves Interior when player colliders
                // physically touch the zone. Treat overlap or essentially-zero
                // separation as invalid for a DeckBoardZone destination.
                if (distance.isOverlapped ||
                    distance.distance <= 0.001f)
                {
                    interiorZone = zone;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool BelongsToBoat(
        Transform candidate,
        Transform boatRoot)
    {
        if (candidate == null ||
            boatRoot == null)
        {
            return false;
        }

        return
            candidate == boatRoot ||
            candidate.IsChildOf(boatRoot);
    }

    private static Vector2 ResolvePlayerReferencePoint(
        PlayerBoardingState boarding)
    {
        if (boarding == null)
            return Vector2.zero;

        Rigidbody2D rb =
            boarding.GetComponent<Rigidbody2D>();

        if (rb != null)
            return rb.worldCenterOfMass;

        return boarding.transform.position;
    }

    private void CacheBlockingHullLayer()
    {
        _blockingHullLayer =
            string.IsNullOrWhiteSpace(
                blockingHullLayerName)
                ? -1
                : LayerMask.NameToLayer(
                    blockingHullLayerName);

        _blockingHullMask =
            _blockingHullLayer >= 0
                ? 1 << _blockingHullLayer
                : 0;

        if (_blockingHullLayer < 0 &&
            !_missingHullLayerLogged)
        {
            _missingHullLayerLogged = true;

            Debug.LogError(
                $"[DeckBoardZone:{name}] Safety layer '{blockingHullLayerName}' does not exist. " +
                "Deck boarding will fail closed rather than permit wall teleporting.",
                this);
        }
    }

    private static void SetPlayerPosition(
        PlayerBoardingState boarding,
        Vector2 target)
    {
        if (boarding == null)
            return;

        Rigidbody2D rb =
            boarding.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.position = target;
            return;
        }

        Vector3 position =
            boarding.transform.position;

        position.x = target.x;
        position.y = target.y;

        boarding.transform.position =
            position;
    }

    private static void ZeroPlayerVelocity(
        PlayerBoardingState boarding)
    {
        if (boarding == null)
            return;

        Rigidbody2D rb =
            boarding.GetComponent<Rigidbody2D>();

        if (rb == null)
            return;

        rb.linearVelocity =
            Vector2.zero;

        rb.angularVelocity =
            0f;
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

    private bool IsBoardHoldIntentActive(
        PlayerBoardingState boarding)
    {
        if (boarding == null)
            return false;

        IInteractionIntentSource interaction =
            ResolveInteractionIntentSource(boarding);

        if (interaction != null &&
            interaction.Current.InteractHeld)
        {
            return true;
        }

        if (!allowSecondaryHoldKey)
            return false;

        ICharacterIntentSource character =
            ResolveCharacterIntentSource(boarding);

        return
            character != null &&
            character.Current.ClimbUpHeld;
    }

    private void TickUnboardGesture(
        PlayerBoardingState boarding)
    {
        ICharacterIntentSource character =
            ResolveCharacterIntentSource(boarding);

        if (character == null)
        {
            ResetHold(boarding);
            return;
        }

        CharacterIntent intent =
            character.Current;

        bool previousJumpHeld =
            _previousJumpHeld.TryGetValue(
                boarding,
                out bool stored) &&
            stored;

        bool newJumpPress =
            intent.JumpHeld &&
            !previousJumpHeld;

        _previousJumpHeld[boarding] =
            intent.JumpHeld;

        // Down/S only arms the leave action. It never unboards by itself.
        if (!intent.ClimbDownHeld)
        {
            _holdTimers[boarding] = 0f;
            return;
        }

        float timer =
            GetHoldTimer(boarding) +
            Time.deltaTime;

        _holdTimers[boarding] = timer;

        if (timer < unboardArmSeconds ||
            !newJumpPress)
        {
            return;
        }

        bool succeeded =
            TryUnboard(boarding);

        _holdTimers[boarding] = 0f;

        if (succeeded)
            _holdActions[boarding] = ZoneAction.None;
    }

    private void CaptureCurrentJumpHeld(
        PlayerBoardingState boarding)
    {
        if (boarding == null)
            return;

        ICharacterIntentSource character =
            ResolveCharacterIntentSource(boarding);

        _previousJumpHeld[boarding] =
            character != null &&
            character.Current.JumpHeld;
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
        CaptureCurrentJumpHeld(boarding);
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
            {
                return
                    $"{localCharacter.ClimbDownBindingLabel}, then " +
                    $"{localCharacter.JumpBindingLabel}";
            }

            return "Down, then Jump";
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

        float requiredSeconds =
            action == ZoneAction.Unboard
                ? unboardArmSeconds
                : holdSeconds;

        float progress =
            Mathf.Clamp01(
                GetHoldTimer(boarding) /
                Mathf.Max(
                    0.01f,
                    requiredSeconds));

        string actionText =
            action == ZoneAction.Unboard
                ? $"{inputText} to {verb}"
                : $"Hold {inputText} to {verb}";

        actions.Add(
            new PromptAction(
                actionText,
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
