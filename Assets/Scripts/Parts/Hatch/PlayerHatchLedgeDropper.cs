using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerHatchLedgeDropper : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private LocalCharacterIntentSource localCharacterIntentSource;
    [SerializeField] private CharacterMotor2D motor;

    [Header("Detection")]
    [Tooltip("Layers containing hatch ledges. Usually Hull.")]
    [SerializeField] private LayerMask ledgeMask;

    [SerializeField, Min(0.01f)]
    private float ledgeDetectRadiusPadding = 0.08f;

    [Header("Drop Through")]
    [SerializeField, Min(0.05f)]
    private float ignoreCollisionSeconds = 0.35f;

    [SerializeField, Min(0f)]
    private float downwardNudgeVelocity = 0.75f;

    [SerializeField] private bool requireGroundedOnLedge = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private Rigidbody2D _rb;
    private Collider2D[] _playerColliders;
    private bool _dropInProgress;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();

        if (localCharacterIntentSource == null)
            localCharacterIntentSource = GetComponent<LocalCharacterIntentSource>();

        if (motor == null)
            motor = GetComponent<CharacterMotor2D>();

        _playerColliders = GetComponentsInChildren<Collider2D>(true);
    }

    private void FixedUpdate()
    {
        if (_dropInProgress)
            return;

        if (localCharacterIntentSource == null)
            return;

        CharacterIntent intent = localCharacterIntentSource.Current;

        if (!intent.ClimbDownHeld)
            return;

        TryDropThroughLedge();
    }

    private void TryDropThroughLedge()
    {
        if (motor != null)
            motor.UpdateGrounded();

        if (requireGroundedOnLedge && motor != null && !motor.IsGrounded)
            return;

        Vector2 checkPos = motor != null
            ? (Vector2)transform.position + motor.groundCheckLocalOffset
            : _rb.position;

        float radius = motor != null
            ? motor.groundCheckRadius + ledgeDetectRadiusPadding
            : 0.25f;

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            checkPos,
            radius,
            ledgeMask.value != 0 ? ledgeMask : motor != null ? motor.groundMask : ~0);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            HatchLedge ledge = hit.GetComponent<HatchLedge>();
            if (ledge == null)
                ledge = hit.GetComponentInParent<HatchLedge>();

            if (ledge == null)
                continue;

            Collider2D ledgeCollider = ledge.Collider;
            if (ledgeCollider == null || !ledgeCollider.enabled)
                continue;

            StartCoroutine(DropThroughRoutine(ledgeCollider));
            return;
        }
    }

    private IEnumerator DropThroughRoutine(Collider2D ledgeCollider)
    {
        _dropInProgress = true;

        if (_playerColliders == null || _playerColliders.Length == 0)
            _playerColliders = GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < _playerColliders.Length; i++)
        {
            Collider2D playerCol = _playerColliders[i];
            if (playerCol == null || ledgeCollider == null)
                continue;

            Physics2D.IgnoreCollision(playerCol, ledgeCollider, true);
        }

        Vector2 v = _rb.linearVelocity;
        if (v.y > -downwardNudgeVelocity)
            v.y = -downwardNudgeVelocity;

        _rb.linearVelocity = v;

        if (debugLogs)
            Debug.Log($"[PlayerHatchLedgeDropper:{name}] Dropping through ledge '{ledgeCollider.name}'.", this);

        yield return new WaitForSeconds(ignoreCollisionSeconds);

        if (ledgeCollider != null)
        {
            for (int i = 0; i < _playerColliders.Length; i++)
            {
                Collider2D playerCol = _playerColliders[i];
                if (playerCol == null)
                    continue;

                Physics2D.IgnoreCollision(playerCol, ledgeCollider, false);
            }
        }

        _dropInProgress = false;
    }
}