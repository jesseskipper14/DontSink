using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Opt-in authoring profile for complex tether payloads.
///
/// A simple anchor can omit this component and keep legacy behavior.
/// A diving bell/cage should add it so ONLY external payload collision geometry
/// changes to the TetherPayload layer when deployed.
///
/// IMPORTANT:
/// Unity layers live on GameObjects, not individual Collider2D components.
/// Keep external tether colliders on dedicated child GameObjects. Do not place a
/// BellInterior/BellLedge collider on the same GameObject as a selected external
/// tether collider.
/// </summary>
[DisallowMultipleComponent]
public sealed class TetherPayloadCollisionScope :
    MonoBehaviour
{
    [Header("External Tether Collision Geometry")]
    [Tooltip(
        "Every Collider2D under these roots is treated as external payload geometry " +
        "and moves to the TetherPayload layer while deployed.")]
    [SerializeField] private Transform[] collisionRoots;

    [Tooltip(
        "Optional individual external colliders in addition to Collision Roots.")]
    [SerializeField] private Collider2D[] explicitColliders;

    [SerializeField] private bool includeTriggerColliders = false;

    [Header("Debug")]
    [SerializeField] private int lastCollectedColliderCount;

    public void CollectTetherCollisionColliders(
        List<Collider2D> result)
    {
        if (result == null)
            return;

        result.Clear();

        if (collisionRoots != null)
        {
            for (int i = 0;
                 i < collisionRoots.Length;
                 i++)
            {
                Transform root =
                    collisionRoots[i];

                if (root == null)
                    continue;

                Collider2D[] found =
                    root.GetComponentsInChildren<Collider2D>(
                        true);

                AddUniqueUsable(
                    found,
                    result);
            }
        }

        if (explicitColliders != null)
        {
            for (int i = 0;
                 i < explicitColliders.Length;
                 i++)
            {
                Collider2D collider =
                    explicitColliders[i];

                if (!IsUsable(
                        collider))
                {
                    continue;
                }

                if (!result.Contains(
                        collider))
                {
                    result.Add(
                        collider);
                }
            }
        }

        lastCollectedColliderCount =
            result.Count;
    }

    private void AddUniqueUsable(
        Collider2D[] source,
        List<Collider2D> result)
    {
        if (source == null ||
            result == null)
        {
            return;
        }

        for (int i = 0;
             i < source.Length;
             i++)
        {
            Collider2D collider =
                source[i];

            if (!IsUsable(
                    collider))
            {
                continue;
            }

            if (!result.Contains(
                    collider))
            {
                result.Add(
                    collider);
            }
        }
    }

    private bool IsUsable(
        Collider2D collider)
    {
        if (collider == null)
            return false;

        if (!includeTriggerColliders &&
            collider.isTrigger)
        {
            return false;
        }

        return true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        List<Collider2D> preview =
            new List<Collider2D>();

        CollectTetherCollisionColliders(
            preview);

        Gizmos.color =
            new Color(
                1f,
                0.55f,
                0.1f,
                0.9f);

        for (int i = 0;
             i < preview.Count;
             i++)
        {
            Collider2D collider =
                preview[i];

            if (collider == null)
                continue;

            Bounds bounds =
                collider.bounds;

            Gizmos.DrawWireCube(
                bounds.center,
                bounds.size);
        }
    }
#endif
}
