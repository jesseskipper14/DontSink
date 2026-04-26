using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class StairSlopeAccess : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Collider2D accessTrigger;
    [SerializeField] private Collider2D slopeCollider;
    [SerializeField] private StairSlopeAuthoring stairAuthoring;

    [Header("Engage Rules")]
    [SerializeField] private float maxSnapUpDistance = 0.55f;
    [SerializeField] private float maxBelowSurfaceTolerance = 0.08f;
    [SerializeField] private float disengageBelowSurfaceDistance = 0.35f;
    [SerializeField] private float dropThroughSeconds = 0.25f;

    private readonly Dictionary<PlayerBoardingState, PlayerAccess> _players = new();

    private sealed class PlayerAccess
    {
        public PlayerBoardingState Boarding;
        public Rigidbody2D Rigidbody;
        public Collider2D[] Colliders;
        public ICharacterIntentSource IntentSource;

        public bool Engaged;
        public bool CurrentlyIgnoring = true;
        public float IgnoreUntilTime;
    }

    private void Reset()
    {
        if (slopeCollider == null)
            slopeCollider = GetComponent<Collider2D>();

        if (stairAuthoring == null)
            stairAuthoring = GetComponent<StairSlopeAuthoring>();

        Collider2D[] children = GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D col in children)
        {
            if (col != null && col.isTrigger)
            {
                accessTrigger = col;
                break;
            }
        }
    }

    private void Awake()
    {
        if (slopeCollider == null)
            slopeCollider = GetComponent<Collider2D>();

        if (stairAuthoring == null)
            stairAuthoring = GetComponent<StairSlopeAuthoring>();

        if (slopeCollider == null)
        {
            Debug.LogError("[StairSlopeAccess] Missing slope collider.", this);
            enabled = false;
            return;
        }

        slopeCollider.enabled = true;
        slopeCollider.isTrigger = false;
    }

    private void Update()
    {
        if (slopeCollider == null)
            return;

        foreach (var kvp in _players)
        {
            PlayerAccess access = kvp.Value;
            if (access == null || access.Boarding == null)
                continue;

            CharacterIntent intent = access.IntentSource != null
                ? access.IntentSource.Current
                : default;

            bool wantsClimb = intent.ClimbUpHeld;
            bool wantsDrop = intent.ClimbDownHeld;

            if (wantsDrop)
            {
                access.Engaged = false;
                access.IgnoreUntilTime = Time.time + dropThroughSeconds;
            }

            bool inDropGrace = Time.time < access.IgnoreUntilTime;

            if (inDropGrace)
            {
                SetPlayerIgnoring(access, true);
                continue;
            }

            StairSurfaceInfo surface = GetSurfaceInfo(access);

            if (!access.Engaged)
            {
                if (wantsClimb && surface.CanEngage)
                    access.Engaged = true;
            }
            else
            {
                if (!surface.ShouldRemainEngaged)
                    access.Engaged = false;
            }

            SetPlayerIgnoring(access, !access.Engaged);
        }
    }

    public void NotifyTriggerEnter(Collider2D other)
    {
        RegisterPlayer(other);
    }

    public void NotifyTriggerStay(Collider2D other)
    {
        RegisterPlayer(other);
    }

    public void NotifyTriggerExit(Collider2D other)
    {
        PlayerBoardingState boarding = other.GetComponentInParent<PlayerBoardingState>();
        if (boarding == null)
            return;

        if (!_players.TryGetValue(boarding, out PlayerAccess access))
            return;

        access.Engaged = false;
        SetPlayerIgnoring(access, false);
        _players.Remove(boarding);
    }

    private void RegisterPlayer(Collider2D other)
    {
        PlayerBoardingState boarding = other.GetComponentInParent<PlayerBoardingState>();
        if (boarding == null)
            return;

        if (_players.ContainsKey(boarding))
            return;

        PlayerAccess access = new PlayerAccess
        {
            Boarding = boarding,
            Rigidbody = boarding.GetComponent<Rigidbody2D>(),
            Colliders = boarding.GetComponentsInChildren<Collider2D>(true),
            IntentSource = boarding.GetComponent<ICharacterIntentSource>(),
            Engaged = false,
            CurrentlyIgnoring = false,
        };

        _players.Add(boarding, access);

        // Default: pass through stairs until climb intent engages them.
        SetPlayerIgnoring(access, true);
    }

    private struct StairSurfaceInfo
    {
        public bool CanEngage;
        public bool ShouldRemainEngaged;
    }

    private StairSurfaceInfo GetSurfaceInfo(PlayerAccess access)
    {
        if (access == null || access.Colliders == null || access.Colliders.Length == 0)
            return default;

        Bounds playerBounds = GetCombinedColliderBounds(access.Colliders);

        Vector2 footWorld = new Vector2(
            playerBounds.center.x,
            playerBounds.min.y);

        Vector2 footLocal = transform.InverseTransformPoint(footWorld);

        float width = stairAuthoring != null ? Mathf.Max(0.01f, stairAuthoring.Run) : Mathf.Max(0.01f, slopeCollider.bounds.size.x);
        float height = stairAuthoring != null ? Mathf.Max(0.01f, stairAuthoring.Rise) : Mathf.Max(0.01f, slopeCollider.bounds.size.y);

        float hw = width * 0.5f;
        float hh = height * 0.5f;

        bool insideX = footLocal.x >= -hw && footLocal.x <= hw;
        if (!insideX)
            return default;

        bool ascendRight = IsAscendRight();
        float t = ascendRight
            ? Mathf.InverseLerp(-hw, hw, footLocal.x)
            : Mathf.InverseLerp(hw, -hw, footLocal.x);

        float slopeYLocal = Mathf.Lerp(-hh, hh, t);
        float verticalDelta = slopeYLocal - footLocal.y;

        bool closeEnoughToSnap =
            verticalDelta >= -maxBelowSurfaceTolerance &&
            verticalDelta <= maxSnapUpDistance;

        bool stillOnOrNearSurface =
            footLocal.y >= slopeYLocal - disengageBelowSurfaceDistance;

        return new StairSurfaceInfo
        {
            CanEngage = closeEnoughToSnap,
            ShouldRemainEngaged = stillOnOrNearSurface
        };
    }

    private Bounds GetCombinedColliderBounds(Collider2D[] colliders)
    {
        bool hasBounds = false;
        Bounds result = default;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D col = colliders[i];
            if (col == null || col.isTrigger)
                continue;

            if (!hasBounds)
            {
                result = col.bounds;
                hasBounds = true;
            }
            else
            {
                result.Encapsulate(col.bounds);
            }
        }

        return hasBounds ? result : new Bounds(transform.position, Vector3.one);
    }

    private bool IsAscendRight()
    {
        // Temporary because StairSlopeAuthoring currently exposes Run/Rise but not direction.
        // Add a public property there if you have not already:
        //
        // public bool AscendRight => ascendRight;
        //
        if (stairAuthoring != null)
            return stairAuthoring.AscendRight;

        return true;
    }

    private void SetPlayerIgnoring(PlayerAccess access, bool ignore)
    {
        if (access == null || access.Colliders == null || slopeCollider == null)
            return;

        if (access.CurrentlyIgnoring == ignore)
            return;

        access.CurrentlyIgnoring = ignore;

        for (int i = 0; i < access.Colliders.Length; i++)
        {
            Collider2D playerCollider = access.Colliders[i];
            if (playerCollider == null || playerCollider.isTrigger)
                continue;

            Physics2D.IgnoreCollision(playerCollider, slopeCollider, ignore);
        }
    }
}