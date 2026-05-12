using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HatchRuntime : MonoBehaviour
{
    [Header("Authoring")]
    [SerializeField] private HatchAuthoring authoring;

    [Header("Runtime State")]
    [SerializeField] private bool isOpen;

    [Header("Open Ledge")]
    [SerializeField] private bool enableLedgeWhenOpen = true;

    [Header("Close Occupancy Check")]
    [Tooltip("When closing, these layers are checked inside the blocking collider area. Usually Player + Cargo/Items, NOT Hull.")]
    [SerializeField] private LayerMask closeObstructionMask = ~0;

    [Tooltip("If false, trigger colliders will not block hatch closing.")]
    [SerializeField] private bool triggersBlockClosing = false;

    [Tooltip("Ignore colliders that belong to this hatch object and its children.")]
    [SerializeField] private bool ignoreOwnColliders = true;

    [Tooltip("Ignore colliders under the same Boat root. Usually false if you want cargo/player under the boat to block; use layer mask to avoid Hull false positives.")]
    [SerializeField] private bool ignoreSameBoatRoot = false;

    [Header("Denied Close Feedback")]
    [SerializeField] private bool vibrateOnDeniedClose = true;

    [SerializeField, Min(0.01f)]
    private float deniedVibrationDuration = 0.14f;

    [SerializeField, Min(0f)]
    private float deniedVibrationDistance = 0.035f;

    [SerializeField, Min(1f)]
    private float deniedVibrationFrequency = 36f;

    [Header("Debug")]
    [SerializeField] private bool logDeniedClose = true;

    public event Action<bool> StateChanged;

    public bool IsOpen => isOpen;
    public HatchAuthoring Authoring => authoring;

    private Coroutine _vibrationRoutine;
    private Vector3 _baseLocalPosition;
    private bool _hasBaseLocalPosition;

    private Collider2D[] _ownColliders;
    private Boat _owningBoat;

    private void Reset()
    {
        if (authoring == null)
            authoring = GetComponent<HatchAuthoring>();
    }

    private void Awake()
    {
        ResolveRefs();

        if (authoring == null)
        {
            Debug.LogError("[HatchRuntime] Missing HatchAuthoring.", this);
            enabled = false;
            return;
        }

        isOpen = authoring.StartsOpen;
        RefreshPresentation();
    }

    private void OnDisable()
    {
        if (_vibrationRoutine != null)
        {
            StopCoroutine(_vibrationRoutine);
            _vibrationRoutine = null;
        }

        RestoreBaseLocalPosition();
    }

    private void ResolveRefs()
    {
        if (authoring == null)
            authoring = GetComponent<HatchAuthoring>();

        _ownColliders = GetComponentsInChildren<Collider2D>(true);
        _owningBoat = GetComponentInParent<Boat>();
    }

    public bool CanToggle(out string reason)
    {
        reason = null;

        if (authoring == null)
        {
            reason = "Missing hatch authoring.";
            return false;
        }

        // Opening is always allowed.
        if (!isOpen)
            return true;

        // Closing is conditional.
        if (!CanClose(out Collider2D blocker, out reason))
        {
            if (blocker != null)
                reason = $"Blocked by {blocker.name}.";
            else if (string.IsNullOrWhiteSpace(reason))
                reason = "Blocked.";

            return false;
        }

        return true;
    }

    public bool Toggle()
    {
        return SetOpen(!isOpen);
    }

    public bool SetOpen(bool open)
    {
        if (isOpen == open)
            return false;

        if (!open && !CanClose(out Collider2D blocker, out string reason))
        {
            if (logDeniedClose)
            {
                Debug.LogWarning(
                    $"[HatchRuntime:{name}] Close denied. {reason} " +
                    $"blocker={(blocker != null ? blocker.name : "NULL")}",
                    blocker != null ? blocker : this);
            }

            PlayDeniedCloseFeedback();
            return false;
        }

        isOpen = open;
        RefreshPresentation();
        StateChanged?.Invoke(isOpen);
        return true;
    }

    public void RefreshPresentation()
    {
        if (authoring == null)
            return;

        if (authoring.FrameRenderer != null)
            authoring.FrameRenderer.enabled = true;

        if (authoring.ClosedRenderer != null)
            authoring.ClosedRenderer.enabled = !isOpen;

        if (authoring.OpenRenderer != null)
            authoring.OpenRenderer.enabled = isOpen;

        if (authoring.BlockingCollider != null)
            authoring.BlockingCollider.enabled = !isOpen;

        if (authoring.LedgeCollider != null)
            authoring.LedgeCollider.enabled = enableLedgeWhenOpen && isOpen;
    }

    private bool CanClose(out Collider2D blocker, out string reason)
    {
        blocker = null;
        reason = null;

        if (authoring == null)
        {
            reason = "Missing authoring.";
            return false;
        }

        Collider2D blockingCollider = authoring.BlockingCollider;
        if (blockingCollider == null)
            return true;

        Collider2D[] hits = QueryBlockingVolume(blockingCollider);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            if (ShouldIgnoreCloseHit(hit))
                continue;

            blocker = hit;
            reason = $"Closing volume occupied by '{hit.name}'.";
            return false;
        }

        return true;
    }

    private Collider2D[] QueryBlockingVolume(Collider2D blockingCollider)
    {
        if (blockingCollider is BoxCollider2D box)
        {
            Vector2 center = box.transform.TransformPoint(box.offset);

            Vector2 size = new Vector2(
                Mathf.Abs(box.size.x * box.transform.lossyScale.x),
                Mathf.Abs(box.size.y * box.transform.lossyScale.y));

            float angle = box.transform.eulerAngles.z;

            return Physics2D.OverlapBoxAll(
                center,
                size,
                angle,
                closeObstructionMask);
        }

        Bounds b = blockingCollider.bounds;

        return Physics2D.OverlapBoxAll(
            b.center,
            b.size,
            0f,
            closeObstructionMask);
    }

    private bool ShouldIgnoreCloseHit(Collider2D hit)
    {
        if (hit == null)
            return true;

        if (!triggersBlockClosing && hit.isTrigger)
            return true;

        if (ignoreOwnColliders && IsOwnCollider(hit))
            return true;

        if (ignoreSameBoatRoot && _owningBoat != null)
        {
            Boat hitBoat = hit.GetComponentInParent<Boat>();
            if (hitBoat == _owningBoat)
                return true;
        }

        return false;
    }

    private bool IsOwnCollider(Collider2D hit)
    {
        if (_ownColliders == null || _ownColliders.Length == 0)
            _ownColliders = GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < _ownColliders.Length; i++)
        {
            if (_ownColliders[i] == hit)
                return true;
        }

        return false;
    }

    private void PlayDeniedCloseFeedback()
    {
        if (!vibrateOnDeniedClose)
            return;

        if (_vibrationRoutine != null)
            StopCoroutine(_vibrationRoutine);

        if (!_hasBaseLocalPosition)
        {
            _baseLocalPosition = transform.localPosition;
            _hasBaseLocalPosition = true;
        }

        _vibrationRoutine = StartCoroutine(DeniedCloseVibrationRoutine());
    }

    private IEnumerator DeniedCloseVibrationRoutine()
    {
        float elapsed = 0f;

        while (elapsed < deniedVibrationDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / deniedVibrationDuration);
            float fade = 1f - t;

            float wave = Mathf.Sin(elapsed * deniedVibrationFrequency * Mathf.PI * 2f);
            Vector3 offset = transform.right * (wave * deniedVibrationDistance * fade);

            transform.localPosition = _baseLocalPosition + offset;

            yield return null;
        }

        RestoreBaseLocalPosition();
        _vibrationRoutine = null;
    }

    private void RestoreBaseLocalPosition()
    {
        if (!_hasBaseLocalPosition)
            return;

        transform.localPosition = _baseLocalPosition;
    }

    public string AccessStateId =>
    authoring != null ? authoring.HatchId : null;

    public void RestoreOpenState(bool open)
    {
        isOpen = open;
        RefreshPresentation();
        StateChanged?.Invoke(isOpen);
    }

#if UNITY_EDITOR
    [ContextMenu("Open Hatch")]
    private void EditorOpen()
    {
        isOpen = true;
        RefreshPresentation();
    }

    [ContextMenu("Close Hatch")]
    private void EditorClose()
    {
        isOpen = false;
        RefreshPresentation();
    }

    [ContextMenu("Refresh Hatch Presentation")]
    private void EditorRefresh()
    {
        RefreshPresentation();
    }

    private void OnValidate()
    {
        if (authoring == null)
            authoring = GetComponent<HatchAuthoring>();

        deniedVibrationDuration = Mathf.Max(0.01f, deniedVibrationDuration);
        deniedVibrationDistance = Mathf.Max(0f, deniedVibrationDistance);
        deniedVibrationFrequency = Mathf.Max(1f, deniedVibrationFrequency);

        if (!Application.isPlaying)
            RefreshPresentation();
    }

    private void OnDrawGizmosSelected()
    {
        if (authoring == null)
            authoring = GetComponent<HatchAuthoring>();

        if (authoring == null)
            return;

        if (authoring.BlockingCollider != null)
            DrawColliderGizmo(authoring.BlockingCollider, new Color(1f, 0.2f, 0.9f, 0.25f), new Color(1f, 0.2f, 0.9f, 0.85f));

        if (authoring.LedgeCollider != null)
            DrawColliderGizmo(authoring.LedgeCollider, new Color(0.2f, 1f, 0.4f, 0.20f), new Color(0.2f, 1f, 0.4f, 0.85f));
    }

    private static void DrawColliderGizmo(Collider2D col, Color fill, Color wire)
    {
        Gizmos.color = fill;

        if (col is BoxCollider2D box)
        {
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = box.transform.localToWorldMatrix;

            Gizmos.DrawCube(box.offset, box.size);
            Gizmos.color = wire;
            Gizmos.DrawWireCube(box.offset, box.size);

            Gizmos.matrix = old;
        }
        else
        {
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
            Gizmos.color = wire;
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
#endif
}