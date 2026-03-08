using UnityEngine;

[DisallowMultipleComponent]
public sealed class NodeWaterBottomBinder : MonoBehaviour
{
    [SerializeField] private NodeGroundSpriteShapeBinder ground;
    [SerializeField] private WaterMeshRenderer water;
    [SerializeField] private float extraDepth = 0f;

    private void Awake()
    {
        if (ground == null) ground = FindAnyObjectByType<NodeGroundSpriteShapeBinder>();
        if (water == null) water = FindAnyObjectByType<WaterMeshRenderer>();
    }

    private void OnEnable()
    {
        if (ground != null)
            ground.OnBottomYChanged += HandleBottomChanged;

        Apply();
    }

    private void OnDisable()
    {
        if (ground != null)
            ground.OnBottomYChanged -= HandleBottomChanged;
    }

    private void HandleBottomChanged(float bottomY)
    {
        ApplyFromBottom(bottomY);
    }

    [ContextMenu("Apply")]
    public void Apply()
    {
        if (ground == null || water == null) return;
        ApplyFromBottom(ground.LastUsedBottomY);
    }

    private void ApplyFromBottom(float groundBottomY)
    {
        if (water == null) return;
        water.bottomY = groundBottomY - extraDepth;
    }
}