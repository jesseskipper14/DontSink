using UnityEngine;

[DisallowMultipleComponent]
public sealed class TetherRopeFlowVisual : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TetherConstraint2D tetherConstraint;
    [SerializeField] private LineRenderer lineRenderer;

    [Header("Flow")]
    [Tooltip("How many visible rope-pattern cycles pass the tether exit per meter of deployed rope.")]
    [SerializeField, Min(0.01f)] private float flowCyclesPerMeter = 1f;

    [Tooltip("Flip this if the visible rope pattern moves toward the tether exit while lowering.")]
    [SerializeField] private bool reverseFlowDirection;

    [Tooltip("Keep the LineRenderer in Tile mode so its procedural pattern density is based on line length rather than stretching one UV span over the entire tether.")]
    [SerializeField] private bool forceTileTextureMode = true;

    [Header("Runtime Debug")]
    [SerializeField] private bool shaderSupportsFlow;
    [SerializeField] private float deployedLength;
    [SerializeField] private float appliedFlowOffset;

    private static readonly int FlowOffsetId =
        Shader.PropertyToID(
            "_FlowOffset");

    private MaterialPropertyBlock _propertyBlock;

    private void Awake()
    {
        CacheRefs();
        EnsurePropertyBlock();
        RefreshShaderSupport();
    }

    private void OnEnable()
    {
        CacheRefs();
        EnsurePropertyBlock();
        RefreshShaderSupport();
        ApplyFlow();
    }

    private void LateUpdate()
    {
        ApplyFlow();
    }

    private void OnValidate()
    {
        flowCyclesPerMeter =
            Mathf.Max(
                0.01f,
                flowCyclesPerMeter);
    }

    private void CacheRefs()
    {
        if (tetherConstraint == null)
            tetherConstraint =
                GetComponent<TetherConstraint2D>();

        if (lineRenderer == null)
            lineRenderer =
                GetComponent<LineRenderer>();

        if (lineRenderer == null)
            lineRenderer =
                GetComponentInChildren<LineRenderer>(true);

        if (forceTileTextureMode &&
            lineRenderer != null)
        {
            lineRenderer.textureMode =
                LineTextureMode.Tile;
        }
    }

    private void EnsurePropertyBlock()
    {
        if (_propertyBlock == null)
            _propertyBlock =
                new MaterialPropertyBlock();
    }

    private void RefreshShaderSupport()
    {
        shaderSupportsFlow =
            lineRenderer != null &&
            lineRenderer.sharedMaterial != null &&
            lineRenderer.sharedMaterial.HasProperty(
                FlowOffsetId);
    }

    private void ApplyFlow()
    {
        if (tetherConstraint == null ||
            lineRenderer == null)
        {
            CacheRefs();
        }

        if (tetherConstraint == null ||
            lineRenderer == null)
        {
            shaderSupportsFlow = false;
            return;
        }

        if (forceTileTextureMode)
            lineRenderer.textureMode =
                LineTextureMode.Tile;

        Material material =
            lineRenderer.sharedMaterial;

        shaderSupportsFlow =
            material != null &&
            material.HasProperty(
                FlowOffsetId);

        if (!shaderSupportsFlow)
            return;

        deployedLength =
            tetherConstraint.DeployedLength;

        float direction =
            reverseFlowDirection
                ? -1f
                : 1f;

        // Deployed length is the authority, not payload distance.
        // Therefore the visible rope continues flowing if the anchor is
        // bottomed but the winch keeps paying out line.
        appliedFlowOffset =
            Mathf.Repeat(
                direction *
                deployedLength *
                Mathf.Max(
                    0.01f,
                    flowCyclesPerMeter),
                1f);

        EnsurePropertyBlock();

        lineRenderer.GetPropertyBlock(
            _propertyBlock);

        _propertyBlock.SetFloat(
            FlowOffsetId,
            appliedFlowOffset);

        lineRenderer.SetPropertyBlock(
            _propertyBlock);
    }
}
