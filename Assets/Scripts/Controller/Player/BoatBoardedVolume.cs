using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class BoatBoardedVolume : MonoBehaviour
{
    private static readonly List<BoatBoardedVolume> ActiveVolumes = new();

    [SerializeField] private Transform boatRoot;

    private Collider2D _volumeCollider;
    private bool _isTearingDown;

    public Transform BoatRoot => boatRoot;

    private void Awake()
    {
        _volumeCollider = GetComponent<Collider2D>();
        _volumeCollider.isTrigger = true;

        if (boatRoot == null)
            boatRoot = transform.root;
    }

    private void OnEnable()
    {
        _isTearingDown = false;

        if (_volumeCollider == null)
            _volumeCollider = GetComponent<Collider2D>();

        if (!ActiveVolumes.Contains(this))
            ActiveVolumes.Add(this);
    }

    private void OnDisable()
    {
        // If we're disabling (scene load, boat despawn, pooling), do NOT auto-unboard.
        _isTearingDown = true;

        ActiveVolumes.Remove(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // If this volume is being disabled, ignore exits. They're not "real gameplay exits".
        if (_isTearingDown)
            return;

        // If the boat root is disabling, also ignore.
        if (boatRoot != null && !boatRoot.gameObject.activeInHierarchy)
            return;

        var boarding = other.GetComponentInParent<PlayerBoardingState>();
        if (boarding == null)
            return;

        // Only unboard if they were boarded to THIS boat.
        if (boarding.IsBoarded &&
            boarding.CurrentBoatRoot == boatRoot)
        {
            Physics2D.SyncTransforms();

            Vector2 playerPoint =
                ResolvePlayerReferencePoint(
                    boarding);

            // One child collider can exit while the player's physical center is
            // still legitimately inside this boat. Do not let that transient exit
            // callback tear down the entire boat context.
            if (TryFindContainingVolume(
                    playerPoint,
                    out BoatBoardedVolume containingVolume) &&
                containingVolume != null &&
                containingVolume.BoatRoot == boatRoot)
            {
                boarding.RefreshCurrentBoatVisualState();
                return;
            }

            boarding.transform.SetParent(
                null,
                worldPositionStays: true);

            boarding.Unboard();
        }
    }

    public bool ContainsWorldPoint(Vector2 worldPoint)
    {
        if (_volumeCollider == null)
            _volumeCollider = GetComponent<Collider2D>();

        if (_volumeCollider == null)
            return false;

        // Accurate for trigger shape. Bounds fallback would be easier, and worse, because
        // apparently rectangles lie when boats get interesting.
        return _volumeCollider.OverlapPoint(worldPoint);
    }

    public static bool TryFindContainingVolume(Vector2 worldPoint, out BoatBoardedVolume volume)
    {
        for (int i = ActiveVolumes.Count - 1; i >= 0; i--)
        {
            BoatBoardedVolume candidate = ActiveVolumes[i];

            if (candidate == null)
            {
                ActiveVolumes.RemoveAt(i);
                continue;
            }

            if (!candidate.isActiveAndEnabled)
                continue;

            if (candidate.ContainsWorldPoint(worldPoint))
            {
                volume = candidate;
                return true;
            }
        }

        volume = null;
        return false;
    }

    public static bool TryFindContainingVolume(MoneyChestState chest, out BoatBoardedVolume volume)
    {
        if (chest == null)
        {
            volume = null;
            return false;
        }

        return TryFindContainingVolume(chest.transform.position, out volume);
    }

    public static bool TryFindContainingVolume(
        PlayerBoardingState boarding,
        out BoatBoardedVolume volume)
    {
        if (boarding == null)
        {
            volume = null;
            return false;
        }

        return
            TryFindContainingVolume(
                ResolvePlayerReferencePoint(
                    boarding),
                out volume);
    }

    public static bool IsInsideAnyVolume(Vector2 worldPoint)
    {
        return TryFindContainingVolume(worldPoint, out _);
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

    public static bool IsInsideAnyVolume(MoneyChestState chest)
    {
        return TryFindContainingVolume(chest, out _);
    }
}