using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AnchorModule : TetherPayloadModule
{
    [Header("Anchor Holding")]
    [SerializeField, Min(0f)] private float holdingForceNewtons = 8000f;
    [SerializeField] private LayerMask validSeafloorLayers = ~0;
    [SerializeField, Range(-1f, 1f)] private float minimumGroundNormalY = 0.25f;

    [Header("Anchor Balance")]
    [Tooltip("Optional child transform defining the deployed Rigidbody2D center of mass. Place this below the visual center so the anchor naturally hangs heavy-end-down.")]
    [SerializeField] private Transform centerOfMassPoint;

    private readonly HashSet<Collider2D> _validGroundContacts = new();

    public float HoldingForceNewtons => Mathf.Max(0f, holdingForceNewtons);
    public bool IsOnBottom => _validGroundContacts.Count > 0;

    private void Awake()
    {
        ApplyCenterOfMass();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyCenterOfMass();
    }
#endif

    private void ApplyCenterOfMass()
    {
        if (Rigidbody == null || centerOfMassPoint == null)
            return;

        Rigidbody.centerOfMass =
            Rigidbody.transform.InverseTransformPoint(
                centerOfMassPoint.position);
    }

    private void FixedUpdate()
    {
        if (IsStowed || IsCutLoose || !IsOnBottom || Rigidbody == null)
            return;

        SetPayloadState(TetherDeploymentState.Holding);

        float dt = Mathf.Max(0.0001f, Time.fixedDeltaTime);
        float forceToCancelHorizontalVelocity = -(Rigidbody.linearVelocity.x * Rigidbody.mass) / dt;
        float clamped = Mathf.Clamp(
            forceToCancelHorizontalVelocity,
            -HoldingForceNewtons,
            HoldingForceNewtons);

        Rigidbody.AddForce(Vector2.right * clamped, ForceMode2D.Force);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        EvaluateGroundContact(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        EvaluateGroundContact(collision);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision != null && collision.collider != null)
            _validGroundContacts.Remove(collision.collider);

        if (_validGroundContacts.Count == 0 && !IsStowed && !IsCutLoose)
            SetPayloadState(TetherDeploymentState.Suspended);
    }

    private void EvaluateGroundContact(Collision2D collision)
    {
        if (collision == null || collision.collider == null)
            return;

        int layerBit = 1 << collision.collider.gameObject.layer;
        if ((validSeafloorLayers.value & layerBit) == 0)
        {
            _validGroundContacts.Remove(collision.collider);
            return;
        }

        bool validNormal = false;
        int contactCount = collision.contactCount;

        for (int i = 0; i < contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            if (contact.normal.y >= minimumGroundNormalY)
            {
                validNormal = true;
                break;
            }
        }

        if (validNormal)
        {
            _validGroundContacts.Add(collision.collider);
            if (!IsStowed && !IsCutLoose && State != TetherDeploymentState.Holding)
                SetPayloadState(TetherDeploymentState.Bottomed);
        }
        else
        {
            _validGroundContacts.Remove(collision.collider);
        }
    }
}
