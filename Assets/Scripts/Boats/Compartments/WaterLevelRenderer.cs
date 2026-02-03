using UnityEngine;

[RequireComponent(typeof(Compartment))]
public class WaterLevelRenderer : MonoBehaviour
{
    public Color waterColor = new Color(0f, 0.5f, 1f, 0.5f);
    public bool debugDrawGizmos = true;

    private Compartment compartment;

    private void Awake()
    {
        compartment = GetComponent<Compartment>();
    }

    private void OnDrawGizmos()
    {
        if (!debugDrawGizmos || compartment == null)
            return;

        // Fraction of compartment filled
        float fillFraction = Mathf.Clamp01(compartment.WaterArea / compartment.MaxWaterArea);

        Vector3 bl = compartment.transform.TransformPoint(compartment.p3);
        Vector3 br = compartment.transform.TransformPoint(compartment.p2);
        Vector3 tl = compartment.transform.TransformPoint(compartment.p0);
        Vector3 tr = compartment.transform.TransformPoint(compartment.p1);

        // Then interpolate top edge based on fillFraction
        Vector3 waterTopLeft = Vector3.Lerp(bl, tl, fillFraction);
        Vector3 waterTopRight = Vector3.Lerp(br, tr, fillFraction);

        // Draw polygon
        Gizmos.color = waterColor;
        Gizmos.DrawLine(bl, br);
        Gizmos.DrawLine(br, waterTopRight);
        Gizmos.DrawLine(waterTopRight, waterTopLeft);
        Gizmos.DrawLine(waterTopLeft, bl);

    }
}
