using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Failsafe for a diving-bell occupant who physically leaves the authored interior
/// without using a legitimate exit interaction.
///
/// This is NOT the future bottom-exit mechanic. It exists so clipping, tunneling,
/// bad spawn placement, or collider mistakes cannot leave a player permanently
/// stuck in the bell-only collision context.
///
/// Author one trigger Collider2D that describes the safe walkable interior volume.
/// If an occupant's primary solid body center remains outside that volume longer
/// than Outside Grace Seconds, that occupant is emergency-ejected.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class DivingBellOccupantContainmentGuard :
    MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private DivingBellOccupancy occupancy;

    [Tooltip(
        "Trigger volume defining the allowed bell interior. Defaults to the Collider2D " +
        "on this GameObject.")]
    [SerializeField] private Collider2D containmentVolume;

    [Header("Safety")]
    [SerializeField, Min(0f)]
    private float outsideGraceSeconds =
        0.12f;

    [SerializeField]
    private bool emergencyEjectWhenOutside =
        true;

    [Header("Debug")]
    [SerializeField]
    private bool verboseLogging =
        false;

    private readonly Dictionary<PlayerBellOccupantState, float>
        _outsideSince =
            new Dictionary<PlayerBellOccupantState, float>();

    private readonly List<PlayerBellOccupantState>
        _scratch =
            new List<PlayerBellOccupantState>();

    private void Awake()
    {
        ResolveRefs();

        if (containmentVolume != null &&
            !containmentVolume.isTrigger)
        {
            Debug.LogWarning(
                $"[DivingBellOccupantContainmentGuard:{name}] " +
                "Containment Collider2D should be Is Trigger.",
                this);
        }
    }

    private void OnValidate()
    {
        outsideGraceSeconds =
            Mathf.Max(
                0f,
                outsideGraceSeconds);

        ResolveRefs();
    }

    private void LateUpdate()
    {
        ResolveRefs();

        if (occupancy == null ||
            containmentVolume == null ||
            !containmentVolume.enabled)
        {
            _outsideSince.Clear();
            return;
        }

        IReadOnlyList<PlayerBellOccupantState> occupants =
            occupancy.Occupants;

        _scratch.Clear();

        if (occupants != null)
        {
            for (int i = 0;
                 i < occupants.Count;
                 i++)
            {
                PlayerBellOccupantState state =
                    occupants[i];

                if (state == null ||
                    !state.IsInside(
                        occupancy))
                {
                    continue;
                }

                _scratch.Add(
                    state);

                Vector2 anchor =
                    ResolvePlayerBodyCenter(
                        state);

                bool inside =
                    containmentVolume.OverlapPoint(
                        anchor);

                if (inside)
                {
                    _outsideSince.Remove(
                        state);

                    continue;
                }

                if (!_outsideSince.TryGetValue(
                        state,
                        out float since))
                {
                    _outsideSince[state] =
                        Time.time;

                    continue;
                }

                if (Time.time - since <
                    outsideGraceSeconds)
                {
                    continue;
                }

                if (verboseLogging)
                {
                    Debug.LogWarning(
                        $"[DivingBellOccupantContainmentGuard:{name}] " +
                        $"'{state.name}' left the bell containment volume at {anchor}.",
                        state);
                }

                _outsideSince.Remove(
                    state);

                if (emergencyEjectWhenOutside)
                {
                    occupancy.TryEmergencyEjectOccupant(
                        state.gameObject,
                        "Occupant left diving bell containment.");
                }
            }
        }

        // Clean timers for players no longer occupying this bell.
        _scratch.RemoveAll(
            state => state == null);

        List<PlayerBellOccupantState> stale =
            null;

        foreach (var pair in _outsideSince)
        {
            if (pair.Key == null ||
                !_scratch.Contains(
                    pair.Key))
            {
                stale ??=
                    new List<PlayerBellOccupantState>();

                stale.Add(
                    pair.Key);
            }
        }

        if (stale != null)
        {
            for (int i = 0;
                 i < stale.Count;
                 i++)
            {
                _outsideSince.Remove(
                    stale[i]);
            }
        }
    }

    private Vector2 ResolvePlayerBodyCenter(
        PlayerBellOccupantState state)
    {
        if (state == null)
            return Vector2.zero;

        Rigidbody2D rb =
            state.GetComponent<Rigidbody2D>() ??
            state.GetComponentInChildren<Rigidbody2D>(
                true);

        Collider2D[] colliders =
            state.GetComponentsInChildren<Collider2D>(
                true);

        Collider2D best =
            null;

        float bestArea =
            -1f;

        if (colliders != null)
        {
            for (int i = 0;
                 i < colliders.Length;
                 i++)
            {
                Collider2D collider =
                    colliders[i];

                if (collider == null ||
                    !collider.enabled ||
                    collider.isTrigger)
                {
                    continue;
                }

                if (rb != null &&
                    collider.attachedRigidbody != rb)
                {
                    continue;
                }

                Bounds bounds =
                    collider.bounds;

                float area =
                    Mathf.Abs(
                        bounds.size.x *
                        bounds.size.y);

                if (area <= bestArea)
                    continue;

                bestArea =
                    area;

                best =
                    collider;
            }
        }

        if (best != null)
            return best.bounds.center;

        if (rb != null)
            return rb.worldCenterOfMass;

        return state.transform.position;
    }

    private void ResolveRefs()
    {
        if (containmentVolume == null)
        {
            containmentVolume =
                GetComponent<Collider2D>();
        }

        if (occupancy == null)
        {
            occupancy =
                GetComponentInParent<DivingBellOccupancy>() ??
                GetComponentInChildren<DivingBellOccupancy>(
                    true);
        }
    }
}
