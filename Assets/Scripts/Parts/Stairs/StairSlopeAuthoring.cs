using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Authoring helper for scalable stair slopes.
/// Syncs ResizableSegment2D with a triangle PolygonCollider2D.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class StairSlopeAuthoring : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ResizableSegment2D resizableSegment;
    [SerializeField] private PolygonCollider2D polygonCollider;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField, HideInInspector] private Mesh generatedInstanceMesh;

    [SerializeField] private bool generateTriangleMeshVisual = true;

    [Header("Direction")]
    [Tooltip("If true, slope rises from left → right. Otherwise right → left.")]
    [SerializeField] private bool ascendRight = true;

    [Header("Behavior")]
    [SerializeField] private bool allowResizableSegmentToDriveSize = true;

    [Header("Optional Top Ledge")]
    [SerializeField] private BoxCollider2D topLedgeCollider;
    [SerializeField] private bool autoSizeTopLedge = true;
    [SerializeField] private SpriteRenderer topLedgeSpriteRenderer;
    [SerializeField, Min(0.01f)]
    private float topLedgeHeight = 0.15f;

    [Header("Optional Access Trigger")]
    [SerializeField] private BoxCollider2D accessTriggerCollider;
    [SerializeField] private bool autoSizeAccessTrigger = true;
    [SerializeField, Min(0f)] private float accessTriggerPaddingX = 0.5f;
    [SerializeField, Min(0f)] private float accessTriggerPaddingY = 0.5f;

    [SerializeField] private MeshRenderer meshRenderer;

    [Header("Optional Stair Visual")]
    [SerializeField] private bool autoDriveStairMaterial = true;
    [SerializeField, Min(1)] private int minVisualSteps = 3;
    [SerializeField, Min(0.1f)] private float visualStepHeight = 0.5f;

    private static readonly int StepCountId = Shader.PropertyToID("_StepCount");
    private static readonly int AscendRightId = Shader.PropertyToID("_AscendRight");

    private MaterialPropertyBlock _materialBlock;

    private float _lastWidth;
    private float _lastHeight;
    private bool _lastAscendRight;

    private bool _applying;

    public float Run => resizableSegment != null ? resizableSegment.Width : 1f;
    public float Rise => resizableSegment != null ? resizableSegment.Height : 1f;

    public bool AscendRight => ascendRight;

    private void Reset()
    {
        ResolveRefs();
        Apply();
    }

    private void OnEnable()
    {
        ResolveRefs();
        Apply();
    }

    private void OnValidate()
    {
        ResolveRefs();
        Apply();
    }

    private void Update()
    {
        if (_applying)
            return;

        ResolveRefs();

        if (resizableSegment != null && allowResizableSegmentToDriveSize)
        {
            bool sizeChanged =
                !Mathf.Approximately(_lastWidth, resizableSegment.Width) ||
                !Mathf.Approximately(_lastHeight, resizableSegment.Height);

            if (sizeChanged)
            {
                Apply();
                return;
            }
        }

        if (_lastAscendRight != ascendRight)
        {
            Apply();
        }
    }

    private void ResolveRefs()
    {
        if (resizableSegment == null)
            resizableSegment = GetComponent<ResizableSegment2D>();

        if (polygonCollider == null)
            polygonCollider = GetComponent<PolygonCollider2D>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();

        if (topLedgeCollider == null)
        {
            HatchLedge ledge = GetComponentInChildren<HatchLedge>(true);
            if (ledge != null)
                topLedgeCollider = ledge.Collider as BoxCollider2D;
        }

        if (topLedgeSpriteRenderer == null && topLedgeCollider != null)
            topLedgeSpriteRenderer = topLedgeCollider.GetComponentInChildren<SpriteRenderer>(true);

        if (accessTriggerCollider == null)
        {
            StairAccessTriggerRelay relay = GetComponentInChildren<StairAccessTriggerRelay>(true);
            if (relay != null)
                accessTriggerCollider = relay.GetComponent<BoxCollider2D>();
        }

        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();
    }

    public void Apply()
    {
        if (_applying)
            return;

        _applying = true;

        ResolveRefs();

        float width = Mathf.Max(0.01f, Run);
        float height = Mathf.Max(0.01f, Rise);

        ApplyTriangleCollider(width, height);
        ApplyTopLedge(width, height);
        ApplyAccessTrigger(width, height);

        if (generateTriangleMeshVisual)
            ApplyTriangleMesh(width, height);

        ApplyStairMaterial(width, height);

        //ApplyVisual(width, height);

        _lastWidth = width;
        _lastHeight = height;
        _lastAscendRight = ascendRight;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (polygonCollider != null) EditorUtility.SetDirty(polygonCollider);
            if (spriteRenderer != null) EditorUtility.SetDirty(spriteRenderer);
            if (topLedgeCollider != null) EditorUtility.SetDirty(topLedgeCollider);
            if (topLedgeSpriteRenderer != null) EditorUtility.SetDirty(topLedgeSpriteRenderer);
            if (accessTriggerCollider != null) EditorUtility.SetDirty(accessTriggerCollider);
            EditorUtility.SetDirty(this);
        }
#endif

        _applying = false;
    }

    private void ApplyTopLedge(float width, float height)
    {
        if (!autoSizeTopLedge || topLedgeCollider == null)
            return;

        float hh = height * 0.5f;
        float ledgeCenterY = hh;

        Transform ledgeTransform = topLedgeCollider.transform;

        ledgeTransform.localPosition = new Vector3(0f, ledgeCenterY, 0f);
        ledgeTransform.localRotation = Quaternion.identity;
        ledgeTransform.localScale = Vector3.one;

        topLedgeCollider.isTrigger = false;
        topLedgeCollider.offset = Vector2.zero;
        topLedgeCollider.size = new Vector2(width, topLedgeHeight);

        if (topLedgeSpriteRenderer != null)
        {
            Transform srTransform = topLedgeSpriteRenderer.transform;

            if (srTransform != ledgeTransform)
            {
                srTransform.localPosition = Vector3.zero;
                srTransform.localRotation = Quaternion.identity;
                srTransform.localScale = Vector3.one;
            }

            if (topLedgeSpriteRenderer.drawMode == SpriteDrawMode.Simple)
                topLedgeSpriteRenderer.drawMode = SpriteDrawMode.Tiled;

            if (topLedgeSpriteRenderer.drawMode != SpriteDrawMode.Simple)
                topLedgeSpriteRenderer.size = new Vector2(width, topLedgeHeight);
        }
    }

    private void ApplyAccessTrigger(float width, float height)
    {
        if (!autoSizeAccessTrigger || accessTriggerCollider == null)
            return;

        float triggerWidth = width + accessTriggerPaddingX * 2f;
        float triggerHeight = height + accessTriggerPaddingY * 2f;

        Transform triggerTransform = accessTriggerCollider.transform;

        SetChildPoseFromStairLocal(
            triggerTransform,
            Vector3.zero,
            Quaternion.identity);

        triggerTransform.localScale = Vector3.one;

        accessTriggerCollider.isTrigger = true;
        accessTriggerCollider.offset = Vector2.zero;
        accessTriggerCollider.size = new Vector2(triggerWidth, triggerHeight);
    }

    private void SetChildPoseFromStairLocal(
    Transform child,
    Vector3 stairLocalPosition,
    Quaternion stairLocalRotation)
    {
        if (child == null)
            return;

        Vector3 worldPos = transform.TransformPoint(stairLocalPosition);
        Quaternion worldRot = transform.rotation * stairLocalRotation;

        child.position = worldPos;
        child.rotation = worldRot;
    }

    private void ApplyTriangleCollider(float width, float height)
    {
        if (polygonCollider == null)
            return;

        float hw = width * 0.5f;
        float hh = height * 0.5f;

        Vector2[] points = ascendRight
            ? new[]
            {
            new Vector2(-hw, -hh), // bottom-left
            new Vector2( hw, -hh), // bottom-right
            new Vector2( hw,  hh), // top-right
            }
            : new[]
            {
            new Vector2( hw, -hh), // bottom-right
            new Vector2(-hw, -hh), // bottom-left
            new Vector2(-hw,  hh), // top-left
            };

        polygonCollider.pathCount = 1;
        polygonCollider.SetPath(0, points);
    }

    private void ApplyTriangleMesh(float width, float height)
    {
        if (meshFilter == null)
            return;

        float hw = width * 0.5f;
        float hh = height * 0.5f;

        Vector3[] verts;
        Vector2[] uvs;

        if (ascendRight)
        {
            verts = new[]
            {
            new Vector3(-hw, -hh, 0f), // bottom-left
            new Vector3( hw, -hh, 0f), // bottom-right
            new Vector3( hw,  hh, 0f), // top-right
        };

            uvs = new[]
            {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
        };
        }
        else
        {
            verts = new[]
            {
            new Vector3( hw, -hh, 0f), // bottom-right
            new Vector3(-hw, -hh, 0f), // bottom-left
            new Vector3(-hw,  hh, 0f), // top-left
        };

            uvs = new[]
            {
            new Vector2(1f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
        };
        }

        Mesh mesh = GetOrCreateOwnedMesh();
        if (mesh == null)
            return;

        mesh.Clear();
        mesh.vertices = verts;
        mesh.triangles = new[] { 0, 1, 2 };
        mesh.uv = uvs;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
    }

    private void ApplyStairMaterial(float width, float height)
    {
        if (!autoDriveStairMaterial || meshRenderer == null)
            return;

        if (_materialBlock == null)
            _materialBlock = new MaterialPropertyBlock();

        int visualSteps = Mathf.Max(
            minVisualSteps,
            Mathf.RoundToInt(height / Mathf.Max(0.01f, visualStepHeight)));

        meshRenderer.GetPropertyBlock(_materialBlock);
        _materialBlock.SetFloat(StepCountId, visualSteps);
        _materialBlock.SetFloat(AscendRightId, ascendRight ? 1f : 0f);
        meshRenderer.SetPropertyBlock(_materialBlock);
    }

    private Mesh GetOrCreateOwnedMesh()
    {
        if (meshFilter == null)
            return null;

        if (generatedInstanceMesh == null)
        {
            generatedInstanceMesh = new Mesh
            {
                name = $"Generated Stair Triangle ({gameObject.name})"
            };

            meshFilter.sharedMesh = generatedInstanceMesh;
        }
        else if (meshFilter.sharedMesh != generatedInstanceMesh)
        {
            meshFilter.sharedMesh = generatedInstanceMesh;
        }

        return generatedInstanceMesh;
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && generatedInstanceMesh != null)
            DestroyImmediate(generatedInstanceMesh);
        else
#endif
        if (generatedInstanceMesh != null)
            Destroy(generatedInstanceMesh);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        float width = Mathf.Max(0.01f, Run);
        float height = Mathf.Max(0.01f, Rise);

        float hw = width * 0.5f;
        float hh = height * 0.5f;

        Gizmos.matrix = transform.localToWorldMatrix;

        Vector3 a;
        Vector3 b;
        Vector3 c;

        if (ascendRight)
        {
            a = new Vector3(-hw, -hh, 0f); // bottom-left
            b = new Vector3(hw, -hh, 0f); // bottom-right
            c = new Vector3(hw, hh, 0f); // top-right
        }
        else
        {
            a = new Vector3(hw, -hh, 0f); // bottom-right
            b = new Vector3(-hw, -hh, 0f); // bottom-left
            c = new Vector3(-hw, hh, 0f); // top-left
        }

        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.4f);
        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, a);
    }
#endif
}