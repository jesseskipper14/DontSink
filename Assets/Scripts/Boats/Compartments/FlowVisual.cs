using UnityEngine;

public class FlowVisual : MonoBehaviour
{
    [SerializeField] Renderer rend;
    [SerializeField] float scrollSpeed = 1f;
    [SerializeField] float minVisibleFlow = 0.0001f;

    MaterialPropertyBlock mpb;
    float intensity;

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
        rend.enabled = false;
    }

    public void SetFlow(Compartment source, Compartment target, float flow)
    {
        if (flow <= minVisibleFlow)
        {
            rend.enabled = false;
            return;
        }

        // Enable visual
        rend.enabled = true;

        // --- Parent to target compartment ---
        transform.SetParent(target.transform, worldPositionStays: false);

        // --- Position near the opening on the target side ---
        Vector3 localPos = Vector3.zero;

        // Use the connection midpoint along X and the bottom of the opening along Y
        if (target != null && source != null)
        {
            // Compute the midpoint of the opening in target local space
            float midX = (target.transform.InverseTransformPoint(source.transform.position).x
                          + 0f) * 0.5f; // simplified, refine if you have actual connection points
            float y = 0.5f * target.Height; // just a placeholder, align visually with water surface
            localPos = new Vector3(midX, y, 0f);
        }

        transform.localPosition = localPos;

        // --- Animate texture in flow direction ---
        float dir = (source == target) ? 1f : -1f;
        intensity = Mathf.Lerp(intensity, flow, 0.2f);

        mpb.SetVector(
            "_MainTex_ST",
            new Vector4(dir, 1f, Time.time * scrollSpeed * dir, 0f)
        );

        rend.SetPropertyBlock(mpb);
    }

}
