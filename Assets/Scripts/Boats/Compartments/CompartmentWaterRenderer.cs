using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CompartmentWaterRenderer : MonoBehaviour
{
    private Mesh mesh;
    private Compartment compartment;

    void Awake()
    {
        compartment = GetComponent<Compartment>();

        mesh = new Mesh();
        mesh.name = "WaterMesh";
        GetComponent<MeshFilter>().mesh = mesh;

        var mr = GetComponent<MeshRenderer>();
        mr.material = new Material(Shader.Find("Sprites/Default"))
        {
            color = new Color(0.1f, 0.4f, 0.9f, 0.6f)
        };
    }

    void LateUpdate()
    {
        UpdateWaterMesh();
    }

    void UpdateWaterMesh()
    {
        if (compartment.WaterArea <= 0f)
        {
            mesh.Clear();
            return;
        }

        Vector2[] worldPoly = compartment.GetWorldCorners();

        // Gravity-aligned water plane
        Vector2 gravityDir = Physics2D.gravity.normalized;
        Vector2 planeNormal = -gravityDir;

        // Solve for plane offset from area
        float planeOffset =
            compartment.SolveSurfaceOffsetFromArea(compartment.WaterArea);

        // Clip against plane
        List<Vector2> clipped =
            ConvexPolygonUtil.ClipBelowPlane(
                worldPoly,
                planeNormal,
                planeOffset);

        if (clipped.Count < 3)
        {
            mesh.Clear();
            return;
        }

        // Convert to local space
        Vector3[] vertices = new Vector3[clipped.Count];
        for (int i = 0; i < clipped.Count; i++)
            vertices[i] = transform.InverseTransformPoint(clipped[i]);

        // Fan triangulation (convex polygon)
        List<int> tris = new();
        for (int i = 1; i < vertices.Length - 1; i++)
        {
            tris.Add(0);
            tris.Add(i);
            tris.Add(i + 1);
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
    }
}
