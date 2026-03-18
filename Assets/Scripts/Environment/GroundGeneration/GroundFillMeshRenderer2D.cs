using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class GroundFillMeshRenderer2D : MonoBehaviour, IGroundFillBottomSource
{
    [Header("Refs")]
    [SerializeField] private MonoBehaviour generatorSource; // must implement IGroundGeneratedNotifier
    [SerializeField] private EdgeCollider2D edge;

    [Header("Fill")]
    public float fillBottomY = -30f;

    [Tooltip("Minimum visual ground thickness below the deepest generated terrain point.")]
    [Min(0f)] public float extraFillDepth = 25f;

    [Header("Visual Resolution")]
    public bool decimate = true;
    [Min(1)] public int decimateStep = 2;

    public float LastUsedBottomY { get; private set; }
    public event Action<float> OnBottomYChanged;

    private IGroundGeneratedNotifier _generator;
    private MeshFilter _meshFilter;
    private Mesh _mesh;
    private Coroutine _pending;

    private Vector3[] _vertices;
    private Vector2[] _uvs;
    private int[] _triangles;

    private void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();

        if (_mesh == null)
        {
            _mesh = new Mesh { name = "Ground Fill Mesh" };
            _mesh.MarkDynamic();
            _meshFilter.sharedMesh = _mesh;
        }

        if (generatorSource == null)
            generatorSource = FindGeneratorSource();

        _generator = generatorSource as IGroundGeneratedNotifier;

        if (edge == null && generatorSource != null)
            edge = generatorSource.GetComponent<EdgeCollider2D>();

        if (edge == null)
            edge = GetComponentInParent<EdgeCollider2D>();
    }

    private void OnEnable()
    {
        if (_generator != null)
            _generator.OnGenerated += HandleGenerated;

        ScheduleRebuild();
    }

    private void OnDisable()
    {
        if (_generator != null)
            _generator.OnGenerated -= HandleGenerated;

        if (_pending != null)
        {
            StopCoroutine(_pending);
            _pending = null;
        }
    }

    private MonoBehaviour FindGeneratorSource()
    {
        MonoBehaviour[] sources = GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] is IGroundGeneratedNotifier)
                return sources[i];
        }

        return null;
    }

    private void HandleGenerated()
    {
        ScheduleRebuild();
    }

    private void ScheduleRebuild()
    {
        if (_pending != null)
            StopCoroutine(_pending);

        _pending = StartCoroutine(RebuildNextFrame());
    }

    private IEnumerator RebuildNextFrame()
    {
        yield return null;
        Rebuild();
        _pending = null;
    }

    [ContextMenu("Rebuild Ground Fill Mesh")]
    public void Rebuild()
    {
        if (_mesh == null)
        {
            _mesh = new Mesh { name = "Ground Fill Mesh" };
            _mesh.MarkDynamic();
            _meshFilter.sharedMesh = _mesh;
        }

        if (edge == null) return;

        var src = edge.points;
        if (src == null || src.Length < 2) return;

        int visualCount = GetVisualPointCount(src.Length);
        if (visualCount < 2) return;

        Vector2[] pts = BuildVisualPoints(src, visualCount);

        float minY = pts[0].y;
        for (int i = 1; i < pts.Length; i++)
            minY = Mathf.Min(minY, pts[i].y);

        float safeBottomY = Mathf.Min(fillBottomY, minY - extraFillDepth);

        LastUsedBottomY = safeBottomY;
        OnBottomYChanged?.Invoke(LastUsedBottomY);

        BuildMesh(pts, safeBottomY);
    }

    private int GetVisualPointCount(int sourceCount)
    {
        if (!decimate || decimateStep <= 1)
            return sourceCount;

        int count = 0;
        for (int i = 0; i < sourceCount; i++)
        {
            if (i % decimateStep == 0 || i == sourceCount - 1)
                count++;
        }

        return count;
    }

    private Vector2[] BuildVisualPoints(Vector2[] src, int visualCount)
    {
        if (!decimate || decimateStep <= 1)
        {
            Vector2[] copy = new Vector2[src.Length];
            Array.Copy(src, copy, src.Length);
            return copy;
        }

        Vector2[] pts = new Vector2[visualCount];
        int write = 0;

        for (int i = 0; i < src.Length; i++)
        {
            if (i % decimateStep != 0 && i != src.Length - 1)
                continue;

            pts[write++] = src[i];
        }

        return pts;
    }

    private void BuildMesh(Vector2[] topPts, float bottomY)
    {
        int count = topPts.Length;
        int vertCount = count * 2;
        int triCount = (count - 1) * 6;

        if (_vertices == null || _vertices.Length != vertCount)
            _vertices = new Vector3[vertCount];

        if (_uvs == null || _uvs.Length != vertCount)
            _uvs = new Vector2[vertCount];

        if (_triangles == null || _triangles.Length != triCount)
            _triangles = new int[triCount];

        float minX = topPts[0].x;
        float maxX = topPts[count - 1].x;
        float width = Mathf.Max(0.001f, maxX - minX);
        float height = Mathf.Max(0.001f, Mathf.Abs(topPts[0].y - bottomY));

        for (int i = 0; i < count; i++)
        {
            Vector2 p = topPts[i];

            _vertices[i] = new Vector3(p.x, p.y, 0f);
            _vertices[i + count] = new Vector3(p.x, bottomY, 0f);

            float u = Mathf.InverseLerp(minX, maxX, p.x);
            _uvs[i] = new Vector2(u, 1f);
            _uvs[i + count] = new Vector2(u, 0f);
        }

        int t = 0;
        for (int i = 0; i < count - 1; i++)
        {
            int a = i;
            int b = i + 1;
            int c = i + count;
            int d = i + count + 1;

            _triangles[t++] = a;
            _triangles[t++] = b;
            _triangles[t++] = d;

            _triangles[t++] = a;
            _triangles[t++] = d;
            _triangles[t++] = c;
        }

        _mesh.Clear();
        _mesh.vertices = _vertices;
        _mesh.uv = _uvs;
        _mesh.triangles = _triangles;
        _mesh.RecalculateBounds();
    }
}