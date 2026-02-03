using NUnit.Framework.Constraints;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WaterMeshRenderer : MonoBehaviour
{
    [SerializeField] private WaveManager waveManager; // exposed in editor

    private IWaveService wave => waveManager; // interface forwarding

    public int points = 1000;
    public float bottomY = -20f;
    public float textureWorldScale = 10f;
    //public float meshExtraWidth = 100f; // world units to extend beyond camera

    [Header("Mesh Settings")]
    public float meshWidth = 300f;

    Mesh mesh;
    Vector3[] vertices;
    Vector2[] uvs;
    int[] triangles;

    void Awake()
    {
        mesh = new Mesh { name = "Water Mesh" };
        GetComponent<MeshFilter>().mesh = mesh;
    }

    void LateUpdate()
    {
        if (wave == null) return;
        BuildMesh();
    }

    void BuildMesh()
    {
        if (wave == null) return;

        int vertCount = points * 2;

        if (vertices == null || vertices.Length != vertCount)
        {
            vertices = new Vector3[vertCount];
            uvs = new Vector2[vertCount];
            triangles = new int[(points - 1) * 6];
            BuildTriangles(points);
        }

        // Center mesh around camera/player
        float centerX = Camera.main.transform.position.x;
        float startX = centerX - meshWidth * 0.5f;
        float dx = meshWidth / (points - 1);

        for (int i = 0; i < points; i++)
        {
            float x = startX + i * dx;
            float y = wave.SampleHeight(x);

            // Top surface
            vertices[i] = new Vector3(x, y, 0f);
            // Bottom surface
            vertices[i + points] = new Vector3(x, bottomY, 0f);

            float u = x / textureWorldScale;
            uvs[i] = new Vector2(u, 1f);
            uvs[i + points] = new Vector2(u, 0f);
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    void BuildTriangles(int points)
    {
        int t = 0;
        for (int i = 0; i < points - 1; i++)
        {
            int a = i;
            int b = i + 1;
            int c = i + points;
            int d = i + points + 1;

            triangles[t++] = a; triangles[t++] = b; triangles[t++] = d;
            triangles[t++] = a; triangles[t++] = d; triangles[t++] = c;
        }
    }
}
