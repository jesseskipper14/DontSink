using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CompartmentWaterRenderer : MonoBehaviour
{
    private Mesh mesh;
    private Compartment compartment;

    [Header("Rendering")]
    [SerializeField] private string sortingLayerName = "BoatView";
    [SerializeField] private int sortingOrder = 25;
    [SerializeField] private Color waterColor = new Color(0.1f, 0.4f, 0.9f, 0.6f);
    [SerializeField] private Material waterMaterial;

    void Awake()
    {
        compartment = GetComponent<Compartment>();

        mesh = new Mesh();
        mesh.name = "WaterMesh";
        GetComponent<MeshFilter>().mesh = mesh;

        ApplyRendererSettings();
    }

    private void ApplyRendererSettings()
    {
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr == null)
            return;

        if (waterMaterial != null)
        {
            mr.sharedMaterial = waterMaterial;
        }
        else
        {
            Material mat = new Material(Shader.Find("Sprites/Default"))
            {
                color = waterColor
            };

            mr.sharedMaterial = mat;
        }

        if (!string.IsNullOrWhiteSpace(sortingLayerName))
            mr.sortingLayerName = sortingLayerName;

        mr.sortingOrder = sortingOrder;
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

        Vector2[] waterPoly = compartment.GetWaterPolygonWorld();

        List<Vector2> clipped = waterPoly != null
            ? new List<Vector2>(waterPoly)
            : new List<Vector2>();

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

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyRendererSettings();
    }
#endif
}
