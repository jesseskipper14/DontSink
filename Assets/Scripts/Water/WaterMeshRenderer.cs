using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class WaterMeshRenderer : MonoBehaviour
{
    [SerializeField] private WaveManager waveManager;
    private IWaveService wave => waveManager;

    [Header("Centering")]
    [Tooltip("What to center the water mesh around. If null, falls back to Camera.main.")]
    [SerializeField] private Transform centerTarget;

    [Header("Mesh Settings")]
    [Min(2)] public int points = 1000;
    public float bottomY = -20f;
    public float textureWorldScale = 10f;
    public float meshWidth = 300f;

    private Mesh _mesh;
    private MeshFilter _meshFilter;

    private Vector3[] _vertices;
    private Vector2[] _uvs;
    private Vector2[] _uv2s;
    private int[] _triangles;

    private int _lastPoints = -1;

    private void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();

        _mesh = new Mesh
        {
            name = "Water Mesh"
        };
        _mesh.MarkDynamic();

        _meshFilter.sharedMesh = _mesh;

        ResolveRefs();
        EnsureMeshData();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ResolveRefs();
    }

    private void Start()
    {
        ResolveRefs();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveRefs();
    }

    private void ResolveRefs()
    {
        if (ServiceRoot.Instance != null && ServiceRoot.Instance.WaveManager != null)
        {
            waveManager = ServiceRoot.Instance.WaveManager;
            return;
        }

        if (waveManager == null)
            waveManager = FindFirstObjectByType<WaveManager>();
    }

    private void LateUpdate()
    {
        if (waveManager == null)
            ResolveRefs();

        if (wave == null) return;
        if (!TryGetCenterX(out float centerX)) return;

        EnsureMeshData();
        UpdateMesh(centerX);
    }

    private bool TryGetCenterX(out float centerX)
    {
        if (centerTarget != null)
        {
            centerX = centerTarget.position.x;
            return true;
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            centerX = cam.transform.position.x;
            return true;
        }

        centerX = 0f;
        return false;
    }

    private void EnsureMeshData()
    {
        if (points < 2) points = 2;

        if (_mesh == null)
        {
            _mesh = new Mesh { name = "Water Mesh" };
            _mesh.MarkDynamic();

            if (_meshFilter == null)
                _meshFilter = GetComponent<MeshFilter>();

            _meshFilter.sharedMesh = _mesh;
        }

        if (_lastPoints == points &&
            _vertices != null &&
            _uvs != null &&
            _uv2s != null &&
            _triangles != null)
        {
            return;
        }

        _lastPoints = points;

        int vertCount = points * 2;
        _vertices = new Vector3[vertCount];
        _uvs = new Vector2[vertCount];
        _uv2s = new Vector2[vertCount];
        _triangles = new int[(points - 1) * 6];

        BuildTriangles();

        _mesh.Clear();
        _mesh.vertices = _vertices;
        _mesh.uv = _uvs;
        _mesh.uv2 = _uv2s;
        _mesh.triangles = _triangles;
        _mesh.RecalculateBounds();
    }

    private void UpdateMesh(float centerX)
    {
        float startX = centerX - meshWidth * 0.5f;
        float dx = meshWidth / (points - 1);
        float safeScale = Mathf.Approximately(textureWorldScale, 0f) ? 1f : textureWorldScale;

        for (int i = 0; i < points; i++)
        {
            float worldX = startX + i * dx;
            float surfaceY = waveManager.SampleSurfaceY(worldX);

            _vertices[i] = new Vector3(worldX, surfaceY, 0f);
            _vertices[i + points] = new Vector3(worldX, bottomY, 0f);

            float u = worldX / safeScale;
            _uvs[i] = new Vector2(u, 1f);
            _uvs[i + points] = new Vector2(u, 0f);

            // Critical: both verts in a column carry the SAME local surface Y.
            _uv2s[i] = new Vector2(surfaceY, 0f);
            _uv2s[i + points] = new Vector2(surfaceY, 0f);
        }

        _mesh.vertices = _vertices;
        _mesh.uv = _uvs;
        _mesh.uv2 = _uv2s;
        _mesh.RecalculateBounds();
    }

    private void BuildTriangles()
    {
        int t = 0;

        for (int i = 0; i < points - 1; i++)
        {
            int a = i;
            int b = i + 1;
            int c = i + points;
            int d = i + points + 1;

            _triangles[t++] = a;
            _triangles[t++] = b;
            _triangles[t++] = d;

            _triangles[t++] = a;
            _triangles[t++] = d;
            _triangles[t++] = c;
        }
    }
}