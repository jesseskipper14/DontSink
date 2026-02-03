using UnityEngine;

public class CloudShaderRenderer : MonoBehaviour
{
    [Header("Service Source")]
    [SerializeField] private MonoBehaviour cloudSource;

    [Header("Material")]
    [SerializeField] private Material cloudMaterial;

    private ICloudService cloud;

    private static readonly int CoverageID = Shader.PropertyToID("_Coverage");

    private void Awake()
    {
        cloud = cloudSource as ICloudService;

        if (cloud == null)
        {
            Debug.LogError($"{name}: cloudSource does not implement ICloudService");
            return;
        }

        if (cloudMaterial == null)
        {
            Debug.LogError($"{name}: Cloud material not assigned");
            return;
        }

        cloud.OnCloudCoverageChanged += UpdateCoverage;

        // Initial sync
        UpdateCoverage(cloud.CloudCoverage);
    }

    private void OnDestroy()
    {
        if (cloud != null)
            cloud.OnCloudCoverageChanged -= UpdateCoverage;
    }

    private void UpdateCoverage(float coverage01)
    {
        if (!cloudMaterial) return;

        cloudMaterial.SetFloat(CoverageID, Mathf.Clamp01(1 - coverage01)); // Because of inverted slider
    }

#if UNITY_EDITOR
    [ContextMenu("Test Coverage 50%")]
    private void Test50() => UpdateCoverage(0.5f);

    [ContextMenu("Test Coverage 100%")]
    private void Test100() => UpdateCoverage(1f);
#endif
}
