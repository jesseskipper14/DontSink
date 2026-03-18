using UnityEngine;

[DisallowMultipleComponent]
public sealed class NodeWaterBottomBinder : MonoBehaviour
{
    [SerializeField] private MonoBehaviour groundSource; // must implement IGroundFillBottomSource
    [SerializeField] private WaterMeshRenderer water;
    [SerializeField] private float extraDepth = 0f;

    private IGroundFillBottomSource _ground;

    private void Awake()
    {
        if (groundSource == null)
            groundSource = FindFirstGroundSource();

        _ground = groundSource as IGroundFillBottomSource;

        if (water == null)
            water = FindAnyObjectByType<WaterMeshRenderer>();
    }

    private void OnEnable()
    {
        if (_ground != null)
            _ground.OnBottomYChanged += HandleBottomChanged;

        Apply();
    }

    private void OnDisable()
    {
        if (_ground != null)
            _ground.OnBottomYChanged -= HandleBottomChanged;
    }

    private MonoBehaviour FindFirstGroundSource()
    {
        MonoBehaviour[] all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] is IGroundFillBottomSource)
                return all[i];
        }

        return null;
    }

    private void HandleBottomChanged(float bottomY)
    {
        ApplyFromBottom(bottomY);
    }

    [ContextMenu("Apply")]
    public void Apply()
    {
        if (_ground == null || water == null) return;
        ApplyFromBottom(_ground.LastUsedBottomY);
    }

    private void ApplyFromBottom(float groundBottomY)
    {
        if (water == null) return;
        water.bottomY = groundBottomY - extraDepth;
    }
}