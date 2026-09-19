using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple seabed-contact sensor for a physical sounding weight.
/// Add this beside TetherPayload on the sounding-weight WorldItem prefab.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class SoundingWeightBottomContact2D :
    MonoBehaviour,
    ITetherBottomContactProvider
{
    [Header("Bottom Contact")]
    [SerializeField] private LayerMask validSeafloorLayers = ~0;

    [Tooltip(
        "Minimum upward component of a collision normal that counts as seabed. " +
        "Keep above 0 so a sheer wall does not count as bottom.")]
    [SerializeField, Range(0.01f, 1f)]
    private float minimumGroundNormalY = 0.15f;

    [Header("Runtime Debug")]
    [SerializeField] private int validContactCount;

    private readonly HashSet<Collider2D> _validContacts =
        new HashSet<Collider2D>();

    public bool IsOnBottom =>
        _validContacts.Count > 0;

    private void OnCollisionEnter2D(
        Collision2D collision)
    {
        EvaluateContact(collision);
    }

    private void OnCollisionStay2D(
        Collision2D collision)
    {
        EvaluateContact(collision);
    }

    private void OnCollisionExit2D(
        Collision2D collision)
    {
        if (collision != null &&
            collision.collider != null)
        {
            _validContacts.Remove(
                collision.collider);
        }

        RefreshDebugCount();
    }

    private void OnDisable()
    {
        _validContacts.Clear();
        RefreshDebugCount();
    }

    private void EvaluateContact(
        Collision2D collision)
    {
        if (collision == null ||
            collision.collider == null)
        {
            return;
        }

        Collider2D other =
            collision.collider;

        int layerBit =
            1 << other.gameObject.layer;

        if ((validSeafloorLayers.value &
             layerBit) == 0)
        {
            _validContacts.Remove(other);
            RefreshDebugCount();
            return;
        }

        bool validNormal =
            false;

        for (int i = 0;
             i < collision.contactCount;
             i++)
        {
            ContactPoint2D contact =
                collision.GetContact(i);

            if (contact.normal.y >=
                minimumGroundNormalY)
            {
                validNormal = true;
                break;
            }
        }

        if (validNormal)
            _validContacts.Add(other);
        else
            _validContacts.Remove(other);

        RefreshDebugCount();
    }

    private void RefreshDebugCount()
    {
        validContactCount =
            _validContacts.Count;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        minimumGroundNormalY =
            Mathf.Clamp(
                minimumGroundNormalY,
                0.01f,
                1f);
    }
#endif
}
