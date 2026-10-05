using TMPro;
using UnityEngine;

/// <summary>Self-lit physical depth gauge; depth is measured from the exterior ocean.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TextMeshPro))]
public sealed class DivingBellDepthDisplay : MonoBehaviour
{
    [Tooltip("Optional measurement point. Blank uses the bell root. Assign the bottom opening to measure there.")]
    [SerializeField] private Transform depthPoint;
    [SerializeField] private DivingBellAirVolume airVolume;
    [SerializeField] private DivingBellVisualPresentation presentation;
    [SerializeField] private Color displayColor = new Color(.35f, 1f, .7f, 1f);
    [Tooltip("Order relative to bell interior artwork. Keep below exterior/foreground content.")]
    [SerializeField] private int interiorSortingOrderOffset = 1;
    public float DepthMeters { get; private set; }
    private TextMeshPro label;
    private Renderer labelRenderer;
    private Material originalMaterial;
    private Material unlitMaterial;

    private void Awake()
    {
        label = GetComponent<TextMeshPro>();
        labelRenderer = GetComponent<Renderer>();
        if (airVolume == null) airVolume = GetComponentInParent<DivingBellAirVolume>();
        if (presentation == null) presentation = GetComponentInParent<DivingBellVisualPresentation>();
        // Clone only this label's material: never edit a shared font material.
        originalMaterial = label.fontSharedMaterial;
        var shader = Shader.Find("TextMeshPro/Distance Field");
        if (originalMaterial != null && shader != null)
        {
            unlitMaterial = new Material(originalMaterial) { shader = shader, name = "Bell Depth Display (runtime)" };
            unlitMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            unlitMaterial.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
            label.fontSharedMaterial = unlitMaterial;
        }
        Refresh();
    }

    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        if (label == null) return;
        var anchor = depthPoint != null ? depthPoint : airVolume != null ? airVolume.transform : transform;
        if (anchor == null) anchor = transform;
        var waves = ServiceRoot.Instance?.WaveManager;
        if (waves == null)
        {
            label.text = "DEPTH -- m";
        }
        else
        {
            DepthMeters = Mathf.Max(0f, waves.SampleSurfaceY(anchor.position.x) - anchor.position.y);
            label.SetText("DEPTH {0:1} m", DepthMeters);
        }
        label.color = displayColor;
        if (labelRenderer != null && presentation != null &&
            presentation.TryGetInteriorSortingContext(out int layer, out int order))
        {
            labelRenderer.sortingLayerID = layer;
            labelRenderer.sortingOrder = order + interiorSortingOrderOffset;
        }
    }

    private void OnDestroy()
    {
        if (unlitMaterial == null) return;
        if (label != null && label.fontSharedMaterial == unlitMaterial) label.fontSharedMaterial = originalMaterial;
        Destroy(unlitMaterial);
    }
}
