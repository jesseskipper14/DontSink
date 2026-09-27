using UnityEngine;

// Behavior-preserving partial-class extraction.
// Responsibility: Presentation.
public sealed partial class TetherDeploymentModule
{
    private void RefreshStoredPayloadVisual()
    {
        if (keepStoredPayloadPhysical)
        {
            SuppressStoredPayloadVisual();
            return;
        }

        ItemInstance storedItem =
            StoredPayload;

        if (storedItem == null ||
            storedItem.Definition == null)
        {
            SuppressStoredPayloadVisual();
            return;
        }

        EnsureStoredPayloadVisualRenderer();

        if (storedPayloadVisualRenderer == null)
            return;

        ItemDefinition definition =
            storedItem.Definition;

        SpriteRenderer sourceRenderer =
            definition.WorldPrefab != null
                ? definition.WorldPrefab.GetComponentInChildren<SpriteRenderer>(true)
                : null;

        Sprite sprite =
            sourceRenderer != null
                ? sourceRenderer.sprite
                : definition.Icon;

        if (sprite == null)
        {
            SuppressStoredPayloadVisual();
            return;
        }

        storedPayloadVisualRenderer.sprite =
            sprite;

        if (sourceRenderer != null)
        {
            storedPayloadVisualRenderer.color =
                sourceRenderer.color;

            storedPayloadVisualRenderer.flipX =
                sourceRenderer.flipX;

            storedPayloadVisualRenderer.flipY =
                sourceRenderer.flipY;

            storedPayloadVisualRenderer.sharedMaterial =
                sourceRenderer.sharedMaterial;
        }

        SpriteRenderer hostRenderer =
            FindHostSortingRenderer();

        if (hostRenderer != null)
        {
            storedPayloadVisualRenderer.sortingLayerID =
                hostRenderer.sortingLayerID;

            storedPayloadVisualRenderer.sortingOrder =
                hostRenderer.sortingOrder +
                1 +
                storedPayloadSortingOrderOffset;
        }
        else if (sourceRenderer != null)
        {
            // Fallback only if the installed Anchor Hole/module has no renderer.
            storedPayloadVisualRenderer.sortingLayerID =
                sourceRenderer.sortingLayerID;

            storedPayloadVisualRenderer.sortingOrder =
                sourceRenderer.sortingOrder +
                1 +
                storedPayloadSortingOrderOffset;
        }

        storedPayloadVisualRenderer.transform.localScale =
            Vector3.one *
            Mathf.Max(
                0.01f,
                storedPayloadVisualScale);

        // This renderer is state-owned by TetherDeploymentModule. Boat-wide
        // visibility systems may toggle Renderer.enabled, but forceRenderingOff
        // remains our authoritative veto while the physical payload is deployed.
        storedPayloadVisualRenderer.forceRenderingOff = false;
        storedPayloadVisualRenderer.enabled = true;
    }
    private void SuppressStoredPayloadVisual()
    {
        if (storedPayloadVisualRenderer == null)
            return;

        // BoatVisualStateController is allowed to toggle Renderer.enabled on
        // exterior installed-module renderers during board/unboard transitions.
        // forceRenderingOff prevents that generic visibility pass from reviving
        // this stored-only proxy while its real WorldItem is deployed.
        storedPayloadVisualRenderer.forceRenderingOff = true;
        storedPayloadVisualRenderer.enabled = false;
    }
    private SpriteRenderer FindHostSortingRenderer()
    {
        // Prefer a renderer on the installed module root itself.
        SpriteRenderer renderer =
            GetComponent<SpriteRenderer>();

        if (renderer != null &&
            renderer != storedPayloadVisualRenderer)
        {
            return renderer;
        }

        // Then walk upward through the installed Anchor Hole/module hierarchy.
        Transform current =
            transform.parent;

        while (current != null)
        {
            renderer =
                current.GetComponent<SpriteRenderer>();

            if (renderer != null &&
                renderer != storedPayloadVisualRenderer)
            {
                return renderer;
            }

            current =
                current.parent;
        }

        // Final host fallback: a renderer somewhere under this installed module.
        SpriteRenderer[] childRenderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < childRenderers.Length; i++)
        {
            renderer =
                childRenderers[i];

            if (renderer != null &&
                renderer != storedPayloadVisualRenderer)
            {
                return renderer;
            }
        }

        return null;
    }
    private void EnsureStoredPayloadVisualRenderer()
    {
        if (storedPayloadVisualRenderer != null)
            return;

        Transform parent =
            PayloadHangPoint;

        GameObject visual =
            new GameObject("StoredPayloadVisual");

        visual.transform.SetParent(
            parent,
            false);

        visual.transform.localPosition =
            Vector3.zero;

        visual.transform.localRotation =
            Quaternion.identity;

        visual.transform.localScale =
            Vector3.one *
            Mathf.Max(
                0.01f,
                storedPayloadVisualScale);

        storedPayloadVisualRenderer =
            visual.AddComponent<SpriteRenderer>();
    }
}
